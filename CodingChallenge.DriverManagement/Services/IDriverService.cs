namespace CodingChallenge.DriverManagement.Services;

public interface IDriverService
{
    Task UpdateDriverAvailability(Guid driverId, bool isAvailable);
    Task<bool> UpdateDriverLocation(Guid driverId, float latitude, float longitude);
    Task SendOfferToDriver(Guid driverId, Guid rideId, decimal price);
    Task AcceptOffer(Guid offerId);
    Task RejectOffer(Guid offerId);
}
