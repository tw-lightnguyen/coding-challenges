using CodingChallenge.DriverManagement.Data;
using CodingChallenge.DriverManagement.Enums;
using CodingChallenge.DriverManagement.Models;
using CodingChallenge.DriverManagement.Services;
using CodingChallenge.Share.Data;
using CodingChallenge.Share.Models;
using Microsoft.EntityFrameworkCore;

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
}
