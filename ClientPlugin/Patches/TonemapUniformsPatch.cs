using ClientPlugin.Anomaly;
using HarmonyLib;
using VRageRender;

namespace ClientPlugin.Patches;

/// <summary>
/// Writes Display uniforms after Anomaly captures bloom/avgLum (First+100)
/// and before SE-DLSS may skip Keen <c>Run</c> (First). Does not replace tonemap.
/// </summary>
[HarmonyPatch(typeof(MyToneMapping), nameof(MyToneMapping.Run))]
[HarmonyPriority(Priority.First + 50)]
internal static class TonemapUniformsPatch
{
    private static void Prefix()
    {
        if (!Plugin.IsLive)
            return;
        AnomalyHook.WriteUniforms();
    }
}
