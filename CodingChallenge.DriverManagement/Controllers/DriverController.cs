using CodingChallenge.DriverManagement.Dtos;
using CodingChallenge.DriverManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace CodingChallenge.DriverManagement.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DriverController : ControllerBase
{
    private readonly DriverService _driverService;

    public DriverController(DriverService driverService)
    {
        _driverService = driverService;
    }

    [HttpPut("{driverId}/availability")]
    public async Task<IActionResult> UpdateDriverAvailability(Guid driverId, [FromBody] bool isAvailable)
    {
        await _driverService.UpdateDriverAvailability(driverId, isAvailable);
        return Ok();
    }

    [HttpPut("{driverId:guid}/location")]
    public async Task<IActionResult> UpdateDriverLocation(
        Guid driverId,
        [FromBody] UpdateDriverLocationRequest request)
    {
        var updated = await _driverService.UpdateDriverLocation(
            driverId,
            request.Latitude,
            request.Longitude);

        return updated ? NoContent() : NotFound();
    }
}