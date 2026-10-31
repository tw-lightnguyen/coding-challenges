using CodingChallenge.RideService.Models;

namespace CodingChallenge.RideService.Dtos;

public class CreateRideRequest
{
    public Guid PassengerId { get; set; }
    public required Coordinate Pickup { get; set; }
    public required Coordinate Destination { get; set; }
}