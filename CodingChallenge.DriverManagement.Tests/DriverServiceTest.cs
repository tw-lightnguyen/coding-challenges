using CodingChallenge.DriverManagement.Data;
using CodingChallenge.DriverManagement.Enums;
using CodingChallenge.DriverManagement.Models;
using CodingChallenge.DriverManagement.Services;
using CodingChallenge.Share.Data;
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
