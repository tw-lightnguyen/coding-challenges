using CodingChallenge.RideService.Models;

namespace CodingChallenge.RideService.Dtos;

public class CreateRideRequest
{
    public Guid PassengerId { get; set; }
    public Coordinate Pickup { get; set; }
    public Coordinate Destination { get; set; }
}