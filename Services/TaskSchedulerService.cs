using System;
using System.IO;
using Microsoft.Win32.TaskScheduler;
using Serilog;

namespace WinThemeService.Services;

public class TaskSchedulerService
{
    private const string TaskFolderName = "WinThemeService";
    private const string LightTaskName = "WinThemeSwitcher_Light";
    private const string DarkTaskName = "WinThemeSwitcher_Dark";
    private const string StartupTaskName = "WinThemeSwitcher_Startup";

    private readonly string _exePath;

    public TaskSchedulerService()
    {
        _exePath = Environment.ProcessPath ?? Path.Combine(AppContext.BaseDirectory, "WinThemeService.exe");
    }

    public void SetupScheduledTasks(string dayStart, string dayEnd)
    {
        try
        {
            using var ts = new TaskService();

            // Create or get the task folder
            var folder = ts.RootFolder.SubFolders.Exists(TaskFolderName)
                ? ts.GetFolder(TaskFolderName)
                : ts.RootFolder.CreateFolder(TaskFolderName);

            // Remove existing tasks
            RemoveTask(ts, folder, LightTaskName);
            RemoveTask(ts, folder, DarkTaskName);
            RemoveTask(ts, folder, StartupTaskName);

            // Create light task (runs at day start - switches to light)
            CreateTimeTriggeredTask(ts, folder, LightTaskName, dayStart, "--light");

            // Create dark task (runs at day end - switches to dark)
            CreateTimeTriggeredTask(ts, folder, DarkTaskName, dayEnd, "--dark");

            // Create startup task (runs on system startup - auto switches based on time)
            CreateStartupTriggeredTask(ts, folder, StartupTaskName, "--autoswitch");

            Log.Information("Scheduled tasks created: Light at {DayStart}, Dark at {DayEnd}, Startup check enabled", dayStart, dayEnd);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to setup scheduled tasks");
            throw;
        }
    }

    private void CreateTimeTriggeredTask(TaskService ts, TaskFolder folder, string taskName, string time, string argument)
    {
        var td = ts.NewTask();
        td.RegistrationInfo.Description = $"WinThemeService - {taskName}";
        td.Principal.LogonType = TaskLogonType.InteractiveToken;
        td.Settings.StartWhenAvailable = true;
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;

        // Parse time
        var parts = time.Split(':');
        if (parts.Length != 2 || !int.TryParse(parts[0], out var hours) || !int.TryParse(parts[1], out var minutes))
        {
            throw new ArgumentException($"Invalid time format: {time}");
        }

        // Create daily trigger
        var trigger = new DailyTrigger
        {
            StartBoundary = DateTime.Today.AddHours(hours).AddMinutes(minutes),
            DaysInterval = 1
        };
        td.Triggers.Add(trigger);

        // Action: run the executable with argument
        td.Actions.Add(new ExecAction(_exePath, argument));

        folder.RegisterTaskDefinition(taskName, td);
    }

    private void CreateStartupTriggeredTask(TaskService ts, TaskFolder folder, string taskName, string argument)
    {
        var td = ts.NewTask();
        td.RegistrationInfo.Description = "WinThemeService - Auto switch on startup";
        td.Principal.LogonType = TaskLogonType.InteractiveToken;
        td.Settings.StartWhenAvailable = true;
        td.Settings.DisallowStartIfOnBatteries = false;
        td.Settings.StopIfGoingOnBatteries = false;

        // Create boot trigger (runs at system startup)
        var trigger = new BootTrigger
        {
            Delay = TimeSpan.FromMinutes(1) // Delay by 1 minute to ensure system is ready
        };
        td.Triggers.Add(trigger);

        // Action: run the executable with argument
        td.Actions.Add(new ExecAction(_exePath, argument));

        folder.RegisterTaskDefinition(taskName, td);
        Log.Information("Startup trigger task created: {TaskName}", taskName);
    }

    private void RemoveTask(TaskService ts, TaskFolder folder, string taskName)
    {
        try
        {
            if (folder.Tasks.Exists(taskName))
            {
                folder.DeleteTask(taskName);
                Log.Information("Removed existing task: {TaskName}", taskName);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to remove existing task: {TaskName}", taskName);
        }
    }

    public void RemoveAllTasks()
    {
        try
        {
            using var ts = new TaskService();
            if (ts.RootFolder.SubFolders.Exists(TaskFolderName))
            {
                ts.RootFolder.DeleteFolder(TaskFolderName, false);
                Log.Information("Removed all scheduled tasks");
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to remove scheduled tasks");
        }
    }
}
