using System;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using ClientPlugin.Rendering;
using HarmonyLib;
using SharpDX.DXGI;
using VRageRender;

namespace ClientPlugin.Anomaly;

/// <summary>DX11 swapchain and UI composite. Loaded only beside the main plugin on Windows.</summary>
public static class Dx11Entry
{
    static bool started;
    static bool subscribed;

    public static void OnUpdate()
    {
        ApplyAssets();
        if (started || Config.Current.DisabledAfterCrash)
            return;
        if (!AnomalyRenderer.Found || !AnomalyRenderer.Decided || !AnomalyRenderer.AllowDx11Device)
            return;
        started = true;
        DetermineHdrMode();
        if (!Plugin.IsLive)
            return;
        if (!AnomalyHook.RegistryFound)
        {
            Fail("HDR Output — Anomaly missing",
                "Anomaly Shader Framework was not found. Enable Anomaly and restart.");
            return;
        }
        var packRoot = ResolvePackRoot();
        if (string.IsNullOrEmpty(packRoot))
        {
            Fail("HDR Output — Pack failed",
                "Could not find the Anomaly Display pack. HDR pipeline not started.");
            return;
        }
        HdrResources.TryResolveShaderFolder(packRoot);
        HdrResources.TryResolveShaderFolder(Plugin.AssetFolder);
        try
        {
            HdrResources.InitShaders();
            if (!AnomalyHook.TryRegisterPack(packRoot))
            {
                Fail("HDR Output — Pack failed",
                    AnomalyHook.RegistrationError ?? "Could not register the Anomaly Display pack. HDR pipeline not started.");
                return;
            }
            Patches.SwapChainReplacer.ProbeFeatureSupport();
            Patches.SwapChainReplacer.ApplyMaximumFrameLatency();
            var harmony = new Harmony(Plugin.Name);
            harmony.PatchAll(typeof(Dx11Entry).Assembly);
        }
        catch (Exception e)
        {
            Fail("HDR Output — Init failed", e.Message);
            VRage.Utils.MyLog.Default.WriteLine($"HDR: Init failed: {e}");
            return;
        }
        if (!subscribed)
        {
            Config.Current.PropertyChanged += OnConfigChanged;
            subscribed = true;
        }
    }

    public static void OnDispose()
    {
        if (subscribed)
        {
            Config.Current.PropertyChanged -= OnConfigChanged;
            subscribed = false;
        }
        HdrResources.Release();
    }

    public static void SetAssetsPath(string path)
    {
        HdrResources.SetAssetsPath(path);
    }

    public static void ApplyAssets()
    {
        if (!string.IsNullOrEmpty(Plugin.AssetFolder))
            HdrResources.SetAssetsPath(Plugin.AssetFolder);
        if (!string.IsNullOrEmpty(Plugin.AnomalyPackRoot))
            HdrResources.TryResolveShaderFolder(Plugin.AnomalyPackRoot);
    }

    public static string StatusLine() => AnomalyHook.StatusLine();

    static void OnConfigChanged(object sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Config.HqTarget):
                if (!Plugin.IsLive)
                    return;
                MyRenderProxy.Settings.User.HqTarget = Config.Current.HqTarget;
                MyRenderProxy.SetSettingsDirty();
                break;
            case nameof(Config.LowLatencyMode):
                if (!Plugin.IsLive)
                    return;
                Patches.SwapChainReplacer.ApplyMaximumFrameLatency();
                break;
        }
    }

    static void Fail(string title, string info)
    {
        Plugin.StatusTitle = title;
        Plugin.StatusInfo = (Plugin.StatusInfo ?? "") + "\n" + info;
        Plugin.IsLive = false;
        VRage.Utils.MyLog.Default.WriteLine("HDR: " + info);
    }

    static string ResolvePackRoot()
    {
        if (!string.IsNullOrEmpty(Plugin.AnomalyPackRoot) && Directory.Exists(Plugin.AnomalyPackRoot))
            return Plugin.AnomalyPackRoot;
        if (string.IsNullOrEmpty(Plugin.AssetFolder))
            return Plugin.AnomalyPackRoot;
        var nested = Path.Combine(Plugin.AssetFolder, "AnomalyPack");
        return Directory.Exists(nested) ? nested : Plugin.AnomalyPackRoot;
    }

    static void DetermineHdrMode()
    {
        try
        {
            var swapchain = MyRender11.m_swapchain;
            if (swapchain == null)
            {
                Plugin.StatusTitle = "HDR Output — Inactive";
                Plugin.StatusInfo = "No swapchain yet";
                Plugin.IsLive = false;
                return;
            }
            var displayIsHdr = false;
            try
            {
                using var output = swapchain.ContainingOutput;
                using var output6 = output.QueryInterface<Output6>();
                var desc = output6.Description1;
                displayIsHdr = desc.ColorSpace == ColorSpaceType.RgbFullG2084NoneP2020;
                Plugin.StatusInfo = $"Peak: {desc.MaxLuminance:F0} nits | Full frame: {desc.MaxFullFrameLuminance:F0} nits\n" +
                                    $"Depth: {desc.BitsPerColor}-bit\n" +
                                    $"Mode: {(displayIsHdr ? "HDR" : "SDR")}";
            }
            catch
            {
                Plugin.StatusInfo = "Cannot read display info";
            }
            Plugin.DisplayHdrSupported = displayIsHdr;
            var want = Config.Current.Enabled && (displayIsHdr || Config.Current.ForceEnable);
            if (!want)
            {
                Plugin.StatusTitle = displayIsHdr ? "HDR Output — Off (restart to enable)" : "HDR Output — Inactive";
                VRage.Utils.MyLog.Default.WriteLine(
                    $"HDR: {Plugin.StatusInfo}. Enable HDR in settings (or ForceEnable) and restart.");
                Plugin.IsLive = false;
                return;
            }
            if (displayIsHdr)
            {
                Plugin.StatusTitle = "HDR Output — Active";
                VRage.Utils.MyLog.Default.WriteLine($"HDR: Display HDR active. {Plugin.StatusInfo}");
            }
            else
            {
                Plugin.StatusTitle = "HDR Output — Forced";
                VRage.Utils.MyLog.Default.WriteLine($"HDR: Force mode. {Plugin.StatusInfo}");
            }
            Plugin.IsLive = true;
        }
        catch (Exception e)
        {
            Plugin.StatusTitle = "HDR Output — Error";
            Plugin.StatusInfo = e.Message;
            Plugin.IsLive = false;
            VRage.Utils.MyLog.Default.WriteLine($"HDR: {e.Message}");
        }
    }
}
