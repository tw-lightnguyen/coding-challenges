using CodingChallenge.RideService.Data;
using CodingChallenge.RideService.Dtos;
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
        var idempotencyKey = Guid.NewGuid();

        var request = new CreateRideRequest
        {
            PassengerId = Guid.CreateVersion7(),
            Pickup = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f)),
            Destination = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f))
        };

        using var dbContext = new ApplicationDbContext(_options);
        var service = new Services.RideService(dbContext);

        // Act
        var ride = await service.CreateRide(request, idempotencyKey);

        //Assert
        Assert.NotNull(ride);
        Assert.Equal(request.PassengerId, ride.PassengerId);
        Assert.Equal(request.Pickup, ride.Pickup);
        Assert.Equal(request.Destination, ride.Destination);
        Assert.Equal(RideStatus.REQUESTED, ride.Status);
        Assert.Equal(idempotencyKey, ride.IdempotencyKey);
        Assert.Null(ride.AssignedDriverId);
    }

    [Fact]
    public async Task CreateRide_WithSameIdempotencyKeyAndRequest_ShouldReturnOriginalResult()
    {
        //Arrage
        var idempotencyKey = Guid.NewGuid();

        var request = new CreateRideRequest
        {
            PassengerId = Guid.CreateVersion7(),
            Pickup = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f)),
            Destination = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f))
        };

        using var dbContext = new ApplicationDbContext(_options);
        var service = new Services.RideService(dbContext);

        // Act
        var ride = await service.CreateRide(request, idempotencyKey);
        var ride2 = await service.CreateRide(request, idempotencyKey);

        //Assert
        Assert.NotNull(ride);
        Assert.NotNull(ride2);
        Assert.Equal(ride.Id, ride2.Id);
        Assert.Equivalent(ride.Pickup, ride2.Pickup);
        Assert.Equivalent(ride.Destination, ride2.Destination);
        Assert.Equal(ride.Status, ride2.Status);
        Assert.Equal(ride.PassengerId, ride2.PassengerId);
    }

    [Fact]
    public async Task CreateRide_WithSameIdempotencyKeyAndDifferentRequest_ShouldThrowException()
    {
        //Arrage
        var idempotencyKey = Guid.NewGuid();

        var request = new CreateRideRequest
        {
            PassengerId = Guid.CreateVersion7(),
            Pickup = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f)),
            Destination = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f))
        };

        var request2 = new CreateRideRequest
        {
            PassengerId = Guid.CreateVersion7(),
            Pickup = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f)),
            Destination = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f))
        };

        using var dbContext = new ApplicationDbContext(_options);
        var service = new Services.RideService(dbContext);

        // Act
        var ride = await service.CreateRide(request, idempotencyKey);
        await Assert.ThrowsAsync<Exception>(async () => await service.CreateRide(request2, idempotencyKey));
    }

    private static float RandomFloat(float min, float max)
    {
        return (Random.Shared.NextSingle() * (max - min)) + min;
    }
}
