using System.ComponentModel.DataAnnotations;

namespace CodingChallenge.RideService.Models;

public class IdempotencyKey
{
    [Key]
    public Guid Id { get; set; }
    public required string RequestHash { get; set; }
    public required string Response { get; set; }
}