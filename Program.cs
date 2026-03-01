using ChallengeGenerator.Services;
using Hangfire.Dashboard;
using Hangfire;
using Hangfire.MySql;
using System.Transactions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<ChallengeGeneratorService>();
builder.Services.AddHostedService<ConsoleCommandService>();

// Build temporary service provider to load config
var tempServiceProvider = builder.Services.BuildServiceProvider();
var configService = tempServiceProvider.GetRequiredService<ConfigService>();
var dbConfig = configService.LoadOrCreateDatabaseConfig();

// Build connection strings
var challengesDbConnection = $"Server={dbConfig.Host};Port={dbConfig.Port};Database={dbConfig.ChallengesDatabase};User={dbConfig.User};Password={dbConfig.Password};SslMode=none;";
var privilegesDbConnection = $"Server={dbConfig.Host};Port={dbConfig.Port};Database={dbConfig.PrivilegesDatabase};User={dbConfig.User};Password={dbConfig.Password};SslMode=none;";

// Override configuration
builder.Configuration["ConnectionStrings:ChallengesDb"] = challengesDbConnection;
builder.Configuration["ConnectionStrings:PrivilegesDb"] = privilegesDbConnection;

// Configure Hangfire (аег MySqlStorageOptions!)
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseStorage(new MySqlStorage(
    challengesDbConnection,
    new MySqlStorageOptions
    {
        TransactionIsolationLevel = IsolationLevel.ReadCommitted,
        QueuePollInterval = TimeSpan.FromSeconds(15),
        JobExpirationCheckInterval = TimeSpan.FromHours(1),
        CountersAggregateInterval = TimeSpan.FromMinutes(5),
        PrepareSchemaIfNecessary = true,
        DashboardJobListLimit = 50000,
        TransactionTimeout = TimeSpan.FromMinutes(1),
    })));

builder.Services.AddHangfireServer();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Configure Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

// Schedule daily challenge generation
var generationTime = builder.Configuration.GetValue<string>("GeneratorConfig:GenerationTimeUtc") ?? "00:00";
var timeParts = generationTime.Split(':');
var hour = int.Parse(timeParts[0]);
var minute = int.Parse(timeParts[1]);

RecurringJob.AddOrUpdate<ChallengeGeneratorService>(
    "generate-daily-challenges",
    service => service.GenerateDailyChallenges(),
    Cron.Daily(hour, minute),
    new RecurringJobOptions
    {
        TimeZone = TimeZoneInfo.Utc
    });

app.Logger.LogInformation("Challenge Generator started. Daily generation scheduled at {Time} UTC", generationTime);
app.Logger.LogInformation("Hangfire Dashboard available at /hangfire");
app.Logger.LogInformation("API Documentation available at /swagger");

app.Run();

// Simple authorization filter for Hangfire
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}