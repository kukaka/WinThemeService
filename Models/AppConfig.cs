namespace WinThemeService.Models;

public class AppConfig
{
    public string DayStart { get; set; } = "07:00";
    public string DayEnd { get; set; } = "19:00";
    public bool AutoSwitch { get; set; } = true;
}
