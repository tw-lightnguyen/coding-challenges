using CodingChallenge.DriverManagement.Data;
using CodingChallenge.DriverManagement.Enums;
using CodingChallenge.DriverManagement.Models;
using CodingChallenge.DriverManagement.Services;
using CodingChallenge.Share.Data;
using CodingChallenge.Share.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CodingChallenge.DriverManagement.Tests;

public class DriverServiceTest
{
    private readonly DbContextOptions<BaseDbContext> _options;
    public DriverServiceTest()
    {
        _options = new DbContextOptionsBuilder<BaseDbContext>()
            .UseInMemoryDatabase(databaseName: "DriverTest")
            .Options;
    }

    [Fact]
    public async Task FindNearestAvailableDriver_ShouldIgnoreUnavailableAndOutOfRadiusDrivers()
    {
        var nearestId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        dbContext.Drivers.AddRange(
            new Driver
            {
                Id = nearestId,
                Name = "Nearest",
                Status = DriverStatus.AVAILABLE,
                Coordinate = new Coordinate(0, 0.005f)
            },
            new Driver
            {
                Name = "Farther",
                Status = DriverStatus.AVAILABLE,
                Coordinate = new Coordinate(0, 0.01f)
            },
            new Driver
            {
                Name = "Unavailable",
                Status = DriverStatus.BUSY,
                Coordinate = new Coordinate(0, 0.001f)
            },
            new Driver
            {
                Name = "Outside radius",
                Status = DriverStatus.AVAILABLE,
                Coordinate = new Coordinate(0, 0.03f)
            },
            new Driver
            {
                Name = "Missing location",
                Status = DriverStatus.AVAILABLE
            });
        await dbContext.SaveChangesAsync();

        var matchingService = CreateMatchingService(dbContext);
        var selectedDriverId = await matchingService.FindNearestAvailableDriverId(new Coordinate(0, 0));

        Assert.Equal(nearestId, selectedDriverId);
    }

    [Fact]
    public async Task FindNearestAvailableDriver_WhenEquidistant_ShouldSelectLexicographicallySmallestId()
    {
        var smallerId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var largerId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        dbContext.Drivers.AddRange(
            new Driver
            {
                Id = largerId,
                Name = "Larger id",
                Status = DriverStatus.AVAILABLE,
                Coordinate = new Coordinate(0, 0.005f)
            },
            new Driver
            {
                Id = smallerId,
                Name = "Smaller id",
                Status = DriverStatus.AVAILABLE,
                Coordinate = new Coordinate(0, -0.005f)
            });
        await dbContext.SaveChangesAsync();

        var selectedDriverId = await CreateMatchingService(dbContext)
            .FindNearestAvailableDriverId(new Coordinate(0, 0));

        Assert.Equal(smallerId, selectedDriverId);
    }

    [Fact]
    public async Task FindNearestAvailableDriver_WhenNoSuitableDriverExists_ShouldReturnNull()
    {
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        dbContext.Drivers.Add(new Driver
        {
            Name = "Outside radius",
            Status = DriverStatus.AVAILABLE,
            Coordinate = new Coordinate(0, 0.03f)
        });
        await dbContext.SaveChangesAsync();

        var selectedDriverId = await CreateMatchingService(dbContext)
            .FindNearestAvailableDriverId(new Coordinate(0, 0));

        Assert.Null(selectedDriverId);
    }

    [Fact]
    public async Task FindNearestAvailableDriver_ShouldHonorConfiguredRadius()
    {
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        var driver = new Driver
        {
            Name = "Within default radius",
            Status = DriverStatus.AVAILABLE,
            Coordinate = new Coordinate(0, 0.015f)
        };
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var defaultRadiusMatch = await CreateMatchingService(dbContext, 2)
            .FindNearestAvailableDriverId(new Coordinate(0, 0));
        var smallerRadiusMatch = await CreateMatchingService(dbContext, 1.5)
            .FindNearestAvailableDriverId(new Coordinate(0, 0));

        Assert.Equal(driver.Id, defaultRadiusMatch);
        Assert.Null(smallerRadiusMatch);
    }

