using System.Text.Json;

namespace TamponiInventario.Wpf.Config;

public sealed record LocalConfig(string AdminPassword, string SecretKey)
{
    public static LocalConfig Load(string? path = null)
    {
        var resolvedPath = path
            ?? Environment.GetEnvironmentVariable("APPSETTINGS_PATH")
            ?? Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");

        if (!File.Exists(resolvedPath))
        {
            throw new FileNotFoundException(
                "Config locale mancante. Crea appsettings.local.json con admin_password e flask_secret_key.",
                resolvedPath);
        }

        var json = File.ReadAllText(resolvedPath);
        var document = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
            ?? throw new InvalidOperationException("Config locale non valida: atteso oggetto JSON.");

        var adminPassword = document.TryGetValue("admin_password", out var adminValue)
            ? adminValue?.Trim() ?? string.Empty
            : string.Empty;
        var secretKey = document.TryGetValue("flask_secret_key", out var secretValue)
            ? secretValue?.Trim() ?? string.Empty
            : string.Empty;

        if (string.IsNullOrWhiteSpace(adminPassword))
        {
            throw new InvalidOperationException("admin_password non configurata in appsettings.local.json.");
        }

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException("flask_secret_key non configurata in appsettings.local.json.");
        }

        return new LocalConfig(adminPassword, secretKey);
    }
}
