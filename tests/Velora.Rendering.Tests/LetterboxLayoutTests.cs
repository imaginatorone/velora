using System.Numerics;
using Velora.Rendering;

namespace Velora.Rendering.Tests;

public sealed class LetterboxLayoutTests
{
    [Fact]
    public void NativeAspectFillsOutput()
    {
        LetterboxLayout layout = LetterboxLayout.Calculate(new PixelSize(1920, 1080), LogicalSize.VeloraDefault);

        Assert.Equal(1.5, layout.Scale, precision: 8);
        Assert.Equal(new RectD(0, 0, 1920, 1080), layout.Viewport);
    }

    [Fact]
    public void WiderOutputProducesHorizontalLetterboxMargins()
    {
        LetterboxLayout layout = LetterboxLayout.Calculate(new PixelSize(2560, 1080), LogicalSize.VeloraDefault);

        Assert.Equal(1.5, layout.Scale, precision: 8);
        Assert.Equal(new RectD(320, 0, 1920, 1080), layout.Viewport);
    }

    [Fact]
    public void PortraitOutputProducesVerticalLetterboxMargins()
    {
        LetterboxLayout layout = LetterboxLayout.Calculate(new PixelSize(1080, 1920), LogicalSize.VeloraDefault);

        Assert.Equal(0.84375, layout.Scale, precision: 8);
        Assert.Equal(new RectD(0, 656.25, 1080, 607.5), layout.Viewport);
    }

    [Fact]
    public void CoordinateMappingRoundTripsInsideViewport()
    {
        LetterboxLayout layout = LetterboxLayout.Calculate(new PixelSize(2560, 1080), LogicalSize.VeloraDefault);
        Vector2 logical = new(640, 360);

        Vector2 output = layout.LogicalToOutput(logical);

        Assert.Equal(new Vector2(1280, 540), output);
        Assert.True(layout.TryOutputToLogical(output, out Vector2 roundTrip));
        Assert.Equal(logical, roundTrip);
        Assert.False(layout.TryOutputToLogical(new Vector2(100, 540), out _));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void SurfaceRejectsInvalidScale(double scale)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SurfaceDescriptor(new PixelSize(1280, 720), scale));
    }

    [Fact]
    public void RejectsDefaultStructSizes()
    {
        Assert.Throws<ArgumentException>(() => LetterboxLayout.Calculate(default, LogicalSize.VeloraDefault));
        Assert.Throws<ArgumentException>(() => LetterboxLayout.Calculate(new PixelSize(1280, 720), default));
        Assert.Throws<ArgumentException>(() => new SurfaceDescriptor(default));
    }
}