    private static DbContextOptions<BaseDbContext> CreateIsolatedOptions() =>
        new DbContextOptionsBuilder<BaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static DriverMatchingService CreateMatchingService(
        ApplicationDbContext dbContext,
        double radiusKilometers = 2) =>
        new(
            dbContext,
            Options.Create(new DriverMatchingOptions
            {
                MaxRadiusKilometers = radiusKilometers
            }));

    [Theory]
    [InlineData(true, DriverStatus.AVAILABLE)]
    [InlineData(false, DriverStatus.OFFLINE)]
    public async Task UpdateDriverAvailability_ShouldUpdateDriverStatus(bool isAvailable, DriverStatus expectedStatus)
    {
        // Arrange
        using var dbContext = new ApplicationDbContext(_options);
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            Name = "John Doe",
            Status = DriverStatus.OFFLINE
        };

        dbContext.Drivers.Add(driver);
        dbContext.SaveChanges();

        var service = new DriverService(dbContext);

        // Act
        await service.UpdateDriverAvailability(driver.Id, isAvailable);

        // Assert
        var updatedDriver = dbContext.Drivers.Find(driver.Id);
        Assert.NotNull(updatedDriver);
        Assert.Equal(expectedStatus, updatedDriver.Status);
    }

    [Fact]
    public async Task UpdateDriverLocation_ShouldStoreLatestLocation()
    {
        var driverId = Guid.NewGuid();
        using (var dbContext = new ApplicationDbContext(_options))
        {
            dbContext.Drivers.Add(new Driver
            {
                Id = driverId,
                Name = "Location Test",
                Status = DriverStatus.AVAILABLE,
                Coordinate = new Coordinate(1, 2)
            });
            await dbContext.SaveChangesAsync();

            var service = new DriverService(dbContext);
            Assert.True(await service.UpdateDriverLocation(driverId, 10, 20));
            Assert.True(await service.UpdateDriverLocation(driverId, -30, -40));
        }

        using var verifyContext = new ApplicationDbContext(_options);
        var updatedDriver = await verifyContext.Drivers.FindAsync(driverId);
        Assert.NotNull(updatedDriver?.Coordinate);
        Assert.Equal(-30, updatedDriver.Coordinate.Latitude);
        Assert.Equal(-40, updatedDriver.Coordinate.Longtitude);
    }

    [Theory]
    [InlineData(-91, 0)]
    [InlineData(91, 0)]
    [InlineData(0, -181)]
    [InlineData(0, 181)]
    public async Task UpdateDriverLocation_ShouldRejectInvalidCoordinates(float latitude, float longitude)
    {
        using var dbContext = new ApplicationDbContext(_options);
        var service = new DriverService(dbContext);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.UpdateDriverLocation(Guid.NewGuid(), latitude, longitude));
    }

    [Fact]
    public async Task UpdateDriverLocation_ShouldRejectNonFiniteCoordinates()
    {
        using var dbContext = new ApplicationDbContext(_options);
        var service = new DriverService(dbContext);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.UpdateDriverLocation(Guid.NewGuid(), float.NaN, 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => service.UpdateDriverLocation(Guid.NewGuid(), 0, float.PositiveInfinity));
    }

    [Fact]
    public async Task UpdateDriverLocation_ShouldReturnFalseForMissingDriver()
    {
        using var dbContext = new ApplicationDbContext(_options);
        var service = new DriverService(dbContext);

        Assert.False(await service.UpdateDriverLocation(Guid.NewGuid(), 0, 0));
    }

    [Fact]
    public async Task SendOfferToDriver_ShouldCreateOfferAndUpdateDriverStatus()
    {
        // Arrange
        using var dbContext = new ApplicationDbContext(_options);
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            Name = "Jane Doe",
            Status = DriverStatus.AVAILABLE
        };

        dbContext.Drivers.Add(driver);
        dbContext.SaveChanges();

        var service = new DriverService(dbContext);
        var rideId = Guid.NewGuid();
        decimal price = 100.0m;

        // Act
        await service.SendOfferToDriver(driver.Id, rideId, price);

        // Assert
        var updatedDriver = dbContext.Drivers.Find(driver.Id);
        var offer = dbContext.Offers.FirstOrDefault(o => o.DriverId == driver.Id && o.RideId == rideId);

        Assert.NotNull(updatedDriver);
        Assert.Equal(DriverStatus.OFFERED, updatedDriver.Status);
        Assert.NotNull(offer);
        Assert.Equal(price, offer.Price);
        Assert.Equal(OfferStatus.PENDING, offer.Status);
    }

    [Theory]
    [InlineData(DriverStatus.OFFLINE)]
    [InlineData(DriverStatus.BUSY)]
    [InlineData(DriverStatus.OFFERED)]
    public async Task SendOfferToUnavailableDriver_ShouldThrowException(DriverStatus unavailableStatus)
    {
        // Arrange
        using var dbContext = new ApplicationDbContext(_options);
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            Name = "Bob Smith",
            Status = unavailableStatus
        };

        dbContext.Drivers.Add(driver);
        dbContext.SaveChanges();

        var service = new DriverService(dbContext);
        var rideId = Guid.NewGuid();
        decimal price = 100.0m;

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await service.SendOfferToDriver(driver.Id, rideId, price));
    }

    [Theory]
    [InlineData(true, OfferStatus.ACCEPTED, DriverStatus.BUSY)]
    [InlineData(false, OfferStatus.REJECTED, DriverStatus.AVAILABLE)]
    public async Task AcceptOrRejectOffer_ShouldUpdateOfferAndDriverStatus(bool accept, OfferStatus expectedOfferStatus, DriverStatus expectedDriverStatus)
    {
        // Arrange
        using var dbContext = new ApplicationDbContext(_options);
        var driver = new Driver
        {
            Id = Guid.NewGuid(),
            Name = "Alice Smith",
            Status = DriverStatus.OFFERED
        };

        var offer = new Offer
        {
            Id = Guid.NewGuid(),
            DriverId = driver.Id,
            RideId = Guid.NewGuid(),
            Price = 150.0m,
            Status = OfferStatus.PENDING
        };

        dbContext.Drivers.Add(driver);
        dbContext.Offers.Add(offer);
        dbContext.SaveChanges();

        var service = new DriverService(dbContext);

        // Act
        if (accept)
        {
            await service.AcceptOffer(offer.Id);
        }
        else
        {
            await service.RejectOffer(offer.Id);
        }

        // Assert
        var updatedOffer = dbContext.Offers.Find(offer.Id);
        var updatedDriver = dbContext.Drivers.Find(driver.Id);

        Assert.NotNull(updatedOffer);
        Assert.Equal(expectedOfferStatus, updatedOffer.Status);
        Assert.NotNull(updatedDriver);
        Assert.Equal(expectedDriverStatus, updatedDriver.Status);
    }

    [Fact]
    public async Task RejectOffer_ShouldExcludeDriverFromTheSameRideMatching()
    {
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        var rejectedDriver = new Driver
        {
            Name = "Rejected",
            Status = DriverStatus.AVAILABLE,
            Coordinate = new Coordinate(0, 0.001f)
        };
        var nextDriver = new Driver
        {
            Name = "Next",
            Status = DriverStatus.AVAILABLE,
            Coordinate = new Coordinate(0, 0.005f)
        };
        dbContext.Drivers.AddRange(rejectedDriver, nextDriver);
        await dbContext.SaveChangesAsync();

        var driverService = new DriverService(dbContext);
        var rideId = Guid.NewGuid();
        await driverService.SendOfferToDriver(rejectedDriver.Id, rideId, 0m);
        await driverService.RejectOffer(rideId, rejectedDriver.Id);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => driverService.SendOfferToDriver(rejectedDriver.Id, rideId, 0m));

        var selectedDriverId = await CreateMatchingService(dbContext)
            .FindNearestAvailableDriverId(new Coordinate(0, 0), rideId);

        Assert.Equal(nextDriver.Id, selectedDriverId);
        Assert.Equal(DriverStatus.AVAILABLE, rejectedDriver.Status);
    }

    [Fact]
    public async Task ExpirePendingOffers_ShouldReleaseDriverAndMarkOfferTimedOut()
    {
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        var driver = new Driver
        {
            Name = "Timeout",
            Status = DriverStatus.AVAILABLE
        };
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var service = new DriverService(
            dbContext,
            Options.Create(new RideOfferOptions { ResponseTimeoutSeconds = 1 }));
        await service.SendOfferToDriver(driver.Id, Guid.NewGuid(), 0m);
        var offer = Assert.Single(dbContext.Offers);
        offer.CreatedDateTime = DateTime.UtcNow.AddSeconds(-2);
        await dbContext.SaveChangesAsync();

        var expiredOffers = await service.ExpirePendingOffers();

        Assert.Contains(offer, expiredOffers);
        Assert.Equal(OfferStatus.TIMED_OUT, offer.Status);
        Assert.Equal(DriverStatus.AVAILABLE, driver.Status);
    }

    [Fact]
    public async Task CancelOfferAndCompleteRide_ShouldReleaseTheDriver()
    {
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        var driver = new Driver
        {
            Name = "Lifecycle",
            Status = DriverStatus.AVAILABLE
        };
        dbContext.Drivers.Add(driver);
        await dbContext.SaveChangesAsync();

        var service = new DriverService(dbContext);
        var cancelledRideId = Guid.NewGuid();
        await service.SendOfferToDriver(driver.Id, cancelledRideId, 0m);
        await service.CancelOffer(cancelledRideId, driver.Id);
        var cancelledOffer = Assert.Single(dbContext.Offers.Where(offer => offer.RideId == cancelledRideId));
        Assert.Equal(OfferStatus.CANCELLED, cancelledOffer.Status);
        Assert.Equal(DriverStatus.AVAILABLE, driver.Status);

        var acceptedRideId = Guid.NewGuid();
        await service.SendOfferToDriver(driver.Id, acceptedRideId, 0m);
        await service.AcceptOffer(acceptedRideId, driver.Id);
        var acceptedOffer = Assert.Single(dbContext.Offers.Where(offer => offer.RideId == acceptedRideId));
        Assert.Equal(OfferStatus.ACCEPTED, acceptedOffer.Status);
        Assert.Equal(DriverStatus.BUSY, driver.Status);

        await service.CompleteRide(acceptedRideId, driver.Id);
        Assert.Equal(DriverStatus.AVAILABLE, driver.Status);
    }

    [Fact]
    public async Task CancelPendingOffers_ShouldReleaseEveryDriverOnTheRide()
    {
        var options = CreateIsolatedOptions();
        using var dbContext = new ApplicationDbContext(options);
        var drivers = new[]
        {
            new Driver { Name = "First", Status = DriverStatus.AVAILABLE },
            new Driver { Name = "Second", Status = DriverStatus.AVAILABLE }
        };
        dbContext.Drivers.AddRange(drivers);
        await dbContext.SaveChangesAsync();

        var rideId = Guid.NewGuid();
        var service = new DriverService(dbContext);
        foreach (var driver in drivers)
        {
            await service.SendOfferToDriver(driver.Id, rideId, 0m);
        }

        await service.CancelPendingOffers(rideId);

        Assert.All(drivers, driver => Assert.Equal(DriverStatus.AVAILABLE, driver.Status));
        Assert.All(
            dbContext.Offers.Where(offer => offer.RideId == rideId),
            offer => Assert.Equal(OfferStatus.CANCELLED, offer.Status));
    }
}
