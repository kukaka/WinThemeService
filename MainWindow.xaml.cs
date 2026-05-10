using System;
using System.Windows;
using System.Windows.Controls;
using Serilog;
using WinThemeService.Services;
using WinThemeService.Models;
using Wpf.Ui.Controls;

namespace WinThemeService;

public partial class MainWindow : FluentWindow
{
    private readonly ThemeService _themeService;
    private readonly ConfigService _configService;
    private readonly TaskSchedulerService _taskSchedulerService;
    private bool _isInitializing = true;

    public MainWindow()
    {
        InitializeComponent();

        var app = (App)Application.Current;
        _themeService = app.ThemeService;
        _configService = app.ConfigService;
        _taskSchedulerService = app.TaskSchedulerService;

        // Subscribe to theme changes
        _themeService.ThemeChanged += OnThemeChanged;

        // Load current state
        LoadCurrentState();

        // Set version
        VersionText.Text = typeof(MainWindow).Assembly.GetName().Version?.ToString() ?? "1.0.0";

        _isInitializing = false;
        Log.Information("MainWindow initialized");
    }

    private void LoadCurrentState()
    {
        // Load theme state
        var currentTheme = _themeService.CurrentTheme;
        UpdateThemeDisplay(currentTheme);

        // Load config
        var config = _configService.Config;
        DayStartInput.Text = config.DayStart;
        DayEndInput.Text = config.DayEnd;
        AutoSwitchToggle.IsChecked = config.AutoSwitch;

        // Update schedule card visibility
        ScheduleCard.Opacity = config.AutoSwitch ? 1.0 : 0.5;
    }

    private void UpdateThemeDisplay(AppTheme theme)
    {
        CurrentThemeText.Text = theme == AppTheme.Light ? "Light" : "Dark";

        // Update toggle button appearance based on current theme
        var icon = theme == AppTheme.Light
            ? new SymbolIcon { Symbol = SymbolRegular.WeatherSunny24 }
            : new SymbolIcon { Symbol = SymbolRegular.WeatherMoon24 };

        ToggleThemeButton.Icon = icon;
    }

    private void OnThemeChanged(object? sender, AppTheme theme)
    {
        Dispatcher.Invoke(() => UpdateThemeDisplay(theme));
    }

    private async void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        var newTheme = _themeService.CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light;

        // Disable button during switch to prevent multiple clicks
        ToggleThemeButton.IsEnabled = false;

        try
        {
            await System.Threading.Tasks.Task.Run(() => _themeService.SetTheme(newTheme));
            Log.Information("Manual theme toggle to {Theme}", newTheme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to toggle theme");
        }
        finally
        {
            ToggleThemeButton.IsEnabled = true;
        }
    }

    private void OnAutoSwitchChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;

        var enabled = AutoSwitchToggle.IsChecked == true;
        ScheduleCard.Opacity = enabled ? 1.0 : 0.5;

        _configService.Update(c => c.AutoSwitch = enabled);

        if (enabled)
        {
            try
            {
                _taskSchedulerService.SetupScheduledTasks(
                    _configService.Config.DayStart,
                    _configService.Config.DayEnd);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to setup scheduled tasks");
                System.Windows.MessageBox.Show(
                    $"Failed to setup scheduled tasks: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        else
        {
            _taskSchedulerService.RemoveAllTasks();
        }

        Log.Information("Auto switch {Status}", enabled ? "enabled" : "disabled");
    }

    private void OnScheduleInputChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing) return;

        // Validate time format
        var dayStart = DayStartInput.Text;
        var dayEnd = DayEndInput.Text;

        var isValid = IsValidTime(dayStart) && IsValidTime(dayEnd);
        SaveScheduleButton.IsEnabled = isValid;
    }

    private bool IsValidTime(string time)
    {
        if (string.IsNullOrWhiteSpace(time)) return false;

        var parts = time.Split(':');
        if (parts.Length != 2) return false;

        return int.TryParse(parts[0], out var hours) &&
               int.TryParse(parts[1], out var minutes) &&
               hours >= 0 && hours <= 23 &&
               minutes >= 0 && minutes <= 59;
    }

    private void OnSaveScheduleClick(object sender, RoutedEventArgs e)
    {
        var dayStart = DayStartInput.Text;
        var dayEnd = DayEndInput.Text;

        _configService.Update(c =>
        {
            c.DayStart = dayStart;
            c.DayEnd = dayEnd;
        });

        if (_configService.Config.AutoSwitch)
        {
            try
            {
                _taskSchedulerService.SetupScheduledTasks(dayStart, dayEnd);
                System.Windows.MessageBox.Show(
                    "Schedule saved and Task Scheduler tasks updated.",
                    "Success",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to update scheduled tasks");
                System.Windows.MessageBox.Show(
                    $"Failed to update scheduled tasks: {ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
        else
        {
            System.Windows.MessageBox.Show(
                "Schedule saved.",
                "Success",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        SaveScheduleButton.IsEnabled = false;
        Log.Information("Schedule saved: DayStart={DayStart}, DayEnd={DayEnd}", dayStart, dayEnd);
    }
}
