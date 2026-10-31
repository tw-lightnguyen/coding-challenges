using CodingChallenge.RideManagement.Data;
using CodingChallenge.RideManagement.Dtos;
using CodingChallenge.RideManagement.Enums;
using CodingChallenge.RideManagement.Services;
using CodingChallenge.Share.Data;
using CodingChallenge.Share.Models;
using Microsoft.EntityFrameworkCore;

namespace CodingChallenge.RideManagement.Tests;

public class RideServiceTest
{
    private DbContextOptions<BaseDbContext> _options;

    public RideServiceTest()
    {
        _options = new DbContextOptionsBuilder<BaseDbContext>()
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
        var service = new RideService(dbContext);

        // Act
        var ride = await service.CreateRide(request, idempotencyKey);

        //Assert
        Assert.NotNull(ride);
        Assert.Equal(request.PassengerId, ride.PassengerId);
        Assert.Equal(request.Pickup, ride.Pickup);
        Assert.Equal(request.Destination, ride.Destination);
        Assert.Equal(RideStatus.REQUESTED, ride.Status);
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
        var service = new RideService(dbContext);

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
        var service = new RideService(dbContext);

        // Act
        var ride = await service.CreateRide(request, idempotencyKey);
        await Assert.ThrowsAsync<Exception>(async () => await service.CreateRide(request2, idempotencyKey));
    }

    [Fact]
    public async Task GetRideById_AfterCreatingRide_ShouldReturnRide()
    {
        // Arrange
        var idempotencyKey = Guid.NewGuid();

        var request = new CreateRideRequest
        {
            PassengerId = Guid.CreateVersion7(),
            Pickup = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f)),
            Destination = new Coordinate(RandomFloat(-90f, 90f), RandomFloat(-180f, 180f))
        };

        using var dbContext = new ApplicationDbContext(_options);
        var service = new RideService(dbContext);

        // Act
        var createdRide = await service.CreateRide(request, idempotencyKey);
        var retrievedRide = await service.GetRideById(createdRide.Id);

        // Assert
        Assert.NotNull(retrievedRide);
        Assert.Equal(createdRide.Id, retrievedRide!.Id);
        Assert.Equal(createdRide.PassengerId, retrievedRide.PassengerId);
        Assert.Equal(createdRide.Pickup, retrievedRide.Pickup);
        Assert.Equal(createdRide.Destination, retrievedRide.Destination);
        Assert.Equal(createdRide.Status, retrievedRide.Status);
    }

    private static float RandomFloat(float min, float max)
    {
        return (Random.Shared.NextSingle() * (max - min)) + min;
    }
}
