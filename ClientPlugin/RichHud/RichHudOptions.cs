using ClientPlugin.Settings;

namespace ClientPlugin.RichHud;

/// <summary>
/// Shared get/set/save for Pulsar and Rich HUD controls.
/// </summary>
internal static class RichHudOptions
{
    public static string StatusLabel() =>
        (Plugin.StatusTitle ?? "HDR Output") + "\n" + (Plugin.StatusInfo ?? "");

    public static bool HdrToggleEnabled() =>
        Plugin.DisplayHdrSupported || Config.Current.ForceEnable;

    public static bool GetEnabled() => Config.Current.Enabled;

    public static void SetEnabled(bool value)
    {
        Config.Current.Enabled = value;
        Save();
    }

    public static float GetPeak() => Config.Current.PeakBrightness;

    public static void SetPeak(float value)
    {
        Config.Current.PeakBrightness = value;
        Save();
    }

    public static float GetPaperWhite() => Config.Current.PaperWhite;

    public static void SetPaperWhite(float value)
    {
        Config.Current.PaperWhite = value;
        Save();
    }

    public static float GetUiBrightness() => Config.Current.UiBrightness;

    public static void SetUiBrightness(float value)
    {
        Config.Current.UiBrightness = value;
        Save();
    }

    public static float GetUiShoulder() => Config.Current.UiShoulder;

    public static void SetUiShoulder(float value)
    {
        Config.Current.UiShoulder = value;
        Save();
    }

    public static float GetSourcePeak() => Config.Current.SourcePeak;

    public static void SetSourcePeak(float value)
    {
        Config.Current.SourcePeak = value;
        Save();
    }

    public static float GetBlackLift() => Config.Current.BlackLift;

    public static void SetBlackLift(float value)
    {
        Config.Current.BlackLift = value;
        Save();
    }

    public static float GetLdrIntensity() => Config.Current.LdrIntensity;

    public static void SetLdrIntensity(float value)
    {
        Config.Current.LdrIntensity = value;
        Save();
    }

    public static bool GetHqTarget() => Config.Current.HqTarget;

    public static void SetHqTarget(bool value)
    {
        Config.Current.HqTarget = value;
        Save();
    }

    public static bool GetAllowTearing() => Config.Current.AllowTearing;

    public static void SetAllowTearing(bool value)
    {
        Config.Current.AllowTearing = value;
        Save();
    }

    public static bool GetLowLatencyMode() => Config.Current.LowLatencyMode;

    public static void SetLowLatencyMode(bool value)
    {
        Config.Current.LowLatencyMode = value;
        Save();
    }

    public static bool GetForceEnable() => Config.Current.ForceEnable;

    public static void SetForceEnable(bool value)
    {
        Config.Current.ForceEnable = value;
        Save();
    }

    public static bool GetDisabledAfterCrash() => Config.Current.DisabledAfterCrash;

    public static void SetDisabledAfterCrash(bool value)
    {
        Config.Current.DisabledAfterCrash = value;
        Save();
    }

    public static void ShowStatus() => Config.ShowStatus();

    public static void Save() => ConfigStorage.Save(Config.Current);
}
