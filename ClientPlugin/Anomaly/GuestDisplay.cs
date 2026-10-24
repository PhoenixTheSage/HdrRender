using System;
using System.Reflection;

namespace ClientPlugin.Anomaly;

/// <summary>
/// Vulkan HDR display tenant through Anomaly's portable facades. The Display pack registers as on DX11;
/// the renderer grades it at the tone position into FP16 on an HDR swapchain. Uniforms come from
/// TonemapInputs and RenderClock instead of MyRender11, and the HUD transform from DisplayServices.SetHdrUi.
/// </summary>
internal static class GuestDisplay
{
    const string PackId = "hdr.output";
    const string PassId = "hdr.tonemap";
    const BindingFlags Static = BindingFlags.Public | BindingFlags.Static;

    const string Owner = "hdr.output";
    const int AllowTearing = 32, LowLatency = 64;
    static MethodInfo tryRegister, setUniforms, setHdrUi, setHdrScreenshot, requestFor, releaseFor, releaseOwner;
    static PropertyInfo grading, enableTonemapping, needsAlphaLuminance, frameSeconds, hasDisplayTenant, displayInfo;
    static bool probed, registered, uiActive;
    static string error;

    public static string Status =>
        error ?? (!registered ? "Display pack not registered" :
            (HasDisplayTenant ? "Display tenant registered; the renderer grades on an HDR swapchain" : "Display pack registered") + DisplayLine());

    // Same summary the DX11 companion prints from DXGI Output6.
    static string DisplayLine()
    {
        var info = displayInfo?.GetValue(null);
        if (info == null) return "";
        T Field<T>(string name) => (T)info.GetType().GetField(name).GetValue(info);
        string monitor = Field<bool>("DisplayKnown")
            ? $"\nPeak: {Field<float>("MaxLuminance"):F0} nits | Full frame: {Field<float>("MaxFullFrameLuminance"):F0} nits\nDepth: {Field<int>("BitsPerColor")}-bit | Windows HDR: {(Field<bool>("DisplayHdr10") ? "on" : "off")}"
            : "\nDisplay luminance unavailable";
        return monitor + $"\nSwapchain: {(Field<bool>("HdrActive") ? "FP16 scRGB" : Field<bool>("HdrCapable") ? "SDR (HDR available)" : "SDR")} | Grade: {(Field<bool>("TenantGraded") ? "active" : "inactive")} | Tearing: {(Field<bool>("TearingActive") ? "on" : "off")}";
    }

    static bool HasDisplayTenant => hasDisplayTenant?.GetValue(null) is true;

    public static void Update(bool enabled)
    {
        if (!Probe())
            return;
        if (!enabled)
        {
            DisableUi();
            return;
        }
        if (!registered)
            Register();
        if (!registered)
            return;
        WriteUniforms();
        var cfg = Config.Current;
        uiActive = true;
        setHdrUi?.Invoke(null, new object[] { cfg.PaperWhite, cfg.UiBrightness, cfg.UiShoulder, cfg.PeakBrightness });
        setHdrScreenshot?.Invoke(null, new object[] { cfg.PaperWhite });
        // DX11 AllowTearing and LowLatencyMode; Vulkan applies them on the next swapchain/frame without a restart.
        Request(AllowTearing, cfg.AllowTearing);
        Request(LowLatency, cfg.LowLatencyMode);
    }

    public static void Disable() => DisableUi();

    static void Request(int bit, bool on) => (on ? requestFor : releaseFor)?.Invoke(null, new object[] { Owner, bit });

    static void DisableUi()
    {
        if (!uiActive)
            return;
        uiActive = false;
        setHdrUi?.Invoke(null, new object[] { 0f, 1f, 0f, 0f });
        setHdrScreenshot?.Invoke(null, new object[] { 0f });
        releaseOwner?.Invoke(null, new object[] { Owner });
    }

    static bool Probe()
    {
        if (probed)
            return tryRegister != null && setUniforms != null && grading != null;
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type registry, fullscreen, inputs, clock, display, owned;
            try
            {
                registry = assembly.GetType("ClientPlugin.Shaders.ShaderPackRegistry", false);
                fullscreen = assembly.GetType("ClientPlugin.Shaders.FullscreenPassRegistry", false);
                inputs = assembly.GetType("ClientPlugin.Shaders.TonemapInputs", false);
                clock = assembly.GetType("ClientPlugin.Shaders.RenderClock", false);
                display = assembly.GetType("ClientPlugin.Shaders.DisplayServices", false);
                owned = assembly.GetType("ClientPlugin.Shaders.OwnedPassRegistry", false);
            }
            catch
            {
                continue;
            }
            // Only Anomaly's core assembly carries the portable Grading input and RenderClock.
            if (inputs?.GetProperty("Grading", Static) == null || clock == null)
                continue;
            tryRegister = registry?.GetMethod("TryRegister", Static, null,
                new[] { typeof(string), typeof(string), typeof(string), typeof(string).MakeByRefType() }, null);
            setUniforms = fullscreen?.GetMethod("SetUniforms", Static, null, new[] { typeof(string), typeof(float[]) }, null);
            grading = inputs.GetProperty("Grading", Static);
            enableTonemapping = inputs.GetProperty("EnableTonemapping", Static);
            needsAlphaLuminance = inputs.GetProperty("NeedsAlphaLuminance", Static);
            frameSeconds = clock.GetProperty("FrameSeconds", Static);
            setHdrUi = display?.GetMethod("SetHdrUi", Static);
            setHdrScreenshot = display?.GetMethod("SetHdrScreenshot", Static);
            requestFor = display?.GetMethod("RequestFor", Static);
            releaseFor = display?.GetMethod("ReleaseFor", Static);
            releaseOwner = display?.GetMethod("ReleaseOwner", Static);
            displayInfo = display?.GetProperty("Info", Static);
            hasDisplayTenant = owned?.GetProperty("HasDisplayTenant", Static);
            probed = true;
            break;
        }
        if (!probed)
            return false;
        if (tryRegister == null || setUniforms == null)
            error = "Update Anomaly: the portable HDR display facades are missing.";
        return error == null;
    }

    static void Register()
    {
        var root = Plugin.ResolvePackRoot();
        if (string.IsNullOrEmpty(root))
        {
            error = "Could not find the Anomaly Display pack.";
            return;
        }
        var args = new object[] { PackId, root, PassId, null };
        try
        {
            registered = tryRegister.Invoke(null, args) is true;
            if (!registered)
                error = args[3] as string ?? "Anomaly rejected the HDR Display pack.";
        }
        catch (Exception e)
        {
            error = (e.InnerException ?? e).Message;
        }
        VRage.Utils.MyLog.Default.WriteLine("HDR: Vulkan Display pack " + (registered ? "registered at " + root : "failed: " + error));
    }

    // Same 28-float layout as the DX11 AnomalyHook.WriteUniforms.
    static void WriteUniforms()
    {
        if (grading.GetValue(null) is not float[] g || g.Length < 19)
            return;
        var cfg = Config.Current;
        var values = new float[28];
        values[0] = cfg.PaperWhite / 80f;
        values[1] = cfg.PeakBrightness / 80f;
        values[2] = cfg.SourcePeak / 80f;
        // Grading order: BloomMult, BloomDirtRatio, GrainStrength, GrainAmount, GrainSize, Contrast, Brightness,
        // Saturation, BrightnessFactorR/G/B, Vibrance, SepiaStrength, LightColor xyz, DarkColor xyz.
        Array.Copy(g, 0, values, 3, 16);
        values[19] = Convert.ToSingle(frameSeconds?.GetValue(null) ?? 0d);
        values[20] = g[16];
        values[21] = g[17];
        values[22] = g[18];
        values[23] = cfg.BlackLift;
        values[24] = enableTonemapping?.GetValue(null) is false ? 1f : 0f;
        values[25] = needsAlphaLuminance?.GetValue(null) is true ? 1f : 0f;
        setUniforms.Invoke(null, new object[] { PassId, values });
    }
}
