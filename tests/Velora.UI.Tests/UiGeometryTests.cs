using System.Numerics;
using Velora.Rendering;

namespace Velora.UI.Tests;

public sealed class UiGeometryTests
{
    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void LengthRejectsInvalidValues(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => UiLength.Logical(value));
        Assert.Throws<ArgumentOutOfRangeException>(() => UiLength.Fraction(value));
    }

    [Fact]
    public void AnchorAndPaddingResolveInLogicalCoordinates()
    {
        UiViewport viewport = new(LogicalSize.VeloraDefault);
        UiLayout layout = new(
            UiLength.Logical(200),
            UiLength.Logical(100),
            UiAnchor.BottomRight,
            offset: new Vector2(-10, -20),
            padding: new InsetsD(10, 5, 20, 15));

        RectD bounds = layout.Resolve(viewport.Bounds, viewport);

        Assert.Equal(new RectD(1070, 600, 200, 100), bounds);
        Assert.Equal(new RectD(1080, 605, 170, 80), layout.ContentBounds(bounds));
    }

    [Fact]
    public void RelativeOffsetsProduceResponsiveRows()
    {
        UiViewport viewport = new(new LogicalSize(1000, 600));
        UiLayout layout = new(
            UiLength.Fraction(0.25),
            UiLength.Logical(40),
            relativeOffset: new Vector2(0.5f, 0));

        Assert.Equal(new RectD(500, 0, 250, 40), layout.Resolve(viewport.Bounds, viewport));
    }

    [Fact]
    public void SafeAreaTargetUsesLogicalInsets()
    {
        UiViewport viewport = new(new LogicalSize(1280, 720), new InsetsD(40, 20, 60, 30));
        UiLayout layout = new(
            UiLength.Fraction(1),
            UiLength.Fraction(1),
            target: UiLayoutTarget.SafeArea);

        Assert.Equal(new RectD(40, 20, 1180, 670), layout.Resolve(viewport.Bounds, viewport));
    }

    [Fact]
    public void SurfaceSafeAreaConvertsPixelsThroughLetterboxScale()
    {
        SurfaceDescriptor surface = new(
            new PixelSize(1920, 1080),
            scaleFactor: 3,
            safeArea: new InsetsD(150, 75, 90, 60));

        UiViewport viewport = UiViewport.FromSurface(surface, LogicalSize.VeloraDefault);

        Assert.Equal(100, viewport.SafeArea.Left, precision: 8);
        Assert.Equal(50, viewport.SafeArea.Top, precision: 8);
        Assert.Equal(60, viewport.SafeArea.Right, precision: 8);
        Assert.Equal(40, viewport.SafeArea.Bottom, precision: 8);
    }

    [Fact]
    public void LetterboxMarginsAbsorbSafeInsetsOutsideLogicalViewport()
    {
        SurfaceDescriptor surface = new(
            new PixelSize(2560, 1080),
            safeArea: new InsetsD(200, 0, 200, 0));

        UiViewport viewport = UiViewport.FromSurface(surface, LogicalSize.VeloraDefault);

        Assert.Equal(InsetsD.Zero, viewport.SafeArea);
    }

    [Fact]
    public void EquivalentPhysicalInsetsRemainDpiNeutral()
    {
        UiViewport standard = UiViewport.FromSurface(
            new SurfaceDescriptor(new PixelSize(1280, 720), safeArea: new InsetsD(40, 20, 40, 20)),
            LogicalSize.VeloraDefault);
        UiViewport highDpi = UiViewport.FromSurface(
            new SurfaceDescriptor(
                new PixelSize(2560, 1440),
                scaleFactor: 2,
                safeArea: new InsetsD(80, 40, 80, 40)),
            LogicalSize.VeloraDefault);

        Assert.Equal(standard.SafeArea, highDpi.SafeArea);
    }

    [Fact]
    public void ViewportRejectsSafeAreaThatConsumesLogicalSpace()
    {
        Assert.Throws<ArgumentException>(
            () => new UiViewport(new LogicalSize(100, 100), new InsetsD(50, 0, 50, 0)));
    }
}
