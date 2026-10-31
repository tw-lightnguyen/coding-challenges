using CodingChallenge.RideManagement.Dtos;
using CodingChallenge.RideManagement.Models;

namespace CodingChallenge.RideManagement.Services;

public interface IRideService
{
    Task<Ride> CreateRide(CreateRideRequest request, Guid idempotencyKey);
    Task<Ride?> GetRideById(Guid rideId);
}
