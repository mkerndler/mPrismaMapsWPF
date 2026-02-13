using ACadSharp.Entities;
using CSMath;
using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class EntityTransformHelperTests
{
    [Fact]
    public void TranslateEntity_Line_MovesBothEndpoints()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        EntityTransformHelper.TranslateEntity(line, 5, 3);
        line.StartPoint.X.Should().Be(5);
        line.StartPoint.Y.Should().Be(3);
        line.EndPoint.X.Should().Be(15);
        line.EndPoint.Y.Should().Be(13);
    }

    [Fact]
    public void TranslateEntity_Circle_MovesCenter()
    {
        var circle = EntityFactory.CreateCircle(10, 20, 5);
        EntityTransformHelper.TranslateEntity(circle, -3, 7);
        circle.Center.X.Should().Be(7);
        circle.Center.Y.Should().Be(27);
    }

    [Fact]
    public void TranslateEntity_Arc_MovesCenter()
    {
        var arc = EntityFactory.CreateArc(10, 20, 5, 0, Math.PI);
        EntityTransformHelper.TranslateEntity(arc, 2, -4);
        arc.Center.X.Should().Be(12);
        arc.Center.Y.Should().Be(16);
    }

    [Fact]
    public void TranslateEntity_LwPolyline_MovesAllVertices()
    {
        var poly = EntityFactory.CreateLwPolyline((0, 0), (10, 0), (10, 10));
        EntityTransformHelper.TranslateEntity(poly, 5, 5);
        poly.Vertices[0].Location.X.Should().Be(5);
        poly.Vertices[0].Location.Y.Should().Be(5);
        poly.Vertices[1].Location.X.Should().Be(15);
        poly.Vertices[1].Location.Y.Should().Be(5);
        poly.Vertices[2].Location.X.Should().Be(15);
        poly.Vertices[2].Location.Y.Should().Be(15);
    }

    [Fact]
    public void TranslateEntity_MText_MovesInsertPoint()
    {
        var mtext = EntityFactory.CreateMText(10, 20, "Hello");
        EntityTransformHelper.TranslateEntity(mtext, 3, -5);
        mtext.InsertPoint.X.Should().Be(13);
        mtext.InsertPoint.Y.Should().Be(15);
    }

    [Fact]
    public void TranslateEntity_Point_MovesLocation()
    {
        var point = EntityFactory.CreatePoint(10, 20);
        EntityTransformHelper.TranslateEntity(point, 1, 2);
        point.Location.X.Should().Be(11);
        point.Location.Y.Should().Be(22);
    }

    [Fact]
    public void CloneEntity_Line_ReturnsIndependentCopy()
    {
        var line = EntityFactory.CreateLine(1, 2, 3, 4);
        var clone = EntityTransformHelper.CloneEntity(line) as Line;
        clone.Should().NotBeNull();
        clone!.StartPoint.X.Should().Be(1);
        clone.EndPoint.X.Should().Be(3);

        // Modify original, clone should not be affected
        line.StartPoint = new XYZ(99, 99, 0);
        clone.StartPoint.X.Should().Be(1);
    }

    [Fact]
    public void CloneEntity_Circle_ReturnsIndependentCopy()
    {
        var circle = EntityFactory.CreateCircle(5, 10, 3);
        var clone = EntityTransformHelper.CloneEntity(circle) as Circle;
        clone.Should().NotBeNull();
        clone!.Center.X.Should().Be(5);
        clone.Radius.Should().Be(3);
    }

    [Fact]
    public void CloneEntity_MText_ReturnsIndependentCopy()
    {
        var mtext = EntityFactory.CreateMText(1, 2, "Test");
        var clone = EntityTransformHelper.CloneEntity(mtext) as MText;
        clone.Should().NotBeNull();
        clone!.Value.Should().Be("Test");
        clone.InsertPoint.X.Should().Be(1);
    }

    [Fact]
    public void CloneEntity_LwPolyline_ReturnsIndependentCopy()
    {
        var poly = EntityFactory.CreateLwPolyline((0, 0), (10, 10));
        var clone = EntityTransformHelper.CloneEntity(poly) as LwPolyline;
        clone.Should().NotBeNull();
        clone!.Vertices.Count.Should().Be(2);
        clone.Vertices[0].Location.X.Should().Be(0);
        clone.Vertices[1].Location.X.Should().Be(10);
    }

    [Fact]
    public void CloneEntity_UnsupportedType_ReturnsNull()
    {
        var hatch = new Hatch();
        var clone = EntityTransformHelper.CloneEntity(hatch);
        clone.Should().BeNull();
    }
}
