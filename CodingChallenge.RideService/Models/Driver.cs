using CodingChallenge.RideService.Enums;

namespace CodingChallenge.RideService.Models;

public class Driver : BaseModel
{
    public DriverStatus Status { get; set; }

    public required Coordinate Coordinate { get; set; }
}