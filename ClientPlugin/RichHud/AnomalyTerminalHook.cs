using System;
using System.Reflection;
using VRage.Utils;

namespace ClientPlugin.RichHud;

/// <summary>
/// Optional bind to Anomaly's Rich HUD extension.
/// Well-known type: <c>ClientPlugin.RichHud.TerminalConfigRegistry</c>.
/// </summary>
internal static class AnomalyTerminalHook
{
    public const string RegistryTypeName = "ClientPlugin.RichHud.TerminalConfigRegistry";
    public const string FolderTitle = "HDR";
    public const string SettingsPage = "Settings";
    public const string PageTitle = FolderTitle;

    static readonly object Gate = new();
    static bool _installed;
    static string _lastRequestError;

    public static bool Installed
    {
        get
        {
            lock (Gate)
                return _installed;
        }
    }

    public static bool TryInstall()
    {
        lock (Gate)
        {
            if (_installed)
                return true;

            var page = RequestPageUnlocked();
            if (page == null)
                return false;

            Populate(page);
            _installed = true;
            MyLog.Default.WriteLine("HDR: Rich HUD page under Anomaly Shaders / " + FolderTitle + " / " + SettingsPage);
            return true;
        }
    }

    public static void Reset()
    {
        lock (Gate)
            _installed = false;
    }

    static object RequestPageUnlocked()
    {
        Assembly[] assemblies;
        try
        {
            assemblies = AppDomain.CurrentDomain.GetAssemblies();
        }
        catch
        {
            return null;
        }

        foreach (var assembly in assemblies)
        {
            if (assembly == null || assembly == typeof(AnomalyTerminalHook).Assembly)
                continue;

            Type registry;
            try
            {
                registry = assembly.GetType(RegistryTypeName, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                continue;
            }

            var requestFolder = FindStatic(registry, "RequestFolderPage", typeof(string), typeof(string));
            var request = FindStatic(registry, "RequestPage", typeof(string));
            if (requestFolder == null && request == null)
                continue;

            try
            {
                if (requestFolder != null)
                    return requestFolder.Invoke(null, new object[] { FolderTitle, SettingsPage });
                return request.Invoke(null, new object[] { PageTitle });
            }
            catch (Exception e)
            {
                var message = e.GetType().Name + ": " + e.Message;
                if (_lastRequestError != message)
                {
                    _lastRequestError = message;
                    MyLog.Default.WriteLine("HDR: Anomaly RequestPage: " + message);
                }

                return null;
            }
        }

        return null;
    }

    static void Populate(object page)
    {
        var type = page.GetType();
        Invoke(type, page, "Category", "Display");
        Invoke(type, page, "Checkbox", "HDR",
            (Func<bool>)RichHudOptions.GetEnabled,
            (Action<bool>)RichHudOptions.SetEnabled,
            "Enable the HDR pipeline. Requires Windows HDR on this display, or ForceEnable. Restart to apply.",
            (Func<bool>)RichHudOptions.HdrToggleEnabled);
        Invoke(type, page, "Slider", "Peak", 200f, 2500f,
            (Func<float>)RichHudOptions.GetPeak,
            (Action<float>)RichHudOptions.SetPeak,
            "Display peak brightness in nits",
            50f);
        Invoke(type, page, "Slider", "Paper white", 80f, 500f,
            (Func<float>)RichHudOptions.GetPaperWhite,
            (Action<float>)RichHudOptions.SetPaperWhite,
            "Scene / sRGB reference white in nits",
            10f);
        Invoke(type, page, "Slider", "Source peak", 1000f, 10000f,
            (Func<float>)RichHudOptions.GetSourcePeak,
            (Action<float>)RichHudOptions.SetSourcePeak,
            "Expected scene peak luminance in nits (BT.2390 source peak)",
            100f);
        Invoke(type, page, "Slider", "Black lift", 0f, 0.05f,
            (Func<float>)RichHudOptions.GetBlackLift,
            (Action<float>)RichHudOptions.SetBlackLift,
            "Black floor lift (BT.2390). 0 = off (recommended for OLED).",
            0.002f);
        Invoke(type, page, "Slider", "LDR intensity", 1f, 16f,
            (Func<float>)RichHudOptions.GetLdrIntensity,
            (Action<float>)RichHudOptions.SetLdrIntensity,
            "Emissive LDR billboard boost. Diffuse / UI unaffected.",
            0.5f);

        Invoke(type, page, "Category", "UI");
        Invoke(type, page, "Slider", "UI brightness", 1f, 4f,
            (Func<float>)RichHudOptions.GetUiBrightness,
            (Action<float>)RichHudOptions.SetUiBrightness,
            "HUD-only gain on top of paper white. 1 = paper white.",
            0.1f);
        Invoke(type, page, "Slider", "UI shoulder", 0f, 0.8f,
            (Func<float>)RichHudOptions.GetUiShoulder,
            (Action<float>)RichHudOptions.SetUiShoulder,
            "HUD contrast. 0 = flat scale. Higher restores midtones and lifts chrome above paper white.",
            0.05f);

        Invoke(type, page, "Category", "Advanced");
        Invoke(type, page, "Checkbox", "HQ target",
            (Func<bool>)RichHudOptions.GetHqTarget,
            (Action<bool>)RichHudOptions.SetHqTarget,
            "Use R16G16B16A16 lighting buffer (doubles LBuffer/Bloom VRAM).");
        Invoke(type, page, "Checkbox", "Allow tearing",
            (Func<bool>)RichHudOptions.GetAllowTearing,
            (Action<bool>)RichHudOptions.SetAllowTearing,
            "Windowed / Fullscreen Window VRR — restart required. Illegal in exclusive Fullscreen (driver G-Sync still works there).");
        Invoke(type, page, "Checkbox", "Low latency",
            (Func<bool>)RichHudOptions.GetLowLatencyMode,
            (Action<bool>)RichHudOptions.SetLowLatencyMode,
            "Cap GPU frame queue to 1. May stutter when GPU-bound.");
        Invoke(type, page, "Checkbox", "Force enable",
            (Func<bool>)RichHudOptions.GetForceEnable,
            (Action<bool>)RichHudOptions.SetForceEnable,
            "Force HDR on non-HDR displays. Restart required.");
        Invoke(type, page, "Checkbox", "Disabled after crash",
            (Func<bool>)RichHudOptions.GetDisabledAfterCrash,
            (Action<bool>)RichHudOptions.SetDisabledAfterCrash,
            "Uncheck to retry HDR on next launch after an init crash.");
    }

    static void Invoke(Type type, object instance, string name, params object[] args)
    {
        var method = FindInstance(type, name, args);
        if (method == null)
        {
            MyLog.Default.WriteLine("HDR: Anomaly terminal missing " + name);
            return;
        }

        method.Invoke(instance, PadDefaults(method, args));
    }

    static MethodInfo FindStatic(Type type, string name, params Type[] parameters)
    {
        if (type == null)
            return null;
        return type.GetMethod(name, BindingFlags.Public | BindingFlags.Static, null, parameters, null);
    }

    static MethodInfo FindInstance(Type type, string name, object[] args)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance;
        foreach (var method in type.GetMethods(flags))
        {
            if (method.Name != name || method.IsGenericMethodDefinition)
                continue;
            var parameters = method.GetParameters();
            if (!ArgumentsMatch(parameters, args))
                continue;
            return method;
        }

        return null;
    }

    static bool ArgumentsMatch(ParameterInfo[] parameters, object[] args)
    {
        if (args.Length > parameters.Length)
            return false;
        for (var i = 0; i < parameters.Length; i++)
        {
            if (i >= args.Length)
                return parameters[i].IsOptional || parameters[i].HasDefaultValue;
            var value = args[i];
            var expected = parameters[i].ParameterType;
            if (value == null)
            {
                if (expected.IsValueType && Nullable.GetUnderlyingType(expected) == null)
                    return false;
                continue;
            }

            if (!expected.IsInstanceOfType(value))
                return false;
        }

        return true;
    }

    static object[] PadDefaults(MethodInfo method, object[] args)
    {
        var parameters = method.GetParameters();
        if (args.Length == parameters.Length)
            return args;
        var padded = new object[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
            padded[i] = i < args.Length ? args[i] : parameters[i].DefaultValue;
        return padded;
    }
}
