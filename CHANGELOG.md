# HdrRender changelog

## Unreleased

- On Vulkan, register the Display pack and write its uniforms through Anomaly's
  portable TonemapInputs, RenderClock and FullscreenPassRegistry facades. The
  renderer grades it at the tone position into FP16 on an HDR swapchain.
- Apply paper white, UI brightness, UI shoulder and peak to the Vulkan HUD
  through DisplayServices.SetHdrUi, and clear the transform when HDR is disabled.
- On Vulkan, request AllowTearing and LowLatencyMode from the renderer (applied
  live), keep F4 screenshots as EXR beside the paper-white SDR PNG, and show the
  monitor's peak/full-frame luminance, bit depth and Windows HDR state in the
  status.

## 1.3.2 — 2026-10-23

- Honor DisabledAfterCrash on the Vulkan presentation request and release that request on disposal.
- Identify Vulkan as presentation-only while grading/UI controls remain blocked on Anomaly's shared display contract; record the missing capabilities for later host work.

## 1.3.1 — 2026-10-14

- Load the DX11 companion against the live Pulsar core identity and reset the resolver during disposal.
- Keep companion calls behind the operational renderer route and expose readable initialization failures and active-renderer status.
- Build/deploy matched companions with separate intermediates and valid game access grants; remove unnecessary Publicizer use from the core.
- Register diagnostics independently on a dedicated Status page and keep renderer checks on the correct shared identity.

## 1.3.0 — previous release

Release 1.3.0 Vulkan FP16 display path

Version: 1.2.1 -> 1.3.0 (minor).

Changelog:
- Move the DX11 AfterUpscale grade into HdrOutput.Dx11.dll.
- The Pulsar plugin requests an FP16 swapchain through Anomaly display services when the route is Vulkan.
- Shared-bus registration stays on the core assembly.

Co-authored-by: Cursor <cursoragent@cursor.com>
