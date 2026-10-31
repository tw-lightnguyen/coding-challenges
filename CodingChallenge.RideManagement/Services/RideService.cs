using System.Text.Json;
using CodingChallenge.RideManagement.Data;
using CodingChallenge.RideManagement.Dtos;
using CodingChallenge.RideManagement.Enums;
using CodingChallenge.RideManagement.Helpers;
using CodingChallenge.RideManagement.Models;
using CodingChallenge.Share.Messaging;

namespace CodingChallenge.RideManagement.Services;

public class RideService : IRideService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    public RideService(
        ApplicationDbContext dbContext,
        IEventPublisher eventPublisher)
    {
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    public async Task<Ride> CreateRide(CreateRideRequest request, Guid idempotencyKey)
    {
        var existing = _dbContext.IdempotencyKeys.FirstOrDefault(r => r.Id == idempotencyKey);
        if (existing != null)
        {
            var requestHash = RequestHelper.Hash(request);
            if (existing.RequestHash != requestHash)
            {
                throw new Exception("Idempotency key was already used with a different request.");
            }

            var existingRide = JsonSerializer.Deserialize<Ride>(existing.Response)!;
            var currentRide = await _dbContext.Rides.FindAsync(existingRide.Id);
            if (currentRide?.Status != RideStatus.CANCELLED)
            {
                await PublishRideCreated(existingRide);
            }

            return existingRide;
        }

        var ride = new Ride
        {
            PassengerId = request.PassengerId,
            Pickup = request.Pickup,
            Destination = request.Destination,
            Status = RideStatus.REQUESTED,
        };

        var idempotencyRecord = new IdempotencyKey
        {
            Id = idempotencyKey,
            RequestHash = RequestHelper.Hash(request),
            Response = JsonSerializer.Serialize(ride)
        };

        var result = await _dbContext.Rides.AddAsync(ride);
        await _dbContext.IdempotencyKeys.AddAsync(idempotencyRecord);

        await _dbContext.SaveChangesAsync();

        await PublishRideCreated(result.Entity);
        return result.Entity;
    }

    private Task PublishRideCreated(Ride ride) =>
        _eventPublisher.PublishAsync(
            EventRoutingKeys.RideCreated,
            new RideCreatedEvent(
                ride.Id,
                ride.Pickup.Latitude,
                ride.Pickup.Longtitude));

    public async Task<Ride?> GetRideById(Guid rideId)
    {
        return await _dbContext.Rides.FindAsync(rideId);
    }

    public async Task HandleDriverMatched(DriverMatchedEvent message)
    {
        var ride = await GetRequiredRide(message.RideId);
        if (ride.Status is RideStatus.DRIVER_ASSIGNED or RideStatus.DRIVER_ACCEPTED
            or RideStatus.CANCELLED or RideStatus.COMPLETED)
        {
            return;
        }

        if (ride.Status is not (RideStatus.REQUESTED or RideStatus.MATCHING))
        {
            return;
        }

        if (ride.AssignedDriverId.HasValue)
        {
            if (ride.AssignedDriverId == message.DriverId)
            {
                return;
            }

            return;
        }

        if (message.DriverId.HasValue)
        {
            ride.AssignedDriverId = message.DriverId;
            ride.Status = RideStatus.DRIVER_ASSIGNED;
        }
        else
        {
            ride.Status = RideStatus.MATCHING;
        }

        await _dbContext.SaveChangesAsync();
    }

    public async Task RequestOfferResponse(Guid rideId, Guid driverId, RideDriverAction action)
    {
        if (action is not (RideDriverAction.Accept or RideDriverAction.Reject))
        {
            throw new ArgumentOutOfRangeException(nameof(action));
        }

        var ride = await GetRequiredRide(rideId);
        if (ride.Status != RideStatus.DRIVER_ASSIGNED || ride.AssignedDriverId != driverId)
        {
            throw new InvalidOperationException("Only the driver holding the current offer can respond.");
        }

        await _eventPublisher.PublishAsync(
            EventRoutingKeys.RideDriverAction,
            new RideDriverActionEvent(rideId, driverId, action));
    }

    public async Task CancelRide(Guid rideId)
    {
        var ride = await GetRequiredRide(rideId);
        if (ride.Status is not (RideStatus.REQUESTED or RideStatus.MATCHING
            or RideStatus.DRIVER_ASSIGNED or RideStatus.DRIVER_ACCEPTED))
        {
            throw new InvalidOperationException("This ride can no longer be cancelled.");
        }

        if (ride.AssignedDriverId.HasValue)
        {
            await _eventPublisher.PublishAsync(
                EventRoutingKeys.RideDriverAction,
                new RideDriverActionEvent(rideId, ride.AssignedDriverId.Value, RideDriverAction.Cancel));
            return;
        }

        ride.Status = RideStatus.CANCELLED;
        await _dbContext.SaveChangesAsync();
        await _eventPublisher.PublishAsync(
            EventRoutingKeys.RideCreated,
            new RideCreatedEvent(
                ride.Id,
                ride.Pickup.Latitude,
                ride.Pickup.Longtitude,
                IsCancelled: true));
    }

    public async Task CompleteRide(Guid rideId)
    {
        var ride = await GetRequiredRide(rideId);
        if (ride.Status != RideStatus.DRIVER_ACCEPTED || !ride.AssignedDriverId.HasValue)
        {
            throw new InvalidOperationException("Only an accepted ride can be completed.");
        }

        await _eventPublisher.PublishAsync(
            EventRoutingKeys.RideDriverAction,
            new RideDriverActionEvent(rideId, ride.AssignedDriverId.Value, RideDriverAction.Complete));
    }

    public async Task HandleDriverActionCompleted(RideDriverActionCompletedEvent message)
    {
        var ride = await _dbContext.Rides.FindAsync(message.RideId);
        if (ride == null || ride.AssignedDriverId != message.DriverId)
        {
            return;
        }

        switch (message.Action)
        {
            case RideDriverAction.Accept when ride.Status == RideStatus.DRIVER_ASSIGNED:
                ride.Status = RideStatus.DRIVER_ACCEPTED;
                await _dbContext.SaveChangesAsync();
                break;
            case RideDriverAction.Reject when ride.Status == RideStatus.DRIVER_ASSIGNED:
                ride.AssignedDriverId = null;
                ride.Status = RideStatus.MATCHING;
                await _dbContext.SaveChangesAsync();
                await _eventPublisher.PublishAsync(
                    EventRoutingKeys.RideCreated,
                    new RideCreatedEvent(
                        ride.Id,
                        ride.Pickup.Latitude,
                        ride.Pickup.Longtitude));
                break;
            case RideDriverAction.Cancel when ride.Status is RideStatus.DRIVER_ASSIGNED
                or RideStatus.DRIVER_ACCEPTED:
                ride.AssignedDriverId = null;
                ride.Status = RideStatus.CANCELLED;
                await _dbContext.SaveChangesAsync();
                break;
            case RideDriverAction.Complete when ride.Status == RideStatus.DRIVER_ACCEPTED:
                ride.Status = RideStatus.COMPLETED;
                await _dbContext.SaveChangesAsync();
                break;
        }
    }

    private async Task<Ride> GetRequiredRide(Guid rideId)
    {
        return await _dbContext.Rides.FindAsync(rideId)
            ?? throw new KeyNotFoundException($"Ride {rideId} was not found.");
    }
}