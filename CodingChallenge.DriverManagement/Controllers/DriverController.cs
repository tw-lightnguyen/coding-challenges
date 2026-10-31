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
}