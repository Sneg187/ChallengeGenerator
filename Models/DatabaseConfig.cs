namespace ChallengeGenerator.Models;

public class DatabaseConfig
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3306;
    public string ChallengesDatabase { get; set; } = "challenge_db";
    public string PrivilegesDatabase { get; set; } = "privileges_db";
    public string User { get; set; } = "root";
    public string Password { get; set; } = "";
}