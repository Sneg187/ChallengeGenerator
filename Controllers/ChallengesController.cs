using ChallengeGenerator.Models;
using ChallengeGenerator.Services;
using Microsoft.AspNetCore.Mvc;
using System.Reflection;

namespace ChallengeGenerator.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ChallengesController : ControllerBase
{
    private readonly ChallengeGeneratorService _generator;
    private readonly ConfigService _configService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<ChallengesController> _logger;

    public ChallengesController(
        ChallengeGeneratorService generator,
        ConfigService configService,
        IConfiguration configuration,
        ILogger<ChallengesController> logger)
    {
        _generator = generator;
        _configService = configService;
        _configuration = configuration;
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
    /// Get database configuration from database.json
    /// </summary>
    [HttpGet("database-config")]
    public IActionResult GetDatabaseConfig()
    {
        try
        {
            var config = _configService.LoadOrCreateDatabaseConfig();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load database config");
            return StatusCode(500, new { error = "Failed to load database configuration" });
        }
    }

    /// <summary>
    /// Update database configuration in database.json
    /// </summary>
    [HttpPut("database-config")]
    public IActionResult UpdateDatabaseConfig([FromBody] DatabaseConfig config)
    {
        try
        {
            _configService.SaveDatabaseConfig(config);
            _logger.LogInformation("[API] Database configuration updated");
            return Ok(new
            {
                message = "Database configuration updated successfully. Please restart the server for changes to take effect.",
                success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save database config");
            return StatusCode(500, new { error = "Failed to save database configuration" });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionString = $"v{version?.Major}.{version?.Minor}.{version?.Build}";

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            version = versionString  // ← Автоматически из .csproj
        });
    }

    /// <summary>
    /// Get generator settings from appsettings.json
    /// </summary>
    [HttpGet("generator-settings")]
    public IActionResult GetGeneratorSettings()
    {
        try
        {
            var settings = new
            {
                GenerationTimeUtc = _configuration.GetValue<string>("GeneratorConfig:GenerationTimeUtc"),
                ChallengeDurationHours = _configuration.GetValue<int>("GeneratorConfig:ChallengeDurationHours"),
                NormalChallengesCount = _configuration.GetValue<int>("GeneratorConfig:NormalChallengesCount"),
                PremiumChallengesCount = _configuration.GetValue<int>("GeneratorConfig:PremiumChallengesCount"),
                ServerMode = _configuration.GetValue<string>("GeneratorConfig:ServerMode"),
                MinReward = _configuration.GetValue<int>("GeneratorConfig:MinReward"),
                MaxRewardPerChallenge = _configuration.GetValue<int>("GeneratorConfig:MaxRewardPerChallenge")
            };
            return Ok(settings);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get generator settings");
            return StatusCode(500, new { error = "Failed to get generator settings" });
        }
    }
}