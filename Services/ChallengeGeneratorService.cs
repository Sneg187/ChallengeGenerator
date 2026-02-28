using ChallengeGenerator.Models;
using Dapper;
using MySql.Data.MySqlClient;

namespace ChallengeGenerator.Services;

public class ChallengeGeneratorService
{
    private readonly GeneratorConfig _config;
    private readonly string _connectionString;
    private readonly ILogger<ChallengeGeneratorService> _logger;
    private readonly Random _random = new();

    public ChallengeGeneratorService(
        IConfiguration configuration,
        ILogger<ChallengeGeneratorService> logger)
    {
        _config = configuration.GetSection("GeneratorConfig").Get<GeneratorConfig>()!;
        _connectionString = configuration.GetConnectionString("ChallengesDb")!;
        _logger = logger;
    }

    public async Task<GenerationResult> GenerateDailyChallenges()
    {
        var result = new GenerationResult
        {
            GeneratedAt = DateTime.UtcNow
        };

        try
        {
            _logger.LogInformation("[Generator] Starting daily challenge generation...");

            // Удаляем старые истёкшие челленджи
            await CleanupExpiredChallenges();

            var now = DateTime.UtcNow;
            var validStart = now;
            var validEnd = now.AddHours(_config.ChallengeDurationHours);

            // Генерируем обычные челленджи (is_premium = 0)
            var normalChallenges = GenerateChallengeSet(_config.NormalChallengesCount, 0, validStart, validEnd);
            result.NormalChallengesCreated = await SaveChallenges(normalChallenges);

            // Генерируем премиум челленджи (is_premium = 1)
            var premiumChallenges = GenerateChallengeSet(_config.PremiumChallengesCount, 1, validStart, validEnd);
            result.PremiumChallengesCreated = await SaveChallenges(premiumChallenges);

            result.Challenges.AddRange(normalChallenges);
            result.Challenges.AddRange(premiumChallenges);

            result.Success = true;
            result.Message = $"Successfully generated {result.NormalChallengesCreated} normal and {result.PremiumChallengesCreated} premium challenges";
            
            _logger.LogInformation("[Generator] {Message}", result.Message);
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Message = $"Error: {ex.Message}";
            _logger.LogError(ex, "[Generator] Failed to generate challenges");
        }

        return result;
    }

    private List<ChallengeTemplate> GenerateChallengeSet(int count, int isPremium, DateTime validStart, DateTime validEnd)
    {
        var challenges = new List<ChallengeTemplate>();
        var enabledEvents = _config.Events.Where(e => e.Value.Enabled).ToList();

        if (enabledEvents.Count == 0)
        {
            _logger.LogWarning("[Generator] No enabled events found in configuration");
            return challenges;
        }

        // Вычисляем общий вес
        var totalWeight = enabledEvents.Sum(e => e.Value.Weight);

        for (int i = 0; i < count; i++)
        {
            // Выбираем случайное событие на основе веса
            var selectedEvent = SelectWeightedEvent(enabledEvents, totalWeight);
            var eventConfig = selectedEvent.Value;
            var eventType = selectedEvent.Key;

            // Генерируем случайную цель
            var targetCount = _random.Next(eventConfig.TargetMin, eventConfig.TargetMax + 1);

            // Определяем сложность на основе цели
            var difficulty = DetermineDifficulty(targetCount, eventConfig.DifficultyThresholds);

            // Вычисляем награду
            var rewardValue = CalculateReward(eventType, targetCount, difficulty);

            // Выбираем случайное оружие (если есть)
            string? weapon = null;
            if (eventConfig.Weapons.Count > 0)
            {
                weapon = eventConfig.Weapons[_random.Next(eventConfig.Weapons.Count)];
            }

            // Выбираем случайную карту (если есть)
            string? map = null;
            if (eventConfig.Maps.Count > 0)
            {
                map = eventConfig.Maps[_random.Next(eventConfig.Maps.Count)];
            }

            // Решаем нужен ли headshot
            bool? headshot = null;
            if (eventType == "kill" && _random.NextDouble() < eventConfig.HeadshotChance)
            {
                headshot = true;
            }

            var challenge = new ChallengeTemplate
            {
                EventType = eventType,
                Weapon = weapon,
                Headshot = headshot,
                Map = map,
                TargetCount = targetCount,
                Difficulty = difficulty,
                Mode = _config.ServerMode,
                RewardType = "kcoins",
                RewardValue = rewardValue,
                IsPremium = isPremium,
                ValidDateStart = validStart,
                ValidDateEnd = validEnd,
                IsActive = true
            };

            challenges.Add(challenge);

            _logger.LogDebug("[Generator] Generated {Type} challenge: {Target} {Event}, Difficulty: {Diff}, Reward: {Reward} KCoins",
                isPremium == 0 ? "normal" : "premium", targetCount, eventType, difficulty, rewardValue);
        }

        return challenges;
    }

    private KeyValuePair<string, EventConfig> SelectWeightedEvent(List<KeyValuePair<string, EventConfig>> events, int totalWeight)
    {
        var randomValue = _random.Next(totalWeight);
        var cumulativeWeight = 0;

        foreach (var evt in events)
        {
            cumulativeWeight += evt.Value.Weight;
            if (randomValue < cumulativeWeight)
            {
                return evt;
            }
        }

        return events.Last();
    }

    private string DetermineDifficulty(int targetCount, Dictionary<string, ThresholdConfig> thresholds)
    {
        if (thresholds.TryGetValue("easy", out var easy))
        {
            if (easy.Max.HasValue && targetCount <= easy.Max.Value)
                return "easy";
        }

        if (thresholds.TryGetValue("medium", out var medium))
        {
            if (medium.Min.HasValue && medium.Max.HasValue &&
                targetCount >= medium.Min.Value && targetCount <= medium.Max.Value)
                return "medium";
        }

        if (thresholds.TryGetValue("hard", out var hard))
        {
            if (hard.Min.HasValue && targetCount >= hard.Min.Value)
                return "hard";
        }

        return "medium"; // По умолчанию
    }

    private int CalculateReward(string eventType, int targetCount, string difficulty)
    {
        if (!_config.Events.TryGetValue(eventType, out var eventConfig))
            return _config.MinReward;

        // Базовая награда = база за единицу × количество
        var baseReward = eventConfig.BaseRewardPerUnit * targetCount;

        // Множитель сложности
        var difficultyMultiplier = _config.DifficultyMultipliers.GetValueOrDefault(difficulty, 1.0);

        // Итоговая награда
        var reward = (int)(baseReward * difficultyMultiplier);

        // Применяем ограничения
        reward = Math.Max(_config.MinReward, reward);
        reward = Math.Min(_config.MaxRewardPerChallenge, reward);

        return reward;
    }

    private async Task<int> SaveChallenges(List<ChallengeTemplate> challenges)
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            INSERT INTO challenge_templates 
            (event_type, weapon, headshot, map, target_count, difficulty, mode, reward_type, reward_value, is_premium, valid_date_start, valid_date_end, is_active) 
            VALUES 
            (@EventType, @Weapon, @Headshot, @Map, @TargetCount, @Difficulty, @Mode, @RewardType, @RewardValue, @IsPremium, @ValidDateStart, @ValidDateEnd, @IsActive)";

        var count = 0;
        foreach (var challenge in challenges)
        {
            await connection.ExecuteAsync(query, challenge);
            count++;
        }

        return count;
    }

    private async Task CleanupExpiredChallenges()
    {
        using var connection = new MySqlConnection(_connectionString);
        await connection.OpenAsync();

        var query = @"
            UPDATE challenge_templates 
            SET is_active = 0 
            WHERE valid_date_end < @Now 
            AND is_active = 1";

        var affected = await connection.ExecuteAsync(query, new { Now = DateTime.UtcNow });

        if (affected > 0)
        {
            _logger.LogInformation("[Generator] Deactivated {Count} expired challenge templates", affected);
        }
    }

    public GeneratorConfig GetConfig()
    {
        return _config;
    }
}
