using System.Collections.Generic;
using System.Reflection;
using ClientPlugin.Anomaly;
using ClientPlugin.RichHud;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Layouts;
using JetBrains.Annotations;
using Sandbox.Graphics.GUI;
using VRage.Plugins;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("HdrOutput.Dx11")]

#if !DEV_BUILD
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
#endif

namespace ClientPlugin;

public class Plugin : IPlugin
{
    public const string Name = "HdrOutput";
    internal static string StatusTitle { get; set; } = "HDR Output";
    internal static string StatusInfo { get; set; }
    internal static bool DisplayHdrSupported { get; set; }
    internal static bool IsLive { get; set; }
    internal static string AssetFolder { get; private set; }
    internal static string AnomalyPackRoot { get; private set; }

    private SettingsGenerator _settingsGenerator;

    [UsedImplicitly]
    public void LoadAssets(string path)
    {
        VRage.Utils.MyLog.Default.WriteLine($"HDR: LoadAssets called with path: {path}");
        AssetFolder = path;
        if (AnomalyRenderer.AllowDx11Device)
            Dx11Bridge.Call("SetAssetsPath", path);
    }

    [UsedImplicitly]
    public void LoadAssets(IReadOnlyDictionary<string, string> assets)
    {
        if (assets == null)
            return;
        foreach (var pair in assets)
        {
            if (string.IsNullOrEmpty(pair.Value))
                continue;
            VRage.Utils.MyLog.Default.WriteLine($"HDR: asset {pair.Key}={pair.Value}");
            if (pair.Key == "AnomalyPack")
                AnomalyPackRoot = pair.Value;
            else
                AssetFolder = pair.Value;
        }
        if (AnomalyRenderer.AllowDx11Device)
            Dx11Bridge.Call("ApplyAssets");
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Init(object gameInstance)
    {
        if (Config.Current.DisabledAfterCrash)
        {
            StatusTitle = "HDR Output — Disabled (last launch crashed)";
            StatusInfo = "Auto-disabled after a crash during plugin init.\n" +
                         "Uncheck 'DisabledAfterCrash' in the config to retry on next launch.";
            VRage.Utils.MyLog.Default.WriteLine("HDR: Skipped due to DisabledAfterCrash flag");
            _settingsGenerator = new SettingsGenerator();
            AnomalyTerminalHook.TryInstall();
            return;
        }
        _settingsGenerator = new SettingsGenerator();
        AnomalyTerminalHook.TryInstall();
    }

    public void Dispose()
    {
        ConfigStorage.FlushPending(true);
        if (AnomalyRenderer.AllowDx11Device)
            Dx11Bridge.Call("OnDispose");
    }

    public void Update()
    {
        AnomalyTerminalHook.TryInstall();
        ConfigStorage.FlushPending();
        if (AnomalyRenderer.AllowGuestWork)
            RequestRenderer(Config.Current.Enabled);
        if (AnomalyRenderer.Decided && !AnomalyRenderer.AllowDx11Device && !Config.Current.DisabledAfterCrash)
        {
            StatusTitle = AnomalyRenderer.LinuxRequiresGuest
                ? "HDR Output — waiting for Vulkan"
                : "HDR Output — DX11 display closed";
            StatusInfo = AnomalyRenderer.AllowGuestWork
                ? "Vulkan requests an FP16 swapchain. The DXGI swapchain stays on DX11."
                : AnomalyRenderer.LinuxRequiresGuest
                    ? "Linux stays idle until Anomaly's Vulkan guest bus is open."
                    : "Swapchain path stays on DX11. Renderer is " + AnomalyRenderer.ActiveName + ".";
            IsLive = false;
        }
        if (!AnomalyRenderer.AllowDx11Device || Config.Current.DisabledAfterCrash)
            return;
        Dx11Bridge.Call("OnUpdate");
    }

    static void RequestRenderer(bool enabled)
    {
        foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var type = assembly.GetType("ClientPlugin.Shaders.DisplayServices", false);
            if (type == null)
                continue;
            type.GetMethod(enabled ? "Request" : "Release")?.Invoke(null, new object[] { 1 });
            return;
        }
    }

    [UsedImplicitly]
    public void OpenConfigDialog()
    {
        _settingsGenerator.SetLayout<Simple>();
        MyGuiSandbox.AddScreen(_settingsGenerator.Dialog);
    }
}
