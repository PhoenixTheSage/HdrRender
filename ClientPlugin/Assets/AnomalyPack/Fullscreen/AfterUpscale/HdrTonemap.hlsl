#include <AnomalyFullscreen.hlsli>

#define paper_white           AnomalyPassUniform0.x
#define peak                  AnomalyPassUniform0.y
#define source_peak           AnomalyPassUniform0.z
#define bloom_mult            AnomalyPassUniform0.w

#define bloom_dirt_ratio      AnomalyPassUniform1.x
#define grain_strength        AnomalyPassUniform1.y
#define grain_amount          AnomalyPassUniform1.z
#define grain_size            AnomalyPassUniform1.w

#define contrast              AnomalyPassUniform2.x
#define brightness            AnomalyPassUniform2.y
#define saturation            AnomalyPassUniform2.z
#define brightness_r          AnomalyPassUniform2.w

#define brightness_g          AnomalyPassUniform3.x
#define brightness_b          AnomalyPassUniform3.y
#define vibrance              AnomalyPassUniform3.z
#define sepia_strength        AnomalyPassUniform3.w

#define light_color           AnomalyPassUniform4.xyz
#define frame_time            AnomalyPassUniform4.w

#define dark_color            AnomalyPassUniform5.xyz
#define black_lift            AnomalyPassUniform5.w

#define disable_postprocess   AnomalyPassUniform6.x
#define needs_alpha_luminance AnomalyPassUniform6.y

#define RNG_IA 16807
#define RNG_IM 2147483647
#define RNG_AM (1.0f / float(RNG_IM))
#define RNG_IQ 127773u
#define RNG_IR 2836
#define RNG_MASK 123459876

struct random_generator
{
    int seed;

    void set_seed(uint value)
    {
        seed = int(value);
        cycle();
    }

    float get_float()
    {
        cycle();
        return RNG_AM * seed;
    }

    void cycle()
    {
        seed ^= RNG_MASK;
        int k = seed / RNG_IQ;
        seed = RNG_IA * (seed - k * RNG_IQ) - RNG_IR * k;
        if (seed < 0)
            seed += RNG_IM;
        seed ^= RNG_MASK;
    }
};

float get_relative_luminance(float3 rgb)
{
    return dot(rgb, float3(0.2126, 0.7152, 0.0722));
}

float to_grayscale(float3 color)
{
    return dot(float3(0.3, 0.59, 0.11), color);
}

static const float pq_m1 = 0.1593017578125;
static const float pq_m2 = 78.84375;
static const float pq_c1 = 0.8359375;
static const float pq_c2 = 18.8515625;
static const float pq_c3 = 18.6875;

float linear_to_pq(float l)
{
    float y    = max(l / 125.0, 0.0);
    float y_m1 = pow(y, pq_m1);
    return pow((pq_c1 + pq_c2 * y_m1) / (1.0 + pq_c3 * y_m1), pq_m2);
}

float pq_to_linear(float pq)
{
    float v_m2 = pow(max(pq, 0.0), 1.0 / pq_m2);
    float num  = max(v_m2 - pq_c1, 0.0);
    float den  = max(pq_c2 - pq_c3 * v_m2, 1e-6);
    float y    = pow(num / den, 1.0 / pq_m1);
    return y * 125.0;
}

float3 apply_grain(float3 source, uint2 texel)
{
    if (grain_strength <= 0)
        return source;

    random_generator rng;
    float rounding = 1;
    int gs_size = int(grain_size);

    if (gs_size > 0)
    {
        int gs = gs_size * 2 + 1;
        float2 dist = (float2)(texel % gs) - gs_size;
        rounding = 1 - dot(dist, dist) / (gs_size * gs_size * 2.0f);
        rng.set_seed(((texel.x + gs) / gs) * ((texel.y + gs) / gs) * int(frame_time * 1000));
    }
    else
    {
        rng.set_seed(texel.x * texel.y * int(frame_time * 1000));
    }

    source -= saturate(grain_amount - rng.get_float()) * rounding * grain_strength;
    return source;
}

float3 apply_basic_filters(float3 color)
{
    float br = brightness * brightness_r;
    float bg = brightness * brightness_g;
    float bb = brightness * brightness_b;
    float4x4 brightness_mat = float4x4(
        br, 0, 0, 0,
         0,bg, 0, 0,
         0, 0,bb, 0,
         0, 0, 0, 1);

    float4x4 contrast_mat = float4x4(
        contrast, 0, 0, 0,
        0, contrast, 0, 0,
        0, 0, contrast, 0,
        -0.5 * contrast + 0.5, -0.5 * contrast + 0.5, -0.5 * contrast + 0.5, 1);

    const float rw = 0.3086;
    const float gw = 0.6094;
    const float bw = 0.0820;
    float s = saturation;
    float4x4 sat_mat = float4x4(
        (1 - s)*rw + s, (1 - s)*rw,     (1 - s)*rw,     0,
        (1 - s)*gw,     (1 - s)*gw + s, (1 - s)*gw,     0,
        (1 - s)*bw,     (1 - s)*bw,     (1 - s)*bw + s, 0,
        0,              0,              0,              1);

    float4x4 m = mul(mul(brightness_mat, contrast_mat), sat_mat);
    return mul(float4(color, 1), m).rgb;
}

