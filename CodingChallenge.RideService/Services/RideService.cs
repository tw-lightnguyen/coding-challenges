using CodingChallenge.RideService.Data;
using CodingChallenge.RideService.Enums;
using CodingChallenge.RideService.Models;

namespace CodingChallenge.RideService.Services;

public class RideService
{
    private readonly ApplicationDbContext _dbContext;

    public RideService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Ride> CreateRide(Guid passengerId, Coordinate pickup, Coordinate destination, Guid idempotencyKey)
    {
        var existing = _dbContext.Rides.FirstOrDefault(r => r.IdempotencyKey == idempotencyKey);
        if (existing != null)
        {
            return existing;
        }

        var ride = new Ride
        {
            PassengerId = passengerId,
            Pickup = pickup,
            Destination = destination,
            Status = RideStatus.REQUESTED,
            IdempotencyKey = idempotencyKey
        };

        var result = await _dbContext.Rides.AddAsync(ride);

        await _dbContext.SaveChangesAsync();
        
        return result.Entity;
    }
}