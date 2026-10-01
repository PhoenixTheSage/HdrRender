using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using ClientPlugin.Anomaly;
using ClientPlugin.Rendering;
using ClientPlugin.RichHud;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Layouts;
using HarmonyLib;
using JetBrains.Annotations;
using Sandbox.Graphics.GUI;
using SharpDX.DXGI;
using VRage.Plugins;
using VRageRender;

#if !DEV_BUILD
[assembly: AssemblyVersion("1.2.1.0")]
[assembly: AssemblyFileVersion("1.2.1.0")]
#endif

namespace ClientPlugin;

public class Plugin : IPlugin
{
    public const string Name = "HdrOutput";
    internal static string StatusTitle { get; private set; } = "HDR Output";
    internal static string StatusInfo { get; private set; }
    internal static bool DisplayHdrSupported { get; private set; }
    internal static bool IsLive { get; private set; }

    static string _assetFolder;
    static string _anomalyPackRoot;
    private SettingsGenerator _settingsGenerator;

    [UsedImplicitly]
    public void LoadAssets(string path)
    {
        VRage.Utils.MyLog.Default.WriteLine($"HDR: LoadAssets called with path: {path}");
        _assetFolder = path;
        HdrResources.SetAssetsPath(path);
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
                _anomalyPackRoot = pair.Value;
            else
            {
                _assetFolder = pair.Value;
                HdrResources.SetAssetsPath(pair.Value);
            }
        }

        // Pulsar 2.4+ may ship only AnomalyPack. UiComposite.hlsl lives next to it.
        if (!string.IsNullOrEmpty(_anomalyPackRoot))
            HdrResources.TryResolveShaderFolder(_anomalyPackRoot);
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

        DetermineHdrMode();
        _settingsGenerator = new SettingsGenerator();
        AnomalyTerminalHook.TryInstall();

        if (!IsLive)
            return;

        if (!AnomalyHook.RegistryFound)
        {
            StatusTitle = "HDR Output — Anomaly missing";
            StatusInfo = (StatusInfo ?? "") + "\nAnomaly Shader Framework was not found. Enable Anomaly and restart.";
            IsLive = false;
            VRage.Utils.MyLog.Default.WriteLine("HDR: Anomaly not found; HDR pipeline not started");
            return;
        }

        var packRoot = ResolvePackRoot();
        if (string.IsNullOrEmpty(packRoot))
        {
            FailLive("HDR Output — Pack failed",
                "Could not find the Anomaly Display pack. HDR pipeline not started.");
            return;
        }

        HdrResources.TryResolveShaderFolder(packRoot);
        HdrResources.TryResolveShaderFolder(_assetFolder);

        try
        {
            HdrResources.InitShaders();
            if (!AnomalyHook.TryRegisterPack(packRoot))
            {
                FailLive("HDR Output — Pack failed",
                    AnomalyHook.RegistrationError ?? "Could not register the Anomaly Display pack. HDR pipeline not started.");
                return;
            }
            Patches.SwapChainReplacer.ProbeFeatureSupport();
            Patches.SwapChainReplacer.ApplyMaximumFrameLatency();

            var harmony = new Harmony(Name);
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception e)
        {
            FailLive("HDR Output — Init failed", e.Message);
            VRage.Utils.MyLog.Default.WriteLine($"HDR: Init failed: {e}");
            return;
        }

        Config.Current.PropertyChanged += OnConfigChanged;
    }

    static void FailLive(string title, string info)
    {
        StatusTitle = title;
        StatusInfo = (StatusInfo ?? "") + "\n" + info;
        IsLive = false;
        VRage.Utils.MyLog.Default.WriteLine("HDR: " + info);
    }

    private static void OnConfigChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Config.HqTarget):
                if (!IsLive)
                    return;
                MyRenderProxy.Settings.User.HqTarget = Config.Current.HqTarget;
                MyRenderProxy.SetSettingsDirty();
                break;
            case nameof(Config.LowLatencyMode):
                if (!IsLive)
                    return;
                Patches.SwapChainReplacer.ApplyMaximumFrameLatency();
                break;
        }
    }

    public void Dispose()
    {
        ConfigStorage.FlushPending(true);
        Config.Current.PropertyChanged -= OnConfigChanged;
        HdrResources.Release();
    }

    public void Update()
    {
        AnomalyTerminalHook.TryInstall();
        ConfigStorage.FlushPending();
    }

    [UsedImplicitly]
    public void OpenConfigDialog()
    {
        _settingsGenerator.SetLayout<Simple>();
        MyGuiSandbox.AddScreen(_settingsGenerator.Dialog);
    }

    static string ResolvePackRoot()
    {
        if (!string.IsNullOrEmpty(_anomalyPackRoot) && Directory.Exists(_anomalyPackRoot))
            return _anomalyPackRoot;
        if (string.IsNullOrEmpty(_assetFolder))
            return _anomalyPackRoot;
        var nested = Path.Combine(_assetFolder, "AnomalyPack");
        return Directory.Exists(nested) ? nested : _anomalyPackRoot;
    }

    static void DetermineHdrMode()
    {
        try
        {
            var swapchain = MyRender11.m_swapchain;
            if (swapchain == null)
            {
                StatusTitle = "HDR Output — Inactive";
                StatusInfo = "No swapchain yet";
                IsLive = false;
                return;
            }

            var displayIsHdr = false;
            try
            {
                using var output = swapchain.ContainingOutput;
                using var output6 = output.QueryInterface<Output6>();
                var desc = output6.Description1;
                displayIsHdr = desc.ColorSpace == ColorSpaceType.RgbFullG2084NoneP2020;
                StatusInfo = $"Peak: {desc.MaxLuminance:F0} nits | Full frame: {desc.MaxFullFrameLuminance:F0} nits\n" +
                             $"Depth: {desc.BitsPerColor}-bit\n" +
                             $"Mode: {(displayIsHdr ? "HDR" : "SDR")}";
            }
            catch
            {
                StatusInfo = "Cannot read display info";
            }

            DisplayHdrSupported = displayIsHdr;
            var want = Config.Current.Enabled && (displayIsHdr || Config.Current.ForceEnable);
            if (!want)
            {
                StatusTitle = displayIsHdr ? "HDR Output — Off (restart to enable)" : "HDR Output — Inactive";
                VRage.Utils.MyLog.Default.WriteLine(
                    $"HDR: {StatusInfo}. Enable HDR in settings (or ForceEnable) and restart.");
                IsLive = false;
                return;
            }

            if (displayIsHdr)
            {
                StatusTitle = "HDR Output — Active";
                VRage.Utils.MyLog.Default.WriteLine($"HDR: Display HDR active. {StatusInfo}");
            }
            else
            {
                StatusTitle = "HDR Output — Forced";
                VRage.Utils.MyLog.Default.WriteLine($"HDR: Force mode. {StatusInfo}");
            }

            IsLive = true;
        }
        catch (Exception e)
        {
            StatusTitle = "HDR Output — Error";
            StatusInfo = e.Message;
            IsLive = false;
            VRage.Utils.MyLog.Default.WriteLine($"HDR: {e.Message}");
        }
    }
}