float3 apply_vibrance(float3 rgb)
{
    float lum = get_relative_luminance(rgb);
    float minc = min(min(rgb.r, rgb.g), rgb.b);
    float maxc = max(max(rgb.r, rgb.g), rgb.b);
    // Clamp to the SDR [0,1] range the vibrance curve is designed for. In
    // paper-white-normalized space a bright saturated highlight
    // can have maxc-minc > 1, which drives s outside valid range.
    float sat = saturate(maxc - minc);
    float s = 1.0 + (vibrance * (1.0 - (sign(vibrance) * sat)));
    return lerp(lum, rgb, s);
}

float3 apply_sepia(float3 color)
{
    float gray = saturate(to_grayscale(color));
    float3 sepia = lerp(dark_color, light_color, gray);
    return lerp(color, sepia, sepia_strength);
}

float3 apply_channel_soft_clip(float3 color)
{
    float max_c = max(max(color.r, color.g), color.b);
    float start = peak * 0.8;

    if (max_c <= start)
        return color;

    float x = max_c - start;
    float d = peak - start;

    float compressed = peak - d * d / (x + d);
    return color * (compressed / max_c);
}

float4 grade_tonemap(uint2 texel, float2 uv)
{
    float3 source = AnomalySceneColor.Load(int3(texel, 0)).xyz;

    source = apply_grain(source, texel);
    source = clamp(source, 0, 65504);

    float3 color = source;

    if (disable_postprocess < 0.5)
    {
        color *= exp2(AnomalyAvgLuminance.Load(int3(0, 0, 0)).g);
        float dirt = AnomalyDirt.SampleLevel(AnomalyLinearSampler, uv, 0) * bloom_dirt_ratio + (1 - bloom_dirt_ratio);
        color += AnomalyBloom.SampleLevel(AnomalyLinearSampler, uv, 0).xyz * bloom_mult * dirt;
    }

    color = clamp(color, 0, 65504);
    if (any(isnan(color)) || any(isinf(color)))
        color = 0;

    float lum = max(get_relative_luminance(color), 1e-6);

    float display_peak_pq = linear_to_pq(peak);
    float source_peak_pq  = max(linear_to_pq(source_peak), 1e-6);
    float max_lum_norm    = saturate(display_peak_pq / source_peak_pq);
    float ks              = clamp(1.5 * max_lum_norm - 0.5, 0.0, 1.0);

    float lum_pq = linear_to_pq(lum);
    float e      = saturate(lum_pq / source_peak_pq);

    float one_minus_e    = 1.0 - e;
    float one_minus_e_sq = one_minus_e * one_minus_e;
    e = e + black_lift * one_minus_e_sq * one_minus_e_sq;

    float e_out;
    if (e < ks)
    {
        e_out = e;
    }
    else
    {
        float t  = (e - ks) / max(1.0 - ks, 1e-6);
        float t2 = t * t;
        float t3 = t2 * t;
        e_out =  (2.0 * t3 - 3.0 * t2 + 1.0) * ks
              +  (t3 - 2.0 * t2 + t) * (1.0 - ks)
              +  (-2.0 * t3 + 3.0 * t2) * max_lum_norm;
    }

    float mapped_lum_pq = e_out * source_peak_pq;
    float mapped_lum    = pq_to_linear(mapped_lum_pq);
    float3 hdr          = color * (mapped_lum / lum);

    float3 normalized = hdr / paper_white;
    normalized = apply_basic_filters(normalized);
    normalized = apply_vibrance(normalized);
    normalized = apply_sepia(normalized);
    hdr = max(normalized * paper_white, 0);

    hdr = apply_channel_soft_clip(hdr);

    float out_lum = get_relative_luminance(hdr);
    float alpha = needs_alpha_luminance > 0.5 ? (out_lum / (1.0 + out_lum)) : 1.0;

    return float4(hdr, alpha);
}

float4 __pixel_shader(float4 pos : SV_Position, float2 uv : TEXCOORD0) : SV_Target
{
    return grade_tonemap(uint2(pos.xy), uv);
}

#ifdef ANOMALY_FULLSCREEN_COMPUTE
[numthreads(8, 8, 1)]
void __compute_shader(uint3 id : SV_DispatchThreadID)
{
    uint w, h;
    AnomalyComputeDest.GetDimensions(w, h);
    if (id.x >= w || id.y >= h)
        return;

    float2 uv = (float2(id.xy) + 0.5) / float2(w, h);
    AnomalyComputeDest[id.xy] = grade_tonemap(id.xy, uv);
}
#endif
