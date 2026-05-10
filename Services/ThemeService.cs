using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;
using Serilog;

namespace WinThemeService.Services;

public enum AppTheme
{
    Light,
    Dark
}

public class ThemeService
{
    private const string RegistryPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeKey = "AppsUseLightTheme";
    private const string SystemUsesLightThemeKey = "SystemUsesLightTheme";

    public event EventHandler<AppTheme>? ThemeChanged;

    public AppTheme CurrentTheme => GetSystemTheme();

    public AppTheme GetSystemTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
            var value = key?.GetValue(AppsUseLightThemeKey);
            if (value is int intValue)
            {
                return intValue == 1 ? AppTheme.Light : AppTheme.Dark;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to read system theme from registry");
        }

        return AppTheme.Light;
    }

    public void SetTheme(AppTheme theme)
    {
        try
        {
            var themeValue = theme == AppTheme.Light ? 1 : 0;

            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            key?.SetValue(AppsUseLightThemeKey, themeValue, RegistryValueKind.DWord);
            key?.SetValue(SystemUsesLightThemeKey, themeValue, RegistryValueKind.DWord);

            BroadcastThemeChange();

            Log.Information("Theme changed to {Theme}", theme);
            ThemeChanged?.Invoke(this, theme);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to set theme to {Theme}", theme);
        }
    }

    private void BroadcastThemeChange()
    {
        const int WM_SETTINGCHANGE = 0x001A;
        const int WM_THEMECHANGED = 0x031A;
        const uint SMTO_ABORTIFHUNG = 0x0002;

        var result = IntPtr.Zero;

        // Get the foreground window handle
        var hwnd = GetForegroundWindow();

        // Send WM_SETTINGCHANGE with ImmersiveColorSet
        SendMessageTimeoutW(hwnd, WM_SETTINGCHANGE, IntPtr.Zero,
            "ImmersiveColorSet", SMTO_ABORTIFHUNG, 5000, out result);

        // Send WM_THEMECHANGED to all windows
        SendMessageTimeoutW(hwnd, WM_THEMECHANGED, IntPtr.Zero,
            IntPtr.Zero, SMTO_ABORTIFHUNG, 5000, out result);

        // Also broadcast to all top-level windows for good measure
        EnumWindows((hWnd, lParam) =>
        {
            SendMessageTimeoutW(hWnd, WM_SETTINGCHANGE, IntPtr.Zero,
                "ImmersiveColorSet", SMTO_ABORTIFHUNG, 5000, out _);
            SendMessageTimeoutW(hWnd, WM_THEMECHANGED, IntPtr.Zero,
                IntPtr.Zero, SMTO_ABORTIFHUNG, 5000, out _);
            return true;
        }, IntPtr.Zero);

        Log.Debug("Theme change broadcast sent");
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd, int Msg, IntPtr wParam, string lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeoutW(
        IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
}
