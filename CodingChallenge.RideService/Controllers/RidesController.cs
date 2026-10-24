using Microsoft.AspNetCore.Mvc;

namespace CodingChallenge.RideService.Controllers;

[ApiController]
[Route("[controller]")]
public class RidesController : ControllerBase
{
    public Task CreateRide()
    {
        return Task.CompletedTask;
    }
}