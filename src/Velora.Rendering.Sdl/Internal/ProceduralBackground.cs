using System.Text;
using Velora.Rendering;

namespace Velora.Rendering.Sdl;

internal static class ProceduralBackground
{
    public const int BandCount = 24;
    public const int GridSpacing = 80;
    public const int BandHeight = 720 / BandCount;

    public static ProceduralBackgroundPalette CreatePalette(string logicalPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logicalPath);
        ulong seed = ComputeStableSeed(logicalPath);
        return new ProceduralBackgroundPalette(
            checked((byte)(42 + (seed % 24))),
            checked((byte)(58 + ((seed >> 8) % 30))),
            checked((byte)(64 + ((seed >> 16) % 28))));
    }

    public static ColorRgba GetBandColor(
        ProceduralBackgroundPalette palette,
        int band,
        byte opacity)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(band, BandCount);
        ArgumentOutOfRangeException.ThrowIfNegative(band);
        float progress = (float)band / (BandCount - 1);
        return new ColorRgba(
            checked((byte)(10 + (palette.AccentR * progress * 0.55f))),
            checked((byte)(14 + (palette.AccentG * progress * 0.48f))),
            checked((byte)(18 + (palette.AccentB * progress * 0.42f))),
            opacity);
    }

    public static ulong ComputeStableSeed(string logicalPath)
    {
        ArgumentNullException.ThrowIfNull(logicalPath);
        const ulong offsetBasis = 14695981039346656037;
        const ulong prime = 1099511628211;
        ulong hash = offsetBasis;
        Span<byte> buffer = stackalloc byte[4];
        foreach (Rune rune in logicalPath.EnumerateRunes())
        {
            int length = rune.EncodeToUtf8(buffer);
            for (int index = 0; index < length; index++)
            {
                hash ^= buffer[index];
                hash *= prime;
            }
        }

        return hash;
    }
}

internal readonly record struct ProceduralBackgroundPalette(byte AccentR, byte AccentG, byte AccentB);
