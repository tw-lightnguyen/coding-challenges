using System.ComponentModel.DataAnnotations;

namespace CodingChallenge.RideService.Models;

public class BaseModel
{
    [Key]
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid IdempotencyKey { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedDateTime { get; set; }
    public DateTime UpdatedDateTime { get; set; }
}