using ChallengeGenerator.Services;
using Microsoft.AspNetCore.Mvc;

namespace ChallengeGenerator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChallengesController : ControllerBase
{
    private readonly ChallengeGeneratorService _generator;
    private readonly ILogger<ChallengesController> _logger;

    public ChallengesController(
        ChallengeGeneratorService generator,
        ILogger<ChallengesController> logger)
    {
        _generator = generator;
        _logger = logger;
    }

    /// <summary>
    /// Manually trigger challenge generation
    /// </summary>
    [HttpPost("generate")]
    public async Task<IActionResult> GenerateChallenges()
    {
        _logger.LogInformation("[API] Manual challenge generation triggered");
        
        var result = await _generator.GenerateDailyChallenges();
        
        if (result.Success)
        {
            return Ok(result);
        }
        
        return BadRequest(result);
    }

    /// <summary>
    /// Get current generator configuration
    /// </summary>
    [HttpGet("config")]
    public IActionResult GetConfig()
    {
        var config = _generator.GetConfig();
        return Ok(config);
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = "1.0.0"
        });
    }
}
