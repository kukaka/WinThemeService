using System;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using Serilog;
using WinThemeService.Services;

namespace WinThemeService;

public partial class App : Application
{
    private ThemeService? _themeService;
    private ConfigService? _configService;
    private TaskSchedulerService? _taskSchedulerService;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        // Setup logging
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var logFolder = Path.Combine(appDataPath, "WinThemeService", "logs");

        if (!Directory.Exists(logFolder))
        {
            Directory.CreateDirectory(logFolder);
        }

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                Path.Combine(logFolder, "app_.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== Application Starting ===");
        Log.Information("Version: {Version}", typeof(App).Assembly.GetName().Version);

        // Initialize services
        _configService = new ConfigService();
        _themeService = new ThemeService();
        _taskSchedulerService = new TaskSchedulerService();

        // Handle command line arguments for task scheduler
        if (e.Args.Length > 0)
        {
            HandleCommandLineArgs(e.Args);
            Shutdown();
            return;
        }

        // Check and apply correct theme based on current time (handles missed scheduled tasks)
        CheckAndApplyThemeOnStartup();

        // Apply system theme to app chrome
        UpdateAppTheme();

        // Listen for system theme changes
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

        // Create and show main window
        var mainWindow = new MainWindow();
        mainWindow.Show();

        Log.Information("Application initialized successfully");
    }

    private void HandleCommandLineArgs(string[] args)
    {
        foreach (var arg in args)
        {
            switch (arg.ToLower())
            {
                case "--light":
                    Log.Information("Command line: switching to light theme");
                    _themeService?.SetTheme(AppTheme.Light);
                    break;
                case "--dark":
                    Log.Information("Command line: switching to dark theme");
                    _themeService?.SetTheme(AppTheme.Dark);
                    break;
                case "--autoswitch":
                    Log.Information("Command line: auto switch based on schedule");
                    CheckAndApplyThemeOnStartup();
                    break;
            }
        }
    }

    private void UpdateAppTheme()
    {
        if (_themeService == null) return;

        var systemTheme = _themeService.CurrentTheme;
        var wpfTheme = systemTheme == AppTheme.Light
            ? Wpf.Ui.Appearance.ApplicationTheme.Light
            : Wpf.Ui.Appearance.ApplicationTheme.Dark;

        Wpf.Ui.Appearance.ApplicationThemeManager.Apply(wpfTheme);
        Log.Information("App chrome theme set to {Theme}", wpfTheme);
    }

    private void CheckAndApplyThemeOnStartup()
    {
        if (_themeService == null || _configService == null) return;

        var config = _configService.Config;
        if (!config.AutoSwitch) return;

        var desiredTheme = GetDesiredThemeByTime(config.DayStart, config.DayEnd);
        var currentTheme = _themeService.CurrentTheme;

        if (desiredTheme != currentTheme)
        {
            Log.Information("Startup check: desired={Desired}, current={Current}, switching",
                desiredTheme, currentTheme);
            _themeService.SetTheme(desiredTheme);
        }
        else
        {
            Log.Information("Startup check: desired={Desired}, current={Current}, no switch needed",
                desiredTheme, currentTheme);
        }
    }

    private AppTheme GetDesiredThemeByTime(string dayStart, string dayEnd)
    {
        try
        {
            var now = DateTime.Now.TimeOfDay;
            var start = TimeSpan.Parse(dayStart);
            var end = TimeSpan.Parse(dayEnd);

            // If start < end (e.g., 08:00 to 19:00): light during [start, end), dark otherwise
            // If start > end (e.g., 19:00 to 08:00): light during [end, start), dark otherwise
            if (start < end)
            {
                return now >= start && now < end ? AppTheme.Light : AppTheme.Dark;
            }
            else
            {
                return now >= end || now < start ? AppTheme.Light : AppTheme.Dark;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to parse schedule times, defaulting to light");
            return AppTheme.Light;
        }
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General)
        {
            Log.Information("System preference changed, updating app theme");
            UpdateAppTheme();
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        Log.Information("=== Application Exiting ===");
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        Log.CloseAndFlush();
    }

    public ThemeService ThemeService => _themeService!;
    public ConfigService ConfigService => _configService!;
    public TaskSchedulerService TaskSchedulerService => _taskSchedulerService!;
}
