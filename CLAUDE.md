# CLAUDE.md

## Project Overview

**Project Name:** WinThemeService
**Type:** Windows Desktop Application (WPF/C#)
**Goal:** Automatic light/dark theme switcher with native Windows UI and system theme following.

## User Requirements

1. **Core Functionality**
   - Automatic theme switching based on time schedules
   - Uses Windows Task Scheduler to trigger theme changes even when app is not running
   - Manual theme switching (one-click light/dark toggle)
   - Settings persistence

2. **UI Requirements**
   - Native Windows 11 UI style (Fluent Design)
   - Dark mode support with automatic system theme following
   - Clean, modern interface

## Technical Architecture

### Project Structure

```
WinThemeService/
├── App.xaml                      # Application entry, theme resources
├── App.xaml.cs                   # Startup logic, logging setup
├── MainWindow.xaml               # Main UI with FluentWindow
├── MainWindow.xaml.cs            # UI event handlers
├── Services/
│   ├── ThemeService.cs           # Registry operations + broadcast
│   ├── ConfigService.cs          # JSON config persistence
│   └── TaskSchedulerService.cs   # Task Scheduler integration
├── Models/
│   └── AppConfig.cs              # Config data model
├── Assets/
│   ├── app.ico                   # Application icon
│   └── icon.png                  # PNG version of icon
└── WinThemeService.csproj        # Project file with dependencies
```

### Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| WPF-UI | 4.3.0 | Fluent Design UI components |
| Serilog | 4.3.1 | Structured logging |
| Serilog.Sinks.File | 7.0.0 | File logging sink |
| TaskScheduler | 2.12.2 | Windows Task Scheduler API |

### Components

| Component | Responsibility |
|-----------|----------------|
| `ThemeService` | Read/write registry, broadcast theme change messages |
| `ConfigService` | JSON config persistence to `%APPDATA%\WinThemeService\config.json` |
| `TaskSchedulerService` | Create/delete scheduled tasks in Windows Task Scheduler |
| `MainWindow` | UI interaction, event handling |

## Theme Switching Implementation

### Registry Keys

**Path:** `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize`

| Key | Purpose | Values |
|-----|---------|--------|
| `AppsUseLightTheme` | Controls application mode | 1=Light, 0=Dark |
| `SystemUsesLightTheme` | Controls Windows mode (taskbar, Start menu, etc.) | 1=Light, 0=Dark |

**IMPORTANT:** Both keys must be set simultaneously. Setting only `AppsUseLightTheme` will only change app mode, not Windows mode.

### Broadcast Messages

To notify all applications of theme change, send these messages via `SendMessageTimeoutW`:

```csharp
// WM_SETTINGCHANGE with lParam="ImmersiveColorSet"
SendMessageTimeoutW(hwnd, 0x001A, IntPtr.Zero, "ImmersiveColorSet", ...);

// WM_THEMECHANGED
SendMessageTimeoutW(hwnd, 0x031A, IntPtr.Zero, IntPtr.Zero, ...);
```

Broadcast to all top-level windows using `EnumWindows` to ensure Chromium-based browsers (Chrome, Edge) receive the notification.

## Task Scheduler Tasks

Tasks are created under `TaskFolderName = "WinThemeService"`:

| Task Name | Trigger | Action |
|-----------|---------|--------|
| `WinThemeSwitcher_Light` | Daily at `day_start` | `WinThemeService.exe --light` |
| `WinThemeSwitcher_Dark` | Daily at `day_end` | `WinThemeService.exe --dark` |
| `WinThemeSwitcher_Startup` | System startup (1 min delay) | `WinThemeService.exe --autoswitch` |

The app checks `e.Args` at startup. If arguments are present (`--light`, `--dark`, or `--autoswitch`), it performs the theme switch and exits immediately without showing the UI.

### Startup Auto-Switch

The `--autoswitch` task runs on system startup to handle cases where the scheduled task was missed (e.g., computer was off at the scheduled switch time). When triggered:
1. Reads current time and compares against `day_start` and `day_end`
2. Determines the correct theme for the current time
3. Checks the actual system theme from registry
4. If they differ, performs the switch

## Config File

**Location:** `%APPDATA%\WinThemeService\config.json`

```json
{
  "day_start": "07:00",
  "day_end": "19:00",
  "auto_switch": true
}
```

## Logging

**Location:** `%APPDATA%\WinThemeService\logs\app_YYYYMMDD.log`

Serilog is configured with:
- Rolling file (daily interval)
- 7-day retention
- Debug-level enabled
- Template: `{Timestamp} [{Level}] {Message}{NewLine}{Exception}`

## UI Implementation

### Window Type
Uses `Wpf.Ui.Controls.FluentWindow` from WPF-UI library for native Windows 11 Fluent Design.

### Key UI Components
- `ui:Card` - Card containers for visual grouping
- `ui:ToggleSwitch` - For enabling/disabling auto-switch
- `ui:TextBox` - For time input
- `ui:Button` - Action buttons
- `ui:TitleBar` - Custom title bar with icon

### Theme Following
Application follows system theme automatically via `SystemEvents.UserPreferenceChanged` event. When the user changes Windows theme in system settings, the app updates its chrome to match.

## Run Commands

```bash
# Build the project
dotnet build

# Run the application (shows UI)
dotnet run

# Run with CLI argument (for Task Scheduler, no UI)
dotnet run -- --light
dotnet run -- --dark
dotnet run -- --autoswitch
```

## Environment

- **.NET SDK:** 10.0
- **Platform:** Windows 11
- **Language:** C# with XAML

---

## Lessons Learned (WPF Development)

### Theme Switching

1. **Both registry keys are required**
   - `AppsUseLightTheme` AND `SystemUsesLightTheme` must both be set
   - Setting only one results in inconsistent system behavior

2. **Broadcast to all windows**
   - Use `EnumWindows` + `SendMessageTimeoutW` to broadcast to all top-level windows
   - Without full broadcast, some apps won't detect the change

### UI Responsiveness

1. **Long-running operations must be async**
   - Theme switching and Task Scheduler operations can block UI
   - Use `async/await` with `Task.Run()` for blocking operations
   - Disable buttons during async operations to prevent multiple clicks

2. **WPF-UI components need specific handling**
   - `ui:StackPanel` does not exist in WPF-UI namespace
   - Use standard WPF `StackPanel` with `Margin` for spacing
   - `SymbolIcon` requires `SymbolRegular.` prefix for icon names

### Application Startup

1. **Manually show MainWindow**
   - When using custom `OnStartup` handler, `MainWindow` is not auto-created
   - Must explicitly create and show: `var mainWindow = new MainWindow(); mainWindow.Show();`

2. **Icon loading in TitleBar**
   - Using `ui:ImageIcon` with `pack://application:,,,/Assets/icon.png` works
   - Using direct ICO file in `Image.Source` may cause startup crashes
   - Always test icon changes before committing

### Task Scheduler

1. **Task folder isolation**
   - Create tasks under a dedicated folder (`WinThemeService\`) to avoid conflicts
   - Use `TaskLogonType.InteractiveToken` for current user execution

2. **CLI argument parsing**
   - Check `e.Args` at startup to detect scheduled invocations
   - Exit immediately after performing the action when args are present

3. **Startup auto-switch for missed tasks**
   - Use `BootTrigger` with a 1-minute delay to ensure system is ready
   - The `--autoswitch` argument compares current time against schedule to determine correct theme
   - Handles cases where computer was off at scheduled switch time

### Logging

1. **Serilog file sink**
   - Use `rollingInterval: RollingInterval.Day` for automatic daily log rotation
   - `retainedFileCountLimit: 7` prevents unbounded disk usage

2. **Log location**
   - Place logs in `%APPDATA%\WinThemeService\logs\` for user accessibility
   - Always create directory before configuring Serilog

### Error Handling

1. **Ambiguous type references**
   - WPF-UI may shadow standard WPF types (e.g., `MessageBoxButton`)
   - Use fully qualified names: `System.Windows.MessageBoxButton`

2. **Missing using directives**
   - `UserPreferenceChangedEventArgs` requires `Microsoft.Win32` namespace
   - Always verify namespaces when encountering "type not found" errors
