using System;
using VRageMath;

namespace ClientPlugin.Rendering;

// Paper-white HUD expand shared by Keen sprite composite and PostPP billboards
// (Rich HUD, disconnected-player icon). Shader and CPU must stay bit-identical
// on the expand: pow(n, 1+shoulder) / (1 - n*shoulder), then paper*gain, min peak.
internal static class UiBoost
{
    public static HdrResources.UiCompositeConstants Pack(Config cfg)
    {
        var paper = Math.Max(cfg.PaperWhite, 1f);
        var gain = Math.Max(cfg.UiBrightness, 0f);
        var shoulder = Math.Max(0f, Math.Min(cfg.UiShoulder, 0.85f));
        var peakNits = paper * Math.Max(gain, 1e-3f);
        if (shoulder > 0f)
            peakNits = Math.Min(cfg.PeakBrightness, peakNits * (1f + 2.5f * shoulder));

        return new HdrResources.UiCompositeConstants
        {
            PaperWhite = paper / 80f,
            UiGain = gain,
            UiShoulder = shoulder,
            UiPeak = peakNits / 80f,
        };
    }

    public static bool IsIdentity(in HdrResources.UiCompositeConstants k) =>
        k.UiShoulder <= 0f &&
        Math.Abs(k.PaperWhite * k.UiGain - 1f) < 1e-4f;

    // Premultiplied linear RGB (Rich HUD GetBbColor / Keen sprite offscreen).
    // Alpha is unchanged; RGB is unpremultiplied, expanded, recapped, premultiplied.
    public static void ApplyToPremul(ref Vector4 color, in HdrResources.UiCompositeConstants k)
    {
        var a = color.W;
        if (a <= 1e-5f)
            return;

        var invA = 1f / a;
        var scale = k.PaperWhite * k.UiGain;
        color.X = ExpandChannel(color.X * invA, k.UiShoulder, scale, k.UiPeak) * a;
        color.Y = ExpandChannel(color.Y * invA, k.UiShoulder, scale, k.UiPeak) * a;
        color.Z = ExpandChannel(color.Z * invA, k.UiShoulder, scale, k.UiPeak) * a;
    }

    static float ExpandChannel(float n, float shoulder, float scale, float peak)
    {
        n = n < 0f ? 0f : n > 1f ? 1f : n;
        if (shoulder > 0f)
        {
            var contrasted = (float)Math.Pow(n, 1.0 + shoulder);
            n = contrasted / Math.Max(1f - contrasted * shoulder, 1e-3f);
        }

        n *= scale;
        return n < peak ? n : peak;
    }
}
