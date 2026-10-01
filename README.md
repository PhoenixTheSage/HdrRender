# HDR Render

Pulsar plugin that depends on **Anomaly Shader Framework**. It keeps the flip-model scRGB swapchain, UI paper-white composite, HQ lighting buffer, emissive boosts, and screenshots in Harmony. BT.2390 tonemap is an Anomaly AfterUpscale **Display** program (`hdr.tonemap`).

## Requirements

- [Anomaly](https://github.com/PhoenixTheSage/Anomaly) (`A9C29274-E447-49EE-881B-C980E6D190FD`) — Pulsar `DependencyIds`
- Anomaly must expose `ShaderPackRegistry.TryRegister(id, root, requiredFullscreenProgramId, out error)`. Older builds leave HDR inactive with an update message. Registration verifies `hdr.tonemap` before render patches are installed; acceptance can precede Anomaly initialization and shader compilation.
- Windows HDR on the current display (or **Force enable**, restart required)
- Optional: [Rich HUD Master](https://steamcommunity.com/sharedfiles/filedetails/?id=1965654081) for **Anomaly Shaders → HDR → Settings**. Pulsar MyGui remains the fallback

The master HDR checkbox needs a **restart**. Swapchain replace is one-shot and is not undone at runtime.

## With SE-DLSS

DLSS evaluates pre-grade HDR (`hdrColor`) when a Display tenant is live — into an fp16 dest with NGX `IsHDR` — then AfterUpscale runs BT.2390 on `upscaledColor` plus captured bloom / average luminance / dirt. Keen SDR tonemap does not run. PostPP Rich HUD billboards draw onto the presented dest (paper-white expand at Gather / GatherInternal so a DLSS rebuild still gets the lift). Without DLSS, Anomaly skips Keen SDR and the Display program grades native-res `LBuffer` into a dest `DrawGameScene` can copy (fp16 when Keen’s 8-bit custom texture would clip BT.2390 values above 1).

## Settings

Peak, paper white, UI brightness, UI shoulder, source peak, black lift, and LDR intensity apply immediately while HDR is live. UI brightness and shoulder cover Keen sprites **and** Rich HUD PostPP overlays (Anomaly terminal, other RHUD clients). HQ target and low-latency apply immediately. Allow tearing, Force enable, and the HDR master switch apply on the next launch.

Allow tearing is DXGI VRR for **Windowed** or **Fullscreen Window**. Exclusive **Fullscreen** (`SetFullscreenState(true)`) cannot present with `DXGI_PRESENT_ALLOW_TEARING` — G-Sync/FreeSync in the driver still applies in that mode without this checkbox. The Present path ignores a live toggle until the swapchain is recreated with the creation flag.
