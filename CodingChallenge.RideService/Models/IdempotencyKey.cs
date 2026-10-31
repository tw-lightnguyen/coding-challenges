using System.ComponentModel.DataAnnotations;

namespace CodingChallenge.RideService.Models;

public class IdempotencyKey
{
    [Key]
    public Guid Id { get; set; }
    public string RequestHash { get; set; }
    public string Response { get; set; }
}