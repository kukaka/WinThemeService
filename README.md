# WinThemeService

Automatic Windows light/dark theme switcher with scheduling support.

## Features

- **Automatic Theme Switching** - Switch between light and dark themes based on a daily schedule
- **Task Scheduler Integration** - Uses Windows Task Scheduler to trigger theme changes even when the app is not running
- **Manual Toggle** - One-click manual theme switching
- **Settings Persistence** - Your preferences are saved to `%APPDATA%\WinThemeService\config.json`

## System Requirements

- Windows 11
- .NET 10.0 Runtime (included in the installer)

## Installation

1. Download `WinThemeService_Setup_1.0.0.exe` from the `installer/` folder
2. Run the installer and follow the prompts
3. Launch WinThemeService from the Start Menu or Desktop shortcut

## Usage

### Automatic Switching

1. Enable "Automatic Switching" toggle
2. Set your preferred times:
   - **Day Start (Light)** - When to switch to light theme (e.g., 07:00)
   - **Night Start (Dark)** - When to switch to dark theme (e.g., 19:00)
3. Click "Save Schedule"

The app creates Windows Task Scheduler tasks to trigger theme changes at your specified times.

### Manual Switching

Click the "Toggle" button to manually switch between light and dark themes.

## How It Works

WinThemeService modifies Windows registry keys to change the theme:

- `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize\AppsUseLightTheme`
- `HKCU\SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize\SystemUsesLightTheme`

Both keys are set simultaneously to ensure consistent system-wide theme changes.

A `WM_SETTINGCHANGE` broadcast is sent to all windows after registry modification, so applications like Chrome and Edge update immediately.

## Data Location

| Data | Location |
|------|----------|
| Config | `%APPDATA%\WinThemeService\config.json` |
| Logs | `%APPDATA%\WinThemeService\logs\` |

## Building from Source

```bash
# Build
dotnet build --configuration Release

# Run
dotnet run

# Publish as single-file executable
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
```

## Creating the Installer

The installer is built with [Inno Setup](https://jrsoftware.org/isdl.php).

### Prerequisites

- [Inno Setup 6](https://jrsoftware.org/isdl.php) installed

### Build Steps

1. Publish the application:
   ```bash
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish
   ```

2. Compile the installer:
   ```bash
   iscc setup.iss
   ```

3. The installer will be output to `installer/WinThemeService_Setup_1.0.0.exe`

### Installer Features

- Installs to `C:\Program Files\WinThemeService`
- Creates Start Menu shortcut
- Optional Desktop shortcut
- Includes uninstaller

### Customizing the Installer

Edit `setup.iss` to change:
- `MyAppVersion` - version number
- `OutputBaseFilename` - output filename
- `[Languages]` - installer language
- `[Tasks]` - optional components

## Tech Stack

- WPF with [WPF-UI](https://github.com/lepoco/wpfui) (Fluent Design)
- Serilog for logging
- [TaskScheduler](https://github.com/dahall/taskscheduler) library
- [Inno Setup](https://jrsoftware.org/isdl.php) for installer packaging
