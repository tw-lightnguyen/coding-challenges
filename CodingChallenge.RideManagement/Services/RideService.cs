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
            await PublishRideCreated(existingRide);
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
}