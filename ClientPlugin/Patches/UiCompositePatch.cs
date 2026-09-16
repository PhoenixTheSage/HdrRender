using System;
using ClientPlugin.Anomaly;
using ClientPlugin.Rendering;
using HarmonyLib;
using SharpDX.Direct3D;
using SharpDX.DXGI;
using SharpDX.Mathematics.Interop;
using VRage.Render11.Common;
using VRage.Render11.Resources;
using VRageMath;
using VRageRender;

namespace ClientPlugin.Patches;

// Persistent UI offscreen, owned by the render thread.
//
// Lifetime: CreateRtv on first use / resize; DisposeHeld on HdrResources.Release.
// RenderMainSprites Prefix points Current at it; ConsumeMainSprites Postfix
// composites then nulls Current. Harmony __state cannot cross the two methods.
//
// Render-thread slot only. Keen records sprites on a Parallel.Start deferred
// RC ("UpdateAndRenderMainSprites"). We ClearRtv / composite on MyImmediateRC.RC
// around that task (Prefix before Start, Consume postfix after Wait). Do not
// touch the immediate context from the sprite worker.
//
// Contract: Consume must null Current after the composite Draw so the next
// Prefix treats the slot as unset. The CreateRtv itself stays in `held`.
internal static class UiOffscreenSlot
{
    public static IRtvTexture Current { get; private set; }
    public static IRtvBindable OverrideTarget { get; private set; }
    static IRtvTexture held;
    static Vector2I heldSize;

    public static IRtvTexture Acquire(int width, int height, string debugName)
    {
        if (width <= 0 || height <= 0)
            return null;
        if (held != null && heldSize.X == width && heldSize.Y == height)
            return held;
        if (held != null)
            MyManagers.RwTextures.DisposeTex(ref held);
        held = MyManagers.RwTextures.CreateRtv(
            debugName, width, height, Format.R8G8B8A8_UNorm_SRgb);
        heldSize = new Vector2I(width, height);
        return held;
    }

    public static void DisposeHeld()
    {
        if (held != null)
            MyManagers.RwTextures.DisposeTex(ref held);
        held = null;
        heldSize = default;
        Current = null;
        OverrideTarget = null;
    }

    // Point Current at this frame's offscreen. OverrideTarget resets: a fresh
    // slot composites to Backbuffer; screenshot callers must SetOverrideTarget
    // after Replace.
    public static void Replace(IRtvTexture tex)
    {
        Current = tex;
        OverrideTarget = null;
    }

    public static void SetOverrideTarget(IRtvBindable target) => OverrideTarget = target;

    // End of composite: drop the "this frame" pointer. The CreateRtv stays
    // until resize or HdrResources.Release — pool BorrowRtv is frame-scoped
    // and cannot be held across Present.
    public static void ReleaseAndClear()
    {
        Current = null;
        OverrideTarget = null;
    }
}

[HarmonyPatch(typeof(MyRender11), nameof(MyRender11.RenderMainSprites), [])]
internal static class RenderMainSpritesPatch
{
    private static bool Prefix()
    {
        if (!Plugin.IsLive)
            return true;

        RenderTraceBind.Begin("Hdr.RenderMainSprites");

        SwapChainReplacer.ReplaceSwapChain();

        var res = MyRender11.ViewportResolution;
        var uiOff = UiOffscreenSlot.Acquire(res.X, res.Y, "HdrOutput.UiOffscreen");
        if (uiOff == null)
            return true;
        UiOffscreenSlot.Replace(uiOff);

        MyImmediateRC.RC.ClearRtv(uiOff, new RawColor4(0, 0, 0, 0));

        var viewport = new MyViewport(res.X, res.Y);
        var scaledViewport = MyRender11.ScaleMainViewport(viewport);

        MyRender11.RenderMainSprites(
            uiOff,
            scaledViewport,
            viewport,
            new Vector2(res.X, res.Y));

        return false;
    }

    // Plugin self-disable on crash. Any exception that bubbles through patched
    // RenderMainSprites (our Prefix, the original method, downstream Postfixes,
    // or other plugins' patches on this method) sets DisabledAfterCrash and
    // flushes to disk. The next launch reads the flag in Plugin.Init and skips
    // patching entirely. User clears the flag in plugin config UI to retry.
    //
    // Save() can throw on IO failure - log and continue so the original render
    // crash exception keeps its place in SE's minidump.
    private static Exception Finalizer(Exception __exception)
    {
        if (__exception == null) return null;

        RenderTraceBind.Dump("Hdr.RenderMainSprites", __exception);

        Config.Current.DisabledAfterCrash = true;
        try
        {
            Settings.ConfigStorage.Save(Config.Current);
            Settings.ConfigStorage.FlushPending(true);
        }
        catch (Exception saveEx)
        {
            VRage.Utils.MyLog.Default.WriteLine($"HDR: Failed to persist DisabledAfterCrash flag: {saveEx.Message}");
        }
        return __exception;
    }
}

