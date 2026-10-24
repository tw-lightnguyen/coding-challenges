using CodingChallenge.RideService.Enums;

namespace CodingChallenge.RideService.Models;

public class Ride : BaseModel
{
    public Guid PassengerId { get; set; }
    public required Coordinate Pickup { get; set; }
    public required Coordinate Destination { get; set; }
    public RideStatus Status { get; set; }
    public Guid? AssignedDriverId { get; set; }
}