using CodingChallenge.RideService.Data;
using CodingChallenge.RideService.Enums;
using CodingChallenge.RideService.Models;
using Microsoft.EntityFrameworkCore;

namespace CodingChallenge.RideService.Tests;

public class RideServiceTest
{
    private DbContextOptions<ApplicationDbContext> _options;

    public RideServiceTest()
    {
        _options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(
                databaseName: "RideTest")
            .Options;
    }

    [Fact]
    public async Task CreateRide_WithValidData_ShouldSuccess()
    {
        //Arrage
        var passengerId = Guid.CreateVersion7();
        var idempotencyKey = Guid.NewGuid();

        var pickup = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f));
        var destination = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f));

        using var dbContext = new ApplicationDbContext(_options);
        var service = new Services.RideService(dbContext);

        // Act
        var ride = await service.CreateRide(passengerId, pickup, destination, idempotencyKey);

        //Assert
        Assert.NotNull(ride);
        Assert.Equal(passengerId, ride.PassengerId);
        Assert.Equal(pickup, ride.Pickup);
        Assert.Equal(destination, ride.Destination);
        Assert.Equal(RideStatus.REQUESTED, ride.Status);
        Assert.Equal(idempotencyKey, ride.IdempotencyKey);
        Assert.Null(ride.AssignedDriverId);
    }

    [Fact]
    public async Task CreateRide_WithSameIdempotencyKey_ShouldReturnOriginalResult()
    {
        //Arrage
        var passengerId = Guid.CreateVersion7();
        var idempotencyKey = Guid.NewGuid();

        var pickup = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f));
        var destination = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f));

        using var dbContext = new ApplicationDbContext(_options);
        var service = new Services.RideService(dbContext);

        // Act
        var ride = await service.CreateRide(passengerId, pickup, destination, idempotencyKey);
        var ride2 = await service.CreateRide(passengerId, pickup, destination, idempotencyKey);

        //Assert
        Assert.NotNull(ride);
        Assert.NotNull(ride2);
        Assert.Equal(ride.Id, ride2.Id);
        Assert.Equal(ride.Pickup, ride2.Pickup);
        Assert.Equal(ride.Destination, ride2.Destination);
        Assert.Equal(ride.Status, ride2.Status);
        Assert.Equal(ride.PassengerId, ride2.PassengerId);
    }

    private static float RandomFloat(float min, float max)
    {
        return (Random.Shared.NextSingle() * (max - min)) + min;
    }
}
