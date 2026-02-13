using ACadSharp.Entities;
using FluentAssertions;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Models;

public class EntityModelTests
{
    [Fact]
    public void TypeName_ReturnsEntityClassName()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var model = new EntityModel(line);
        model.TypeName.Should().Be("Line");
    }

    [Fact]
    public void LayerName_ReturnsDefault_WhenNoLayer()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var model = new EntityModel(line);
        model.LayerName.Should().Be("0");
    }

    [Fact]
    public void DisplayName_ContainsTypeAndHandle()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var model = new EntityModel(line);
        model.DisplayName.Should().Contain("Line");
    }

    [Theory]
    [InlineData(typeof(Line), "/")]
    [InlineData(typeof(Circle), "○")]
    [InlineData(typeof(MText), "A")]
    [InlineData(typeof(Point), "•")]
    public void TypeIcon_ReturnsCorrectIcon(Type entityType, string expectedIcon)
    {
        Entity entity = entityType.Name switch
        {
            "Line" => EntityFactory.CreateLine(0, 0, 10, 10),
            "Circle" => EntityFactory.CreateCircle(5, 5, 3),
            "MText" => EntityFactory.CreateMText(0, 0, "test"),
            "Point" => EntityFactory.CreatePoint(0, 0),
            _ => throw new ArgumentException()
        };

        var model = new EntityModel(entity);
        model.TypeIcon.Should().Be(expectedIcon);
    }

    [Fact]
    public void Arc_TypeIcon_IsCorrect()
    {
        var arc = EntityFactory.CreateArc(0, 0, 5, 0, Math.PI);
        var model = new EntityModel(arc);
        model.TypeIcon.Should().Be("◜");
    }

    [Fact]
    public void IsSelected_FiresPropertyChanged()
    {
        var model = new EntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        bool fired = false;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EntityModel.IsSelected))
                fired = true;
        };

        model.IsSelected = true;
        fired.Should().BeTrue();
    }

    [Fact]
    public void IsLocked_FiresPropertyChanged()
    {
        var model = new EntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        bool fired = false;
        model.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(EntityModel.IsLocked))
                fired = true;
        };

        model.IsLocked = true;
        fired.Should().BeTrue();
    }

    [Fact]
    public void GetProperty_Type_ReturnsTypeName()
    {
        var model = new EntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        model.GetProperty("Type").Should().Be("Line");
    }

    [Fact]
    public void GetProperty_Layer_ReturnsLayerName()
    {
        var model = new EntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        model.GetProperty("Layer").Should().Be("0");
    }

    [Fact]
    public void GetProperty_Color_ByLayer_ReturnsByLayer()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        // Default color should be ByLayer
        var model = new EntityModel(line);
        model.GetProperty("Color").Should().Be("ByLayer");
    }
}
