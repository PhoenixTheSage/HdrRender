# HdrRender changelog

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
