using System.Windows.Media;
using FluentAssertions;
using mPrismaMapsWPF.Helpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class ColorHelperTests
{
    [Theory]
    [InlineData(1, 255, 0, 0)]   // Red
    [InlineData(2, 255, 255, 0)] // Yellow
    [InlineData(3, 0, 255, 0)]   // Lime
    [InlineData(5, 0, 0, 255)]   // Blue
    [InlineData(7, 255, 255, 255)] // White
    public void AciToColor_KnownIndices_ReturnsExpectedColor(short index, byte r, byte g, byte b)
    {
        var result = ColorHelper.AciToColor(index);
        result.R.Should().Be(r);
        result.G.Should().Be(g);
        result.B.Should().Be(b);
    }

    [Fact]
    public void AciToColor_OutOfRangeIndex_ReturnsWhite()
    {
        var result = ColorHelper.AciToColor(999);
        result.Should().Be(Colors.White);
    }

    [Fact]
    public void AciToColor_NegativeIndex_ReturnsWhite()
    {
        var result = ColorHelper.AciToColor(-1);
        result.Should().Be(Colors.White);
    }

    [Fact]
    public void GetEntityColor_WithDirectAciIndex_ReturnsMatchingColor()
    {
        var line = new ACadSharp.Entities.Line { Color = new ACadSharp.Color(1) };
        var result = ColorHelper.GetEntityColor(line, Colors.White);
        result.Should().Be(Colors.Red);
    }

    [Fact]
    public void GetEntityColor_ByBlock_ReturnsDefaultColor()
    {
        var line = new ACadSharp.Entities.Line { Color = ACadSharp.Color.ByBlock };
        var result = ColorHelper.GetEntityColor(line, Colors.Cyan);
        result.Should().Be(Colors.Cyan);
    }
}
