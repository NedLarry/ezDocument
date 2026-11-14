using Microsoft.AspNetCore.Mvc;

namespace SampleApi.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>Liveness probe.</summary>
    [HttpGet]
    public IActionResult Get() => Ok("healthy");
}
