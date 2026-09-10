using HarmonyLib;
using Sandbox.Engine.Platform.VideoMode;

namespace ClientPlugin.Patches;

[HarmonyPatch(typeof(MyVideoSettingsManager), nameof(MyVideoSettingsManager.GetGraphicsSettingsFromConfig))]
internal static class HqTargetPatch
{
    private static void Postfix(ref MyGraphicsSettings __result)
    {
        if (!Plugin.IsLive)
            return;
        __result.PerformanceSettings.RenderSettings.HqTarget = Config.Current.HqTarget;
    }
}
