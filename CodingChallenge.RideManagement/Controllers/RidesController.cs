using CodingChallenge.RideManagement.Dtos;
using CodingChallenge.RideManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodingChallenge.RideManagement.Controllers;

[ApiController]
[Route("[controller]")]
public class RidesController : ControllerBase
{
    private readonly RideService _rideService;

    public RidesController(RideService rideService)
    {
        _rideService = rideService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateRide(CreateRideRequest request, [FromHeader(Name = "Idempotency-Key")] Guid idempotencyKey)
    {
        if (idempotencyKey == Guid.Empty)
        {
            return BadRequest("Idempotency key is required.");
        }

        var result = await _rideService.CreateRide(request, idempotencyKey);

        return Ok(result);
    }
}