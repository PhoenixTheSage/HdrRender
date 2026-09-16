# Graph Report - HdrRender  (2026-09-16)

## Corpus Check
- Corpus is ~14,353 words - fits in a single context window. You may not need a graph.

## Summary
- 538 nodes · 795 edges · 31 communities (28 shown, 3 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 15 edges (avg confidence: 0.87)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- Config
- Swap Chain Patch
- Community 2
- Render Trace Bind
- Rich Hud Options
- Agent Instructions
- H L S L
- Plugin
- Button
- Hdr Resources
- Binary Writer
- Color
- Config Storage
- Custom Texture Patch
- Anomaly Hook
- Used Implicitly
- Settings Generator
- Tools
- Dropdown
- Client Plugin
- Button 2
- Checkbox
- Color 2
- Element
- Separator
- Textbox
- Binding
- None
- Control
- Deploy.sh
- verify props.sh

## God Nodes (most connected - your core abstractions)
1. `RichHudOptions` - 31 edges
2. `HdrResources` - 21 edges
3. `Config` - 20 edges
4. `SettingsGenerator` - 17 edges
5. `Plugin` - 16 edges
6. `Control` - 16 edges
7. `BillboardIntensityScalePatch` - 15 edges
8. `ClientPlugin.Settings.Elements` - 15 edges
9. `IElement` - 15 edges
10. `KeybindAttribute` - 14 edges

## Surprising Connections (you probably didn't know these)
- `HLSL Skill README (.agents)` --semantically_similar_to--> `HLSL Skill README (.cursor)`  [INFERRED] [semantically similar]
  .agents/skills/a5c-ai-babysitter-hlsl/README.md → .cursor/skills/a5c-ai-babysitter-hlsl/README.md
- `HLSL Skill (.agents)` --semantically_similar_to--> `HLSL Skill (.cursor)`  [INFERRED] [semantically similar]
  .agents/skills/a5c-ai-babysitter-hlsl/SKILL.md → .cursor/skills/a5c-ai-babysitter-hlsl/SKILL.md
- `Vertex and Pixel Shaders` --conceptually_related_to--> `hdr.tonemap AfterUpscale Display Program`  [INFERRED]
  .cursor/skills/a5c-ai-babysitter-hlsl/SKILL.md → README.md
- `Rich HUD Deferred Config Save` --conceptually_related_to--> `Rich HUD Master`  [INFERRED]
  AGENTS.md → README.md
- `Anomaly Owns Shared Rendering Gaps` --conceptually_related_to--> `Anomaly Shader Framework`  [INFERRED]
  AGENTS.md → README.md

## Import Cycles
- None detected.

## Hyperedges (group relationships)
- **Mirrored HLSL Skill Copies** — agents_skills_a5c_ai_babysitter_hlsl_readme, agents_skills_a5c_ai_babysitter_hlsl_skill, cursor_skills_a5c_ai_babysitter_hlsl_readme, cursor_skills_a5c_ai_babysitter_hlsl_skill [INFERRED 0.95]
- **HDR Display Pipeline** — readme_scrgb_swapchain, readme_hdr_tonemap, readme_bt2390_tonemap, readme_se_dlss, readme_lbuffer, readme_hdrcolor [EXTRACTED 1.00]
- **Anomaly Pack Contract** — agents_anomaly_own_the_gap, readme_anomaly_shader_framework, agents_isolatedmix, agents_docs_extensibility, agents_wiki_framework_gaps [EXTRACTED 1.00]

## Communities (31 total, 3 thin omitted)

### Community 0 - "Config"
Cohesion: 0.06
Nodes (25): Attribute, LabelAttribute, SupportedTypes, Action, Func, List, Type, SliderAttribute (+17 more)

### Community 1 - "Swap Chain Patch"
Cohesion: 0.08
Nodes (22): CreateSwapChainPatch, PresentForceExitFullscreenPatch, SwapChainExclusive, ThisPresent, SwapChainPresentTearingPatch, SwapChainReplacer, AllowTearingSupported, CreationFlags (+14 more)

### Community 2 - "Community 2"
Cohesion: 0.09
Nodes (17): b, BillboardGatherInternalScalePatch, BillboardIntensityScalePatch, Action, List, UiCompositeConstants, Vector4, UiCompositeConstants (+9 more)

### Community 3 - "Render Trace Bind"
Cohesion: 0.12
Nodes (15): RenderTraceBind, Exception, MethodInfo, ConsumeMainSpritesPatch, RenderMainSprites4Patch, RenderMainSpritesPatch, UiOffscreenSlot, Current (+7 more)

### Community 5 - "Agent Instructions"
Cohesion: 0.09
Nodes (28): Agent Instructions, Anomaly Owns Shared Rendering Gaps, CometWorks Skills, Docs/Extensibility.md Slice AI, IsolatedMix Energy, March LOD, Rich HUD Deferred Config Save, se-dev Skill (+20 more)

### Community 6 - "H L S L"
Cohesion: 0.08
Nodes (28): HLSL Skill README (.agents), GPU Compute, DirectX Shaders, GLSL Skill, HLSL, Shader Optimization Skill, Unreal/Unity Shader Authoring, HLSL Skill (.agents) (+20 more)

### Community 7 - "Plugin"
Cohesion: 0.12
Nodes (13): Plugin, DisplayHdrSupported, IsLive, StatusInfo, StatusTitle, AnomalyTerminalHook, Installed, MethodInfo (+5 more)

### Community 8 - "Button"
Cohesion: 0.08
Nodes (21): Button, Config, AllowTearing, BlackLift, DisabledAfterCrash, DisplayInfo, Enabled, ForceEnable (+13 more)

### Community 9 - "Hdr Resources"
Cohesion: 0.14
Nodes (14): HdrResources, Initialized, UiCompositePixelShader, UiCompositeVertexShader, UiConstantBuffer, Device, Format, IEnumerable (+6 more)

### Community 10 - "Binary Writer"
Cohesion: 0.15
Nodes (9): BinaryWriter, ScreenshotPatch, IntPtr, ExrWriter, IntPtr, FileFormat, IResource, Rgba32 (+1 more)

### Community 11 - "Color"
Cohesion: 0.14
Nodes (17): ColorAttribute, SupportedTypes, Action, Color, Func, List, Type, ControlButtonData (+9 more)

### Community 12 - "Config Storage"
Cohesion: 0.12
Nodes (10): ConfigStorage, ConfigFilePath, Config, SettingsScreen, Func, List, MyGuiControlBase, Vector2 (+2 more)

### Community 13 - "Custom Texture Patch"
Cohesion: 0.16
Nodes (10): CustomTextureFormatPatch, IEnumerable, GpuParticleIntensityScalePatch, HqTargetPatch, CodeInstruction, ClientPlugin.Patches, HarmonyPatch, HarmonyTranspiler (+2 more)

### Community 14 - "Anomaly Hook"
Cohesion: 0.15
Nodes (9): AnomalyHook, HasDisplayTenant, HasUpscaleConsumer, PackRegistered, RegistryFound, MethodInfo, TonemapUniformsPatch, ClientPlugin.Anomaly (+1 more)

### Community 15 - "Used Implicitly"
Cohesion: 0.13
Nodes (14): UsedImplicitly, Layout, SettingsPanelSize, Func, List, MyGuiControlBase, Vector2, Simple (+6 more)

### Community 16 - "Settings Generator"
Cohesion: 0.16
Nodes (11): AttributeInfo, SettingsGenerator, ActiveLayout, Dialog, Action, Func, List, MethodInfo (+3 more)

### Community 17 - "Tools"
Cohesion: 0.27
Nodes (4): Tools, Color, Match, Regex

### Community 18 - "Dropdown"
Cohesion: 0.28
Nodes (6): DropdownAttribute, SupportedTypes, Action, Func, List, Type

### Community 19 - "Client Plugin"
Cohesion: 0.25
Nodes (6): ClientPlugin, net10.0, net48, Krafs.Publicizer (2.3.0), Lib.Harmony (2.4.2), Mono.Cecil (0.11.6)

### Community 20 - "Button 2"
Cohesion: 0.29
Nodes (6): ButtonAttribute, SupportedTypes, Action, Func, List, Type

### Community 21 - "Checkbox"
Cohesion: 0.29
Nodes (6): CheckboxAttribute, SupportedTypes, Action, Func, List, Type

### Community 22 - "Color 2"
Cohesion: 0.36
Nodes (3): ClientPlugin.Settings.Tools, ClientPlugin.Settings.Elements, ClientPlugin.Settings.Layouts

### Community 23 - "Element"
Cohesion: 0.29
Nodes (6): IElement, SupportedTypes, Action, Func, List, Type

### Community 24 - "Separator"
Cohesion: 0.29
Nodes (6): SeparatorAttribute, SupportedTypes, Action, Func, List, Type

### Community 25 - "Textbox"
Cohesion: 0.29
Nodes (6): TextboxAttribute, SupportedTypes, Action, Func, List, Type

### Community 26 - "Binding"
Cohesion: 0.39
Nodes (3): Binding, IMyInput, MyKeys

### Community 27 - "None"
Cohesion: 0.29
Nodes (5): None, SettingsPanelSize, List, MyGuiControlBase, Vector2

### Community 28 - "Control"
Cohesion: 0.40
Nodes (4): Control, MyGuiControlBase, Vector2, MyGuiDrawAlignEnum

## Knowledge Gaps
- **84 isolated node(s):** `RegistryFound`, `PackRegistered`, `HasDisplayTenant`, `HasUpscaleConsumer`, `net10.0` (+79 more)
  These have ≤1 connection - possible missing edges or undocumented components. (Counts symbols only; 209 node(s) total have ≤1 connection when file, concept and rationale nodes are included.)
- **3 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `ClientPlugin.Rendering` connect `Community 2` to `Config`, `Swap Chain Patch`, `Binary Writer`, `Render Trace Bind`?**
  _High betweenness centrality (0.209) - this node is a cross-community bridge._
- **Why does `SettingsGenerator` connect `Settings Generator` to `Config Storage`, `Control`, `Used Implicitly`, `Plugin`?**
  _High betweenness centrality (0.187) - this node is a cross-community bridge._
- **Why does `ClientPlugin.Anomaly` connect `Anomaly Hook` to `Config`, `Render Trace Bind`?**
  _High betweenness centrality (0.138) - this node is a cross-community bridge._
- **What connects `RegistryFound`, `PackRegistered`, `HasDisplayTenant` to the rest of the system?**
  _84 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Config` be split into smaller, more focused modules?**
  _Cohesion score 0.06456456456456457 - nodes in this community are weakly interconnected._
- **Should `Swap Chain Patch` be split into smaller, more focused modules?**
  _Cohesion score 0.0761904761904762 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.09247311827956989 - nodes in this community are weakly interconnected._