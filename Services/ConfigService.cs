using System;
using System.IO;
using System.Text.Json;
using Serilog;
using WinThemeService.Models;

namespace WinThemeService.Services;

public class ConfigService
{
    private readonly string _configPath;
    private AppConfig _config;

    public AppConfig Config => _config;

    public ConfigService()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var serviceFolder = Path.Combine(appDataPath, "WinThemeService");

        if (!Directory.Exists(serviceFolder))
        {
            Directory.CreateDirectory(serviceFolder);
        }

        _configPath = Path.Combine(serviceFolder, "config.json");
        _config = Load();
    }

    private AppConfig Load()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var config = JsonSerializer.Deserialize<AppConfig>(json);
                if (config != null)
                {
                    Log.Information("Configuration loaded from {Path}", _configPath);
                    return config;
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load configuration from {Path}", _configPath);
        }

        Log.Information("Using default configuration");
        return new AppConfig();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, json);
            Log.Information("Configuration saved to {Path}", _configPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save configuration to {Path}", _configPath);
        }
    }

    public void Update(Action<AppConfig> updateAction)
    {
        updateAction(_config);
        Save();
    }
}
