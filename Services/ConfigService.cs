using System.Text.Json;
using ChallengeGenerator.Models;

namespace ChallengeGenerator.Services;

public class ConfigService
{
    private const string ConfigFileName = "database.json";
    private readonly ILogger<ConfigService> _logger;

    public ConfigService(ILogger<ConfigService> logger)
    {
        _logger = logger;
    }

    public DatabaseConfig LoadOrCreateDatabaseConfig()
    {
        if (!File.Exists(ConfigFileName))
        {
            _logger.LogWarning("[Config] database.json not found. Creating default config...");

            var defaultConfig = new DatabaseConfig();
            SaveDatabaseConfig(defaultConfig);

            _logger.LogWarning("[Config] Please edit database.json with your MySQL credentials and restart the application.");

            return defaultConfig;
        }

        try
        {
            var json = File.ReadAllText(ConfigFileName);
            var config = JsonSerializer.Deserialize<DatabaseConfig>(json);

            if (config == null)
            {
                _logger.LogError("[Config] Failed to deserialize database.json");
                return new DatabaseConfig();
            }

            _logger.LogInformation("[Config] Database configuration loaded successfully");
            return config;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Config] Error loading database.json");
            return new DatabaseConfig();
        }
    }

    public void SaveDatabaseConfig(DatabaseConfig config)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(ConfigFileName, json);

            _logger.LogInformation("[Config] database.json saved successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Config] Error saving database.json");
        }
    }
}