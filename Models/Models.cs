namespace ChallengeGenerator.Models;

public class EventConfig
{
    public bool Enabled { get; set; }
    public int Weight { get; set; }
    public int TargetMin { get; set; }
    public int TargetMax { get; set; }
    public List<string?> Weapons { get; set; } = new();
    public List<string?> Maps { get; set; } = new();
    public double HeadshotChance { get; set; }
    public Dictionary<string, ThresholdConfig> DifficultyThresholds { get; set; } = new();
    public int BaseRewardPerUnit { get; set; }
}

public class ThresholdConfig
{
    public int? Min { get; set; }
    public int? Max { get; set; }
}

public class GeneratorConfig
{
    public string GenerationTimeUtc { get; set; } = "00:00";
    public int ChallengeDurationHours { get; set; } = 24;
    public int NormalChallengesCount { get; set; } = 5;
    public int PremiumChallengesCount { get; set; } = 5;
    public string ServerMode { get; set; } = "dm";
    public int KCoinRate { get; set; } = 10000;

    // Режим наград
    public bool UseFixedRewards { get; set; } = false;  // false = динамический, true = фиксированный
    public int FixedRewardNormal { get; set; } = 100;   // Фиксированная награда для normal
    public int FixedRewardPremium { get; set; } = 200;  // Фиксированная награда для premium

    // Динамический режим
    public Dictionary<string, EventConfig> Events { get; set; } = new();
    public Dictionary<string, double> DifficultyMultipliers { get; set; } = new();
    public int MinReward { get; set; } = 3000;
    public int MaxRewardPerChallenge { get; set; } = 80000;
}

public class ChallengeTemplate
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Weapon { get; set; }
    public bool? Headshot { get; set; }
    public string? Map { get; set; }
    public int TargetCount { get; set; }
    public string Difficulty { get; set; } = string.Empty;
    public string Mode { get; set; } = string.Empty;
    public string RewardType { get; set; } = "kcoins";
    public int RewardValue { get; set; }
    public int IsPremium { get; set; }
    public DateTime ValidDateStart { get; set; }
    public DateTime ValidDateEnd { get; set; }
    public bool IsActive { get; set; } = true;
}

public class GenerationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int NormalChallengesCreated { get; set; }
    public int PremiumChallengesCreated { get; set; }
    public DateTime GeneratedAt { get; set; }
    public List<ChallengeTemplate> Challenges { get; set; } = new();
}