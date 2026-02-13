using FluentAssertions;
using mPrismaMapsWPF.Drawing;

namespace mPrismaMapsWPF.Tests.Drawing;

public class GridSnapSettingsTests
{
    [Theory]
    [InlineData(30, 1)]
    [InlineData(60, 2)]
    [InlineData(150, 5)]
    [InlineData(300, 10)]
    [InlineData(600, 20)]
    [InlineData(1500, 50)]
    [InlineData(3000, 100)]
    public void CalculateAutoGridSpacing_ReturnsNiceNumbers(double dimension, double expected)
    {
        GridSnapSettings.CalculateAutoGridSpacing(dimension).Should().Be(expected);
    }

    [Fact]
    public void CalculateAutoGridSpacing_ZeroDimension_ReturnsDefault()
    {
        GridSnapSettings.CalculateAutoGridSpacing(0).Should().Be(10.0);
    }

    [Fact]
    public void CalculateAutoGridSpacing_NegativeDimension_ReturnsDefault()
    {
        GridSnapSettings.CalculateAutoGridSpacing(-5).Should().Be(10.0);
    }

    [Fact]
    public void SetUniformSpacing_SetsBothXAndY()
    {
        var settings = new GridSnapSettings();
        settings.SetUniformSpacing(5.0);

        settings.SpacingX.Should().Be(5.0);
        settings.SpacingY.Should().Be(5.0);
    }

    [Fact]
    public void DefaultValues_AreCorrect()
    {
        var settings = new GridSnapSettings();
        settings.IsEnabled.Should().BeTrue();
        settings.SpacingX.Should().Be(10.0);
        settings.SpacingY.Should().Be(10.0);
        settings.OriginX.Should().Be(0.0);
        settings.OriginY.Should().Be(0.0);
        settings.ShowGrid.Should().BeTrue();
    }

    [Fact]
    public void PropertyChanged_FiresOnSpacingChange()
    {
        var settings = new GridSnapSettings();
        bool fired = false;
        settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(GridSnapSettings.SpacingX))
                fired = true;
        };

        settings.SpacingX = 20.0;
        fired.Should().BeTrue();
    }
}
