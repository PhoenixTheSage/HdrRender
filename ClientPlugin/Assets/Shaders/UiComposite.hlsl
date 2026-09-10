Texture2D ui_layer : register(t0);
SamplerState point_sampler : register(s0);

cbuffer Constants : register(b0)
{
    float paper_white;
    float ui_gain;
    float ui_shoulder;
    float ui_peak;
};

struct vs_output
{
    float4 pos : SV_Position;
    float2 uv  : TEXCOORD0;
};

vs_output vs_main(uint id : SV_VertexID)
{
    vs_output o;
    o.uv = float2((id << 1) & 2, id & 2);
    o.pos = float4(o.uv * float2(2, -2) + float2(-1, 1), 0, 1);
    return o;
}

// Inverse-Reinhard shoulder. shoulder = 0 is identity.
float3 expand_ui(float3 n)
{
    float3 contrasted = pow(max(n, 0), 1.0 + ui_shoulder);
    return contrasted / max(1.0 - contrasted * ui_shoulder, 1e-3);
}

float4 ps_main(vs_output input) : SV_Target
{
    float4 ui = ui_layer.Sample(point_sampler, input.uv);

    // SE's sprite pipeline writes premultiplied alpha into the offscreen RT:
    // Sprites.hlsl vs pre-multiplies vertex color (output.color.rgb *= a),
    // ps pre-multiplies sample under PREMULTIPLY_ALPHA, and SpritesManager.Render
    // selects the variant so the final SV_Target0 is premul either way.
    // ConsumeMainSpritesPatch composites with BlendAlphaPremult (Src=One,
    // Dst=InverseSourceAlpha), which already expects a premultiplied src.
    // Multiplying rgb by ui.a again here would square the alpha and crush
    // every translucent UI element (tooltips, HUD fades, modal dimmers).
    //
    // Keen already linearizes GUI textures (MakeSrgb + srgba_to_rgba) and the
    // offscreen is _UNorm_SRgb, so ui.rgb is linear premul. Paper white is the
    // scene SDR anchor (scRGB 1 = 80 nits). Gain / shoulder are HUD-only:
    // pow restores contrast, inverse Reinhard lifts near-white chrome above
    // paper white, then ui_peak caps against the display.
    float a = ui.a;
    if (a <= 1e-5)
        return 0;

    float3 n = saturate(ui.rgb / a);
    float3 hdr = expand_ui(n) * paper_white * ui_gain;
    hdr = min(hdr, ui_peak);
    return float4(hdr * a, a);
}
