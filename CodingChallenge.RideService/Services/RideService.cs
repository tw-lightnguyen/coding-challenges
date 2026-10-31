using System.Text.Json;
using CodingChallenge.RideService.Data;
using CodingChallenge.RideService.Dtos;
using CodingChallenge.RideService.Enums;
using CodingChallenge.RideService.Helpers;
using CodingChallenge.RideService.Models;

namespace CodingChallenge.RideService.Services;

public class RideService
{
    private readonly ApplicationDbContext _dbContext;

    public RideService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
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

            return JsonSerializer.Deserialize<Ride>(existing.Response)!;
        }

        var ride = new Ride
        {
            PassengerId = request.PassengerId,
            Pickup = request.Pickup,
            Destination = request.Destination,
            Status = RideStatus.REQUESTED,
            IdempotencyKey = idempotencyKey
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
        
        return result.Entity;
    }
}