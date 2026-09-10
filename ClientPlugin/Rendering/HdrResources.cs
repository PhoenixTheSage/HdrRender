using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using SharpDX.D3DCompiler;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using VRage.Render11.Common;
using VRage.Render11.Resources;
using VRageRender;

namespace ClientPlugin.Rendering;

internal static class HdrResources
{
    public const Format BackbufferFormat = Format.R16G16B16A16_Float;
    public const ColorSpaceType HdrColorSpace = ColorSpaceType.RgbFullG10NoneP709;

    public static bool Initialized { get; private set; }

    public static VertexShader UiCompositeVertexShader { get; private set; }
    public static PixelShader UiCompositePixelShader { get; private set; }
    public static IConstantBuffer UiConstantBuffer { get; private set; }

    [StructLayout(LayoutKind.Sequential)]
    public struct UiCompositeConstants
    {
        public float PaperWhite;
        public float UiGain;
        public float UiShoulder;
        public float UiPeak;
    }

    private static string _shadersPath;

    public static void SetAssetsPath(string folder)
    {
        TryResolveShaderFolder(folder);
    }

    /// <summary>
    /// Accepts Assets/, Assets/Shaders, AnomalyPack, or the .hlsl file itself.
    /// Pulsar 2.4+ often delivers only the named AnomalyPack folder.
    /// </summary>
    public static bool TryResolveShaderFolder(string hint)
    {
        foreach (var candidate in ShaderFolderCandidates(hint))
        {
            if (string.IsNullOrEmpty(candidate))
                continue;
            var hlsl = Path.Combine(candidate, "UiComposite.hlsl");
            if (!File.Exists(hlsl))
                continue;
            _shadersPath = candidate;
            return true;
        }

        return !string.IsNullOrEmpty(_shadersPath) &&
               File.Exists(Path.Combine(_shadersPath, "UiComposite.hlsl"));
    }

    public static void InitShaders()
    {
        if (UiCompositePixelShader != null)
            return;

        if (!TryResolveShaderFolder(_shadersPath))
        {
            var msg = "HDR: UiComposite.hlsl not found. Pulsar must deliver AssetFolder " +
                      "(ClientPlugin/Assets) or a Shaders folder next to AnomalyPack.";
            VRage.Utils.MyLog.Default.WriteLine(msg);
            throw new InvalidOperationException(msg);
        }

        VRage.Utils.MyLog.Default.WriteLine($"HDR: InitShaders, _shadersPath={_shadersPath}");

        var device = MyRender11.DeviceInstance;

        UiCompositeVertexShader = CompileVertex(device, "UiComposite.hlsl", "vs_main");
        UiCompositePixelShader = CompilePixel(device, "UiComposite.hlsl", "ps_main");

        UiConstantBuffer = MyManagers.Buffers.CreateConstantBuffer(
            "HdrOutput.UiConstants",
            16,
            usage: ResourceUsage.Dynamic);
    }

    static IEnumerable<string> ShaderFolderCandidates(string hint)
    {
        if (!string.IsNullOrEmpty(_shadersPath))
            yield return _shadersPath;

        foreach (var root in ExpandHint(hint))
        {
            yield return root;
            yield return Path.Combine(root, "Shaders");

            string parent;
            try
            {
                parent = Directory.GetParent(root)?.FullName;
            }
            catch
            {
                parent = null;
            }

            if (string.IsNullOrEmpty(parent))
                continue;
            yield return parent;
            yield return Path.Combine(parent, "Shaders");
        }
    }

    static IEnumerable<string> ExpandHint(string hint)
    {
        if (string.IsNullOrEmpty(hint))
            yield break;

        var path = hint;
        try
        {
            if (File.Exists(path))
                path = Path.GetDirectoryName(path);
        }
        catch
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(path))
            yield return path;
    }

    public static void SetInitialized(bool value)
    {
        Initialized = value;
    }

    public static void Release()
    {
        UiCompositeVertexShader?.Dispose();
        UiCompositeVertexShader = null;
        UiCompositePixelShader?.Dispose();
        UiCompositePixelShader = null;

        if (UiConstantBuffer != null)
        {
            MyManagers.Buffers.Dispose(UiConstantBuffer);
            UiConstantBuffer = null;
        }

        Initialized = false;
    }

    private static VertexShader CompileVertex(SharpDX.Direct3D11.Device device, string file, string entry)
    {
        using var bc = Compile(file, entry, "vs_5_0");
        return new VertexShader(device, bc);
    }

    private static PixelShader CompilePixel(SharpDX.Direct3D11.Device device, string file, string entry)
    {
        using var bc = Compile(file, entry, "ps_5_0");
        return new PixelShader(device, bc);
    }

    private static CompilationResult Compile(string file, string entry, string profile)
    {
        var src = File.ReadAllText(Path.Combine(_shadersPath, file));
        var bc = ShaderBytecode.Compile(src, entry, profile, ShaderFlags.OptimizationLevel3);
        if (bc.Bytecode != null) return bc;

        var msg = $"HDR: compile failed {file} ({entry}, {profile}):\n{bc.Message}";
        VRage.Utils.MyLog.Default.WriteLine(msg);
        throw new Exception(msg);
    }
}