[HarmonyPatch(typeof(MyRender11), nameof(MyRender11.RenderMainSprites),
    typeof(IRtvBindable), typeof(MyViewport), typeof(MyViewport), typeof(Vector2), typeof(MyViewport?))]
internal static class RenderMainSprites4Patch
{
    private static bool Prefix(IRtvBindable rtv, MyViewport viewportBound, MyViewport viewportFull, Vector2 size, MyViewport? targetRegion)
    {
        if (!Plugin.IsLive)
            return true;

        // Sentinel: our own re-entry, let the original method run.
        if (ReferenceEquals(rtv, UiOffscreenSlot.Current))
            return true;

        RenderTraceBind.Begin("Hdr.RenderMainSprites");

        // External caller (e.g. screenshot path): hijack the UI render into our offscreen
        // and remember the real target so ConsumeMainSpritesPatch composites onto it.
        // Order matters: Replace() resets OverrideTarget to null as part of its invariant,
        // so SetOverrideTarget MUST run after Replace - otherwise the hijack target is
        // wiped before it takes effect.
        var uiOff = UiOffscreenSlot.Acquire(
            rtv.Size.X, rtv.Size.Y, "HdrOutput.ScreenshotUiOffscreen");
        if (uiOff == null)
            return true;
        MyImmediateRC.RC.ClearRtv(uiOff, new RawColor4(0, 0, 0, 0));
        UiOffscreenSlot.Replace(uiOff);
        UiOffscreenSlot.SetOverrideTarget(rtv);

        // Re-enters this Prefix; the sentinel above lets the call through to the original.
        MyRender11.RenderMainSprites(uiOff, viewportBound, viewportFull, size, targetRegion);

        return false;
    }
}

[HarmonyPatch(typeof(MyRender11), nameof(MyRender11.ConsumeMainSprites))]
internal static class ConsumeMainSpritesPatch
{
    private static void Postfix()
    {
        if (!Plugin.IsLive)
            return;

        RenderTraceBind.Begin("Hdr.ConsumeMainSprites");
        try
        {
            ConsumeMainSpritesCore();
        }
        catch (Exception e)
        {
            RenderTraceBind.Dump("Hdr.ConsumeMainSprites", e);
            throw;
        }
        finally
        {
            RenderTraceBind.End("Hdr.ConsumeMainSprites");
            RenderTraceBind.End("Hdr.RenderMainSprites");
        }
    }

    static void ConsumeMainSpritesCore()
    {
        var uiLayer = UiOffscreenSlot.Current;
        if (uiLayer == null)
            return;

        var rc = MyImmediateRC.RC;
        var device = MyRender11.DeviceInstance;

        var constants = UiBoost.Pack(Config.Current);
        var ctx = device.ImmediateContext;
        var mapped = ctx.MapSubresource(HdrResources.UiConstantBuffer.Resource, 0,
            SharpDX.Direct3D11.MapMode.WriteDiscard, SharpDX.Direct3D11.MapFlags.None);
        SharpDX.Utilities.Write(mapped.DataPointer, ref constants);
        ctx.UnmapSubresource(HdrResources.UiConstantBuffer.Resource, 0);

        rc.SetBlendState(MyBlendStateManager.BlendAlphaPremult);
        rc.SetInputLayout(null);
        rc.SetPrimitiveTopology(PrimitiveTopology.TriangleList);
        rc.SetDepthStencilState(MyDepthStencilStateManager.IgnoreDepthStencil);

        rc.VertexShader.Set(HdrResources.UiCompositeVertexShader);
        rc.PixelShader.Set(HdrResources.UiCompositePixelShader);
        rc.PixelShader.SetConstantBuffer(0, HdrResources.UiConstantBuffer);
        rc.PixelShader.SetSrv(0, uiLayer);
        rc.PixelShader.SetSampler(0, MySamplerStateManager.Point);

        var compositeTarget = UiOffscreenSlot.OverrideTarget ?? MyRender11.Backbuffer;
        rc.SetRtv(compositeTarget);
        // Backbuffer.Size follows ResolutionI after DLSS SetDRS (internal).
        // The offscreen was recorded at ViewportResolution (output). Blit 1:1
        // or Keen GUI and Rich HUD sit in a shrunken island on the swapchain.
        var destW = uiLayer.Size.X;
        var destH = uiLayer.Size.Y;
        if (destW <= 0 || destH <= 0)
        {
            var vp = MyRender11.ViewportResolution;
            destW = vp.X;
            destH = vp.Y;
        }
        rc.SetViewport(0, 0, destW, destH);
        rc.Draw(3, 0);
        rc.SetRtvNull();

        rc.PixelShader.SetSrv(0, null);

        UiOffscreenSlot.ReleaseAndClear();
    }
}
