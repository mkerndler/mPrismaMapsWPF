using ACadSharp.Entities;
using CSMath;
using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class EntityTransformHelperScaleTests
{
    [Fact]
    public void ScaleEntity_Line_ScalesAroundPivot()
    {
        var line = EntityFactory.CreateLine(10, 10, 20, 20);

        EntityTransformHelper.ScaleEntity(line, 0, 0, 2.0, 2.0);

        line.StartPoint.X.Should().BeApproximately(20, 0.001);
        line.StartPoint.Y.Should().BeApproximately(20, 0.001);
        line.EndPoint.X.Should().BeApproximately(40, 0.001);
        line.EndPoint.Y.Should().BeApproximately(40, 0.001);
    }

    [Fact]
    public void ScaleEntity_Line_ScalesAroundCenter()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 0);

        EntityTransformHelper.ScaleEntity(line, 5, 0, 2.0, 1.0);

        line.StartPoint.X.Should().BeApproximately(-5, 0.001);
        line.EndPoint.X.Should().BeApproximately(15, 0.001);
    }

    [Fact]
    public void ScaleEntity_Circle_ScalesRadiusAndCenter()
    {
        var circle = EntityFactory.CreateCircle(10, 10, 5);

        EntityTransformHelper.ScaleEntity(circle, 0, 0, 2.0, 2.0);

        circle.Center.X.Should().BeApproximately(20, 0.001);
        circle.Center.Y.Should().BeApproximately(20, 0.001);
        circle.Radius.Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public void ScaleEntity_Arc_ScalesRadiusAndCenter()
    {
        var arc = EntityFactory.CreateArc(10, 10, 5, 0, Math.PI);

        EntityTransformHelper.ScaleEntity(arc, 0, 0, 3.0, 3.0);

        arc.Center.X.Should().BeApproximately(30, 0.001);
        arc.Center.Y.Should().BeApproximately(30, 0.001);
        arc.Radius.Should().BeApproximately(15, 0.001);
    }

    [Fact]
    public void ScaleEntity_LwPolyline_ScalesAllVertices()
    {
        var poly = EntityFactory.CreateLwPolyline((0, 0), (10, 0), (10, 10));

        EntityTransformHelper.ScaleEntity(poly, 0, 0, 2.0, 3.0);

        poly.Vertices[0].Location.X.Should().BeApproximately(0, 0.001);
        poly.Vertices[0].Location.Y.Should().BeApproximately(0, 0.001);
        poly.Vertices[1].Location.X.Should().BeApproximately(20, 0.001);
        poly.Vertices[1].Location.Y.Should().BeApproximately(0, 0.001);
        poly.Vertices[2].Location.X.Should().BeApproximately(20, 0.001);
        poly.Vertices[2].Location.Y.Should().BeApproximately(30, 0.001);
    }

    [Fact]
    public void ScaleEntity_MText_ScalesHeightAndPosition()
    {
        var mtext = EntityFactory.CreateMText(10, 20, "Hello");

        EntityTransformHelper.ScaleEntity(mtext, 0, 0, 1.0, 2.0);

        mtext.InsertPoint.X.Should().BeApproximately(10, 0.001);
        mtext.InsertPoint.Y.Should().BeApproximately(40, 0.001);
        mtext.Height.Should().BeApproximately(5.0, 0.001); // 2.5 * 2
    }

    [Fact]
    public void ScaleEntity_TextEntity_ScalesHeight()
    {
        var text = new TextEntity
        {
            InsertPoint = new XYZ(5, 5, 0),
            Height = 10,
            Value = "Test"
        };

        EntityTransformHelper.ScaleEntity(text, 0, 0, 1.0, 0.5);

        text.InsertPoint.Y.Should().BeApproximately(2.5, 0.001);
        text.Height.Should().BeApproximately(5, 0.001);
    }

    [Fact]
    public void ScaleEntity_Point_ScalesPosition()
    {
        var point = EntityFactory.CreatePoint(10, 20);

        EntityTransformHelper.ScaleEntity(point, 5, 10, 2.0, 2.0);

        point.Location.X.Should().BeApproximately(15, 0.001);
        point.Location.Y.Should().BeApproximately(30, 0.001);
    }

    [Fact]
    public void ScaleEntity_WithUnitScale_NoChange()
    {
        var line = EntityFactory.CreateLine(5, 5, 15, 15);

        EntityTransformHelper.ScaleEntity(line, 10, 10, 1.0, 1.0);

        line.StartPoint.X.Should().BeApproximately(5, 0.001);
        line.StartPoint.Y.Should().BeApproximately(5, 0.001);
        line.EndPoint.X.Should().BeApproximately(15, 0.001);
        line.EndPoint.Y.Should().BeApproximately(15, 0.001);
    }
}
