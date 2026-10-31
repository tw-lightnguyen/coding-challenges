using CodingChallenge.DriverManagement.Data;
using CodingChallenge.DriverManagement.Enums;
using CodingChallenge.DriverManagement.Models;

namespace CodingChallenge.DriverManagement.Services;

public class DriverService
{
    private readonly ApplicationDbContext _dbContext;

    public DriverService(ApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task UpdateDriverAvailability(Guid driverId, bool isAvailable)
    {
        var driver = await _dbContext.Drivers.FindAsync(driverId);
        if (driver == null)
        {
            throw new Exception("Driver not found.");
        }

        driver.Status = isAvailable ? DriverStatus.AVAILABLE : DriverStatus.OFFLINE;
        await _dbContext.SaveChangesAsync();
    }

    public async Task SendOfferToDriver(Guid driverId, Guid rideId, decimal price)
    {
        var driver = await _dbContext.Drivers.FindAsync(driverId);
        if (driver == null)
        {
            throw new Exception("Driver not found.");
        }

        driver.ReceiveOffer();

        var offer = new Offer
        {
            DriverId = driverId,
            RideId = rideId,
            Price = price,
            Status = OfferStatus.PENDING
        };

        await _dbContext.Offers.AddAsync(offer);
        await _dbContext.SaveChangesAsync();
    }

    public async Task AcceptOffer(Guid offerId)
    {
        var offer = await _dbContext.Offers.FindAsync(offerId);
        if (offer == null)
        {
            throw new Exception("Offer not found.");
        }

        var driver = await _dbContext.Drivers.FindAsync(offer.DriverId);
        if (driver != null)
        {
            driver.AcceptOffer();
        }

        offer.Status = OfferStatus.ACCEPTED;

        await _dbContext.SaveChangesAsync();
    }

    public async Task RejectOffer(Guid offerId)
    {
        var offer = await _dbContext.Offers.FindAsync(offerId);
        if (offer == null)
        {
            throw new Exception("Offer not found.");
        }

        var driver = await _dbContext.Drivers.FindAsync(offer.DriverId);
        if (driver != null)
        {
            driver.Status = DriverStatus.AVAILABLE;
        }

        offer.Status = OfferStatus.REJECTED;

        await _dbContext.SaveChangesAsync();
    }
}