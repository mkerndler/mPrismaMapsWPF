using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using CSMath;
using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class EntityClonerTests
{
    private static readonly Layer TestLayer = new Layer("TestLayer");

    [Fact]
    public void Clone_Line_CopiesCoordinatesAndLayer()
    {
        var src = EntityFactory.CreateLine(1, 2, 3, 4);

        var cloned = EntityCloner.Clone(src, TestLayer) as Line;

        cloned.Should().NotBeNull();
        cloned!.StartPoint.X.Should().Be(1);
        cloned.StartPoint.Y.Should().Be(2);
        cloned.EndPoint.X.Should().Be(3);
        cloned.EndPoint.Y.Should().Be(4);
        cloned.Layer.Should().BeSameAs(TestLayer);
        cloned.Should().NotBeSameAs(src);
    }

    [Fact]
    public void Clone_Line_AppliesOffset()
    {
        var src = EntityFactory.CreateLine(0, 0, 10, 10);

        var cloned = EntityCloner.Clone(src, TestLayer, offsetX: 5, offsetY: -3) as Line;

        cloned!.StartPoint.X.Should().Be(5);
        cloned.StartPoint.Y.Should().Be(-3);
        cloned.EndPoint.X.Should().Be(15);
        cloned.EndPoint.Y.Should().Be(7);
    }

    [Fact]
    public void Clone_Arc_CopiesAllGeometry()
    {
        var src = EntityFactory.CreateArc(10, 20, 5, 30, 270);

        var cloned = EntityCloner.Clone(src, TestLayer) as Arc;

        cloned.Should().NotBeNull();
        cloned!.Center.X.Should().Be(10);
        cloned.Center.Y.Should().Be(20);
        cloned.Radius.Should().Be(5);
        cloned.StartAngle.Should().Be(30);
        cloned.EndAngle.Should().Be(270);
        cloned.Layer.Should().BeSameAs(TestLayer);
    }

    [Fact]
    public void Clone_Circle_CopiesGeometry()
    {
        var src = EntityFactory.CreateCircle(5, 7, 3);

        var cloned = EntityCloner.Clone(src, TestLayer) as Circle;

        cloned.Should().NotBeNull();
        cloned!.Center.X.Should().Be(5);
        cloned.Center.Y.Should().Be(7);
        cloned.Radius.Should().Be(3);
    }

    [Fact]
    public void Clone_LwPolyline_CopiesVerticesAndIsClosed()
    {
        var src = EntityFactory.CreateLwPolyline((0, 0), (10, 0), (10, 10), (0, 10));
        src.IsClosed = true;

        var cloned = EntityCloner.Clone(src, TestLayer) as LwPolyline;

        cloned.Should().NotBeNull();
        cloned!.IsClosed.Should().BeTrue();
        cloned.Vertices.Should().HaveCount(4);
        cloned.Vertices[0].Location.X.Should().Be(0);
        cloned.Vertices[2].Location.X.Should().Be(10);
        cloned.Vertices[2].Location.Y.Should().Be(10);
    }

    [Fact]
    public void Clone_LwPolyline_AppliesOffset()
    {
        var src = EntityFactory.CreateLwPolyline((0, 0), (10, 0));

        var cloned = EntityCloner.Clone(src, TestLayer, offsetX: 2, offsetY: 3) as LwPolyline;

        cloned!.Vertices[0].Location.X.Should().Be(2);
        cloned.Vertices[0].Location.Y.Should().Be(3);
        cloned.Vertices[1].Location.X.Should().Be(12);
        cloned.Vertices[1].Location.Y.Should().Be(3);
    }

    [Fact]
    public void Clone_LwPolyline_PreservesBulge()
    {
        var src = new LwPolyline();
        src.Vertices.Add(new LwPolyline.Vertex(new XY(0, 0)) { Bulge = 1.0 });
        src.Vertices.Add(new LwPolyline.Vertex(new XY(10, 0)));

        var cloned = EntityCloner.Clone(src, TestLayer) as LwPolyline;

        cloned!.Vertices[0].Bulge.Should().Be(1.0);
    }

    [Fact]
    public void Clone_MText_CopiesTextAndPosition()
    {
        var src = EntityFactory.CreateMText(5, 10, "Unit 42");

        var cloned = EntityCloner.Clone(src, TestLayer) as MText;

        cloned.Should().NotBeNull();
        cloned!.InsertPoint.X.Should().Be(5);
        cloned.InsertPoint.Y.Should().Be(10);
        cloned.Value.Should().Be("Unit 42");
        cloned.Layer.Should().BeSameAs(TestLayer);
    }

    [Fact]
    public void Clone_Point_CopiesLocation()
    {
        var src = EntityFactory.CreatePoint(3, 7);

        var cloned = EntityCloner.Clone(src, TestLayer) as Point;

        cloned.Should().NotBeNull();
        cloned!.Location.X.Should().Be(3);
        cloned.Location.Y.Should().Be(7);
    }

    [Fact]
    public void Clone_UnsupportedType_ReturnsNull()
    {
        // Hatch is an entity type not supported by EntityCloner
        var hatch = new Hatch();

        var cloned = EntityCloner.Clone(hatch, TestLayer);

        cloned.Should().BeNull();
    }
}
