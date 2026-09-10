using System;
using System.Collections.Generic;
using ClientPlugin.Rendering;
using HarmonyLib;
using VRageMath;
using VRageRender;

namespace ClientPlugin.Patches;

// Two HDR lifts land on MyBillboard just before verts are packed, then
// restore in a Finalizer so producers and persistent instances do not
// accumulate drift.
//
// 1. Emissive LDR (thrusters, lasers): ColorIntensity > 1 is the producer
//    signal. Multiply by LdrIntensity. Diffuse / UI stay at 1 and are skipped.
// 2. PostPP HUD (Rich HUD Master + Anomaly client, disconnected-player icon):
//    Keen sprites get paper-white / gain / shoulder in UiComposite.hlsl after
//    ConsumeMainSprites. PostPP triangles never enter that offscreen. Apply
//    the same expand to Color (premul RGB). ColorIntensity stays at 1 so
//    Gather's Color *= Intensity does not double the lift.
//
// Keen Gather walks the proxy lists, then GatherInternal packs m_tempBuffer.
// SE-DLSS rebuilds bucket 4 after CopyToRT by calling GatherInternal on a
// fresh snapshot — after this Gather Finalizer already restored Color — so
// the expand must also run on GatherInternal when Saved is empty.
//
// HarmonyPriority.First on Gather: Prism.RenderPerf and similar replace
// Gather; they still read Color / ColorIntensity from these instances.
[HarmonyPatch(typeof(MyBillboardRenderer), nameof(MyBillboardRenderer.Gather))]
[HarmonyPriority(Priority.First)]
internal static class BillboardIntensityScalePatch
{
    internal static readonly List<(MyBillboard b, float origIntensity, Vector4 origColor)> Saved = new(4096);
    // Stock SE's MyTransparentGeometry.AddLineBillboard / AddPointBillboard / etc.
    // intentionally puts the same MyBillboard into both BillboardsWrite AND the
    // persistent linked list when the caller passes persistentBillboards != null
    // (engineer tool highlight, blueprint paste preview, block rotation hints,
    // grid selection box). After swap, the same instance shows up in both
    // BillboardsRead and PersistentBillboards, so naive iteration mutates twice.
    // SavedSet uses reference equality (MyBillboard does not override Equals /
    // GetHashCode) to dedupe by object identity.
    internal static readonly HashSet<MyBillboard> SavedSet = new(4096);
    internal static float LdrK;
    internal static HdrResources.UiCompositeConstants Ui;
    internal static bool UiActive;
    private static readonly Action<MyBillboard> PersistentVisitor = ScaleOne;

    internal static void ScaleOne(MyBillboard bb)
    {
        if (bb == null)
            return;

        if (bb.ColorIntensity > 1.0f)
        {
            if (LdrK <= 1f)
                return;
            if (!SavedSet.Add(bb))
                return;
            Saved.Add((bb, bb.ColorIntensity, bb.Color));
            bb.ColorIntensity *= LdrK;
            return;
        }

        if (!UiActive || bb.BlendType != MyBillboard.BlendTypeEnum.PostPP)
            return;

        if (!SavedSet.Add(bb))
            return;

        Saved.Add((bb, bb.ColorIntensity, bb.Color));
        var color = bb.Color;
        UiBoost.ApplyToPremul(ref color, in Ui);
        bb.Color = color;
    }

    internal static bool PackFromConfig()
    {
        var cfg = Config.Current;
        LdrK = cfg.LdrIntensity;
        Ui = UiBoost.Pack(cfg);
        UiActive = !UiBoost.IsIdentity(in Ui);
        return LdrK > 1f || UiActive;
    }

    internal static void RestoreSaved()
    {
        for (var i = 0; i < Saved.Count; i++)
        {
            var (bb, origIntensity, origColor) = Saved[i];
            bb.ColorIntensity = origIntensity;
            bb.Color = origColor;
        }
        Saved.Clear();
        SavedSet.Clear();
    }

    private static void Prefix()
    {
        if (!Plugin.IsLive)
            return;
        if (!PackFromConfig())
            return;

        foreach (var bb in MyRenderProxy.BillboardsRead)
            ScaleOne(bb);

        var oncePool = MyBillboardRenderer.m_billboardsOncePool;
        var onceCount = oncePool.GetAllocatedCount();
        for (var i = 0; i < onceCount; i++)
            ScaleOne(oncePool.GetAllocatedItem(i));

        MyRenderProxy.ApplyActionOnPersistentBillboards(PersistentVisitor);
    }

    // Finalizer runs in Harmony's try/finally: covers the normal frame plus any
    // exception thrown from Prefix / original Gather / Postfix. A Postfix would
    // be skipped on the exception path, leaving persistent billboards with
    // ColorIntensity or Color permanently lifted — corruption that compounds
    // every time Gather throws.
    //
    // void return (not Exception) keeps Harmony's rethrow IL intact and preserves
    // the original stack trace; returning Exception forces a throw-rewrite.
    private static void Finalizer()
    {
        RestoreSaved();
    }
}

[HarmonyPatch(typeof(MyBillboardRenderer), nameof(MyBillboardRenderer.GatherInternal))]
[HarmonyPriority(Priority.First)]
internal static class BillboardGatherInternalScalePatch
{
    private static void Prefix()
    {
        if (!Plugin.IsLive)
            return;
        // Keen Gather already lifted the lists. Only the SE-DLSS rebuild
        // (GatherInternal after that Finalizer) needs a second lift.
        if (BillboardIntensityScalePatch.Saved.Count > 0)
            return;
        if (!BillboardIntensityScalePatch.PackFromConfig())
            return;

        var n = MyBillboardRenderer.m_billboardCountSafe;
        var buf = MyBillboardRenderer.m_tempBuffer;
        if (buf == null || n <= 0)
            return;
        if (n > buf.Length)
            n = buf.Length;
        for (var i = 0; i < n; i++)
            BillboardIntensityScalePatch.ScaleOne(buf[i]);
    }

    private static void Finalizer()
    {
        BillboardIntensityScalePatch.RestoreSaved();
    }
}
