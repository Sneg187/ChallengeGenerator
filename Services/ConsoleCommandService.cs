using ChallengeGenerator.Models;
using System.Reflection;

namespace ChallengeGenerator.Services;

public class ConsoleCommandService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ConsoleCommandService> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public ConsoleCommandService(
        IServiceProvider serviceProvider,
        ILogger<ConsoleCommandService> logger,
        IHostApplicationLifetime lifetime)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Даём приложению запуститься
        await Task.Delay(2000, stoppingToken);

        Console.WriteLine();
        Console.WriteLine("===========================================");
        Console.WriteLine("  CS2 Challenge Generator - Console Ready");
        Console.WriteLine("  Type 'help' for available commands");
        Console.WriteLine("===========================================");
        Console.WriteLine();

        // Автоматическая проверка обновлений при старте
        await CheckForUpdates();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                Console.Write("> ");
                var input = await Task.Run(() => Console.ReadLine(), stoppingToken);

                if (string.IsNullOrWhiteSpace(input))
                    continue;

                var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                var command = parts[0].ToLower();
                var args = parts.Skip(1).ToArray();

                await ProcessCommand(command, args);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] {ex.Message}");
            }
        }
    }

    private async Task ProcessCommand(string command, string[] args)
    {
        switch (command)
        {
            case "help":
                ShowHelp();
                break;

            case "version":
                ShowVersion();
                break;

            case "check":
                await CheckForUpdates();
                break;

            case "generate":
                await GenerateChallenges();
                break;

            case "update":
                if (args.Length == 0)
                {
                    Console.WriteLine("[ERROR] Usage: update <version>");
                    Console.WriteLine("        Example: update v1.0.1");
                    Console.WriteLine("        Or: update latest");
                }
                else
                {
                    await UpdateApplication(args[0]);
                }
                break;

            case "status":
                await ShowStatus();
                break;

            case "config":
                ShowConfig();
                break;

            case "clear":
            case "cls":
                Console.Clear();
                break;

            default:
                Console.WriteLine($"[ERROR] Unknown command: {command}");
                Console.WriteLine("[INFO] Type 'help' for available commands");
                break;
        }
    }

    private void ShowHelp()
    {
        Console.WriteLine();
        Console.WriteLine("Available Commands:");
        Console.WriteLine("  help              - Show this help message");
        Console.WriteLine("  version           - Show current version");
        Console.WriteLine("  check             - Check for updates");
        Console.WriteLine("  update <version>  - Update to specified version (e.g., update v1.0.1)");
        Console.WriteLine("  update latest     - Update to the latest version");
        Console.WriteLine("  generate          - Manually generate challenges");
        Console.WriteLine("  status            - Show system status");
        Console.WriteLine("  config            - Show current configuration");
        Console.WriteLine("  clear / cls       - Clear console");
        Console.WriteLine();
        Console.WriteLine("Note: Use Pterodactyl panel to start/stop the server");
        Console.WriteLine();
    }

    private void ShowVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var informationalVersion = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? version?.ToString() ?? "Unknown";

        Console.WriteLine();
        Console.WriteLine($"Challenge Generator v{informationalVersion}");
        Console.WriteLine($".NET Version: {Environment.Version}");
        Console.WriteLine($"OS: {Environment.OSVersion}");
        Console.WriteLine();
    }

    private async Task CheckForUpdates()
    {
        Console.WriteLine("[INFO] Checking for updates...");

        try
        {
            var currentVersion = GetCurrentVersion();
            var latestVersion = await GetLatestVersionFromGitHub();

            if (latestVersion == null)
            {
                Console.WriteLine("[WARN] Could not reach GitHub API");
                return;
            }

            Console.WriteLine();
            Console.WriteLine($"Current version: {currentVersion}");
            Console.WriteLine($"Latest version:  {latestVersion}");

            if (CompareVersions(currentVersion, latestVersion) < 0)
            {
                Console.WriteLine();
                Console.WriteLine("[UPDATE AVAILABLE] New version is available!");
                Console.WriteLine($"[INFO] Run 'update {latestVersion}' or 'update latest' to upgrade");
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("[OK] You are running the latest version!");
            }

            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[WARN] Failed to check updates: {ex.Message}");
            _logger.LogDebug(ex, "Failed to check for updates");
        }
    }

    private string GetCurrentVersion()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        return $"v{version?.Major}.{version?.Minor}.{version?.Build}";
    }

    private async Task<string?> GetLatestVersionFromGitHub()
    {
        var githubUser = Environment.GetEnvironmentVariable("GITHUB_USER") ?? "YourUsername";
        var githubRepo = Environment.GetEnvironmentVariable("GITHUB_REPO") ?? "ChallengeGenerator";

        var url = $"https://api.github.com/repos/{githubUser}/{githubRepo}/releases/latest";

        try
        {
            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("User-Agent", "ChallengeGenerator");
            client.Timeout = TimeSpan.FromSeconds(10);

            var response = await client.GetStringAsync(url);
            var json = System.Text.Json.JsonDocument.Parse(response);

            if (json.RootElement.TryGetProperty("tag_name", out var tagName))
            {
                return tagName.GetString();
            }

            return null;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private int CompareVersions(string current, string latest)
    {
        var currentClean = current.TrimStart('v');
        var latestClean = latest.TrimStart('v');

        var currentParts = currentClean.Split('.').Select(int.Parse).ToArray();
        var latestParts = latestClean.Split('.').Select(int.Parse).ToArray();

        for (int i = 0; i < Math.Max(currentParts.Length, latestParts.Length); i++)
        {
            var currentPart = i < currentParts.Length ? currentParts[i] : 0;
            var latestPart = i < latestParts.Length ? latestParts[i] : 0;

            if (currentPart < latestPart) return -1;
            if (currentPart > latestPart) return 1;
        }

        return 0;
    }

    private async Task GenerateChallenges()
    {
        Console.WriteLine("[INFO] Generating challenges...");

        using var scope = _serviceProvider.CreateScope();
        var generator = scope.ServiceProvider.GetRequiredService<ChallengeGeneratorService>();

        var result = await generator.GenerateDailyChallenges();

        if (result.Success)
        {
            Console.WriteLine($"[SUCCESS] {result.Message}");
            Console.WriteLine($"          Normal: {result.NormalChallengesCreated}");
            Console.WriteLine($"          Premium: {result.PremiumChallengesCreated}");
        }
        else
        {
            Console.WriteLine($"[ERROR] {result.Message}");
        }

        Console.WriteLine();
    }

    private async Task UpdateApplication(string version)
    {
        if (version.ToLower() == "latest")
        {
            Console.WriteLine("[INFO] Fetching latest version...");
            var latestVersion = await GetLatestVersionFromGitHub();

            if (latestVersion == null)
            {
                Console.WriteLine("[ERROR] Failed to fetch latest version");
                return;
            }

            version = latestVersion;
            Console.WriteLine($"[INFO] Latest version: {version}");
        }

        Console.WriteLine($"[INFO] Updating to {version}...");

        var githubUser = Environment.GetEnvironmentVariable("GITHUB_USER") ?? "Sneg187";
        var githubRepo = Environment.GetEnvironmentVariable("GITHUB_REPO") ?? "ChallengeGenerator";

        var url = $"https://github.com/{githubUser}/{githubRepo}/releases/download/{version}/ChallengeGenerator-{version.TrimStart('v')}.zip";

        try
        {
            using var client = new HttpClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            Console.WriteLine("[INFO] Downloading from GitHub...");
            var zipBytes = await client.GetByteArrayAsync(url);

            var tempZip = Path.Combine(Path.GetTempPath(), "update.zip");
            await File.WriteAllBytesAsync(tempZip, zipBytes);

            Console.WriteLine("[INFO] Extracting files...");

            // Сохранить database.json ПЕРЕД обновлением
            var dbJsonPath = "/home/container/database.json";
            string? dbJsonBackup = null;
            if (File.Exists(dbJsonPath))
            {
                dbJsonBackup = File.ReadAllText(dbJsonPath);
                Console.WriteLine("[INFO] Backed up database.json");
            }

            // Создать backup папку для DLL
            var backupDir = Path.Combine(AppContext.BaseDirectory, "backup");
            if (Directory.Exists(backupDir))
                Directory.Delete(backupDir, true);

            Directory.CreateDirectory(backupDir);
            foreach (var file in Directory.GetFiles(AppContext.BaseDirectory, "*.dll"))
            {
                var fileName = Path.GetFileName(file);
                File.Copy(file, Path.Combine(backupDir, fileName), true);
            }

            // Распаковать обновление
            System.IO.Compression.ZipFile.ExtractToDirectory(tempZip, AppContext.BaseDirectory, true);

            // Восстановить database.json ПОСЛЕ обновления
            if (dbJsonBackup != null)
            {
                File.WriteAllText(dbJsonPath, dbJsonBackup);
                Console.WriteLine("[INFO] Restored database.json");
            }

            File.Delete(tempZip);

            Console.WriteLine("[SUCCESS] Update completed successfully!");
            Console.WriteLine("[INFO] database.json was preserved");
            Console.WriteLine("[INFO] Restarting application in 3 seconds...");

            await Task.Delay(3000);

            _lifetime.StopApplication();
            Environment.Exit(0);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[ERROR] Download failed: {ex.Message}");
            Console.WriteLine($"[INFO] Make sure release {version} exists on GitHub");
            _logger.LogError(ex, "Update download failed");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERROR] Update failed: {ex.Message}");
            _logger.LogError(ex, "Update failed");
        }
    }

    private async Task ShowStatus()
    {
        Console.WriteLine();
        Console.WriteLine("System Status:");

        using var scope = _serviceProvider.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<ConfigService>();
        var dbConfig = configService.LoadOrCreateDatabaseConfig();

        try
        {
            // БЕЗ SslMode=none!
            using var connection = new MySql.Data.MySqlClient.MySqlConnection(
                $"Server={dbConfig.Host};Port={dbConfig.Port};Database={dbConfig.ChallengesDatabase};User={dbConfig.User};Password={dbConfig.Password};");

            await connection.OpenAsync();
            Console.WriteLine($"  [OK] Database: Connected ({dbConfig.Host}:{dbConfig.Port})");
            connection.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [ERROR] Database: Connection failed - {ex.Message}");
        }

        Console.WriteLine($"  [OK] Hangfire: Running");
        Console.WriteLine($"  [OK] API: Running");
        Console.WriteLine($"  [OK] Config: database.json loaded");

        var port = Environment.GetEnvironmentVariable("SERVER_PORT") ?? "5000";
        Console.WriteLine($"  [OK] Port: {port}");
        Console.WriteLine();
    }

    private void ShowConfig()
    {
        using var scope = _serviceProvider.CreateScope();
        var configService = scope.ServiceProvider.GetRequiredService<ConfigService>();
        var dbConfig = configService.LoadOrCreateDatabaseConfig();

        Console.WriteLine();
        Console.WriteLine("Current Configuration:");
        Console.WriteLine($"  Database Host: {dbConfig.Host}:{dbConfig.Port}");
        Console.WriteLine($"  Challenges DB: {dbConfig.ChallengesDatabase}");
        Console.WriteLine($"  Privileges DB: {dbConfig.PrivilegesDatabase}");
        Console.WriteLine($"  User: {dbConfig.User}");
        Console.WriteLine($"  Password: {new string('*', Math.Min(dbConfig.Password.Length, 8))}");
        Console.WriteLine();
    }
}