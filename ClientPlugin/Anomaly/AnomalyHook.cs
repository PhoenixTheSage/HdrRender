using System;
using System.Reflection;
using VRage.Render11.Common;
using VRage.Utils;
using VRageRender;

namespace ClientPlugin.Anomaly;

/// <summary>
/// Runtime bind to Anomaly Shader Framework. Resolve well-known types by
/// name — no compile-time reference.
/// </summary>
internal static class AnomalyHook
{
    public const string PackRegistryTypeName = "ClientPlugin.Shaders.ShaderPackRegistry";
    public const string FullscreenTypeName = "ClientPlugin.Shaders.FullscreenPassRegistry";
    public const string OwnedPassTypeName = "ClientPlugin.Shaders.OwnedPassRegistry";
    public const string TonemapInputsTypeName = "ClientPlugin.Shaders.TonemapInputs";
    public const string PackId = "hdr.output";
    public const string PassId = "hdr.tonemap";

    static readonly object Gate = new();

    static MethodInfo _register;
    static MethodInfo _tryRegister;
    static MethodInfo _setUniforms;
    static PropertyInfo _hasDisplayTenant;
    static PropertyInfo _hasUpscaleConsumer;
    static PropertyInfo _enableTonemapping;
    static PropertyInfo _needsAlphaLuminance;
    static bool _packRegistered;
    static bool _probed;
    static string _lastUniformError;
    public static string RegistrationError { get; private set; }

    public static bool RegistryFound
    {
        get
        {
            Probe();
            lock (Gate)
                return _register != null && _setUniforms != null;
        }
    }

    public static bool PackRegistered
    {
        get { lock (Gate) return _packRegistered; }
    }

    public static bool HasDisplayTenant
    {
        get
        {
            Probe();
            PropertyInfo prop;
            lock (Gate)
                prop = _hasDisplayTenant;
            return GetBool(prop);
        }
    }

    public static bool HasUpscaleConsumer
    {
        get
        {
            Probe();
            PropertyInfo prop;
            lock (Gate)
                prop = _hasUpscaleConsumer;
            return GetBool(prop);
        }
    }

    public static void Probe()
    {
        lock (Gate)
        {
            if (_probed && _register != null)
                return;
            _probed = true;
            ScanUnlocked();
        }
    }

    public static bool TryRegisterPack(string root)
    {
        if (string.IsNullOrEmpty(root))
            return false;
        Probe();
        MethodInfo register;
        lock (Gate)
        {
            _packRegistered = false;
            RegistrationError = null;
            register = _tryRegister;
        }
        if (register == null)
        {
            RegistrationError = "Anomaly checked pack registration is unavailable. Update Anomaly and restart.";
            MyLog.Default.WriteLine("HDR: " + RegistrationError);
            return false;
        }

        try
        {
            var args = new object[] { PackId, root, PassId, null };
            if (register.Invoke(null, args) is not true)
            {
                RegistrationError = args[3] as string ?? "Anomaly rejected the HDR Display pack.";
                MyLog.Default.WriteLine("HDR: " + RegistrationError);
                return false;
            }
            lock (Gate)
                _packRegistered = true;
            MyLog.Default.WriteLine("HDR: registered Anomaly pack " + PackId + " at " + root);
            return true;
        }
        catch (Exception e)
        {
            RegistrationError = (e.InnerException ?? e).Message;
            MyLog.Default.WriteLine("HDR: ShaderPackRegistry.TryRegister failed: " + RegistrationError);
            return false;
        }
    }

    public static void WriteUniforms()
    {
        if (!Plugin.IsLive)
            return;
        Probe();
        MethodInfo set;
        PropertyInfo enable;
        PropertyInfo needsAlpha;
        lock (Gate)
        {
            set = _setUniforms;
            enable = _enableTonemapping;
            needsAlpha = _needsAlphaLuminance;
        }

        if (set == null)
            return;

        var cfg = Config.Current;
        ref var pp = ref MyRender11.Postprocess;
        var enableTonemap = GetBool(enable, true);
        var needsAlphaLum = GetBool(needsAlpha, false);
        var values = new float[28];
        values[0] = cfg.PaperWhite / 80f;
        values[1] = cfg.PeakBrightness / 80f;
        values[2] = cfg.SourcePeak / 80f;
        values[3] = pp.Data.BloomMult;
        values[4] = pp.Data.BloomDirtRatio;
        values[5] = pp.Data.GrainStrength;
        values[6] = pp.Data.GrainAmount;
        values[7] = pp.Data.GrainSize;
        values[8] = pp.Data.Contrast;
        values[9] = pp.Data.Brightness;
        values[10] = pp.Data.Saturation;
        values[11] = pp.Data.BrightnessFactorR;
        values[12] = pp.Data.BrightnessFactorG;
        values[13] = pp.Data.BrightnessFactorB;
        values[14] = pp.Data.Vibrance;
        values[15] = pp.Data.SepiaStrength;
        values[16] = pp.Data.LightColor.X;
        values[17] = pp.Data.LightColor.Y;
        values[18] = pp.Data.LightColor.Z;
        values[19] = MyCommon.FrameConstantsData.FrameTime;
        values[20] = pp.Data.DarkColor.X;
        values[21] = pp.Data.DarkColor.Y;
        values[22] = pp.Data.DarkColor.Z;
        values[23] = cfg.BlackLift;
        values[24] = enableTonemap ? 0f : 1f;
        values[25] = needsAlphaLum ? 1f : 0f;
        try
        {
            set.Invoke(null, new object[] { PassId, values });
        }
        catch (Exception e)
        {
            var message = e.Message;
            lock (Gate)
            {
                if (_lastUniformError == message)
                    return;
                _lastUniformError = message;
            }

            MyLog.Default.WriteLine("HDR: SetUniforms failed: " + message);
        }
    }

    public static string StatusLine()
    {
        Probe();
        return "Anomaly pack=" + (PackRegistered ? "yes" : "no") +
               " registry=" + (RegistryFound ? "yes" : "no") +
               " displayTenant=" + (HasDisplayTenant ? "yes" : "no") +
               " upscale=" + (HasUpscaleConsumer ? "yes" : "no");
    }

    static bool GetBool(PropertyInfo prop, bool fallback = false)
    {
        if (prop == null)
            return fallback;
        try
        {
            return prop.GetValue(null) is true;
        }
        catch
        {
            return fallback;
        }
    }

    static void ScanUnlocked()
    {
        Assembly[] assemblies;
        try
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        catch
        {
            return;
        }

        foreach (var assembly in assemblies)
        {
            if (assembly == null || assembly == typeof(AnomalyHook).Assembly)
                continue;
            Type pack = null;
            Type fullscreen = null;
            Type owned = null;
            Type inputs = null;
            try
            {
                pack = assembly.GetType(PackRegistryTypeName, throwOnError: false, ignoreCase: false);
                fullscreen = assembly.GetType(FullscreenTypeName, throwOnError: false, ignoreCase: false);
                owned = assembly.GetType(OwnedPassTypeName, throwOnError: false, ignoreCase: false);
                inputs = assembly.GetType(TonemapInputsTypeName, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                continue;
            }

            if (pack != null && _register == null)
                _register = pack.GetMethod("Register", BindingFlags.Public | BindingFlags.Static);
            if (pack != null && _tryRegister == null)
                _tryRegister = pack.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(string), typeof(string), typeof(string).MakeByRefType() }, null);

            if (fullscreen != null && _setUniforms == null)
                _setUniforms = fullscreen.GetMethod("SetUniforms", BindingFlags.Public | BindingFlags.Static);

            if (owned != null)
            {
                _hasDisplayTenant = owned.GetProperty("HasDisplayTenant", BindingFlags.Public | BindingFlags.Static);
                _hasUpscaleConsumer = owned.GetProperty("HasUpscaleConsumer", BindingFlags.Public | BindingFlags.Static);
            }

            if (inputs != null)
            {
                _enableTonemapping = inputs.GetProperty("EnableTonemapping", BindingFlags.Public | BindingFlags.Static);
                _needsAlphaLuminance = inputs.GetProperty("NeedsAlphaLuminance", BindingFlags.Public | BindingFlags.Static);
            }

            if (_register != null && _setUniforms != null)
                return;
        }
    }
}
