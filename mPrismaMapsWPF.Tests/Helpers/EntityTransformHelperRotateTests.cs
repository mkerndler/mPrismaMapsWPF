using ACadSharp.Entities;
using CSMath;
using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class EntityTransformHelperRotateTests
{
    [Fact]
    public void RotateEntity_Line_90DegreesAroundOrigin()
    {
        var line = EntityFactory.CreateLine(10, 0, 10, 10);

        EntityTransformHelper.RotateEntity(line, 0, 0, Math.PI / 2);

        line.StartPoint.X.Should().BeApproximately(0, 0.001);
        line.StartPoint.Y.Should().BeApproximately(10, 0.001);
        line.EndPoint.X.Should().BeApproximately(-10, 0.001);
        line.EndPoint.Y.Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public void RotateEntity_Circle_MovesCenter()
    {
        var circle = EntityFactory.CreateCircle(10, 0, 5);

        EntityTransformHelper.RotateEntity(circle, 0, 0, Math.PI / 2);

        circle.Center.X.Should().BeApproximately(0, 0.001);
        circle.Center.Y.Should().BeApproximately(10, 0.001);
        circle.Radius.Should().Be(5); // radius unchanged
    }

    [Fact]
    public void RotateEntity_Arc_AdjustsAngles()
    {
        var arc = EntityFactory.CreateArc(0, 0, 5, 0, Math.PI / 2);

        EntityTransformHelper.RotateEntity(arc, 0, 0, Math.PI / 4);

        arc.StartAngle.Should().BeApproximately(Math.PI / 4, 0.001);
        arc.EndAngle.Should().BeApproximately(3 * Math.PI / 4, 0.001);
    }

    [Fact]
    public void RotateEntity_LwPolyline_RotatesAllVertices()
    {
        var poly = EntityFactory.CreateLwPolyline((10, 0), (0, 0), (0, 10));

        EntityTransformHelper.RotateEntity(poly, 0, 0, Math.PI / 2);

        poly.Vertices[0].Location.X.Should().BeApproximately(0, 0.001);
        poly.Vertices[0].Location.Y.Should().BeApproximately(10, 0.001);
        poly.Vertices[1].Location.X.Should().BeApproximately(0, 0.001);
        poly.Vertices[1].Location.Y.Should().BeApproximately(0, 0.001);
        poly.Vertices[2].Location.X.Should().BeApproximately(-10, 0.001);
        poly.Vertices[2].Location.Y.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void RotateEntity_TextEntity_AdjustsRotation()
    {
        var text = new TextEntity
        {
            InsertPoint = new XYZ(10, 0, 0),
            Height = 5,
            Value = "Test",
            Rotation = 0
        };

        EntityTransformHelper.RotateEntity(text, 0, 0, Math.PI / 4);

        text.InsertPoint.X.Should().BeApproximately(10 * Math.Cos(Math.PI / 4), 0.001);
        text.InsertPoint.Y.Should().BeApproximately(10 * Math.Sin(Math.PI / 4), 0.001);
        text.Rotation.Should().BeApproximately(45, 0.001); // degrees
    }

    [Fact]
    public void RotateEntity_MText_RotatesPosition()
    {
        var mtext = EntityFactory.CreateMText(10, 0, "Hello");

        EntityTransformHelper.RotateEntity(mtext, 0, 0, Math.PI / 2);

        mtext.InsertPoint.X.Should().BeApproximately(0, 0.001);
        mtext.InsertPoint.Y.Should().BeApproximately(10, 0.001);
    }

    [Fact]
    public void RotateEntity_Point_RotatesPosition()
    {
        var point = EntityFactory.CreatePoint(10, 0);

        EntityTransformHelper.RotateEntity(point, 0, 0, Math.PI);

        point.Location.X.Should().BeApproximately(-10, 0.001);
        point.Location.Y.Should().BeApproximately(0, 0.001);
    }

    [Fact]
    public void RotateEntity_360Degrees_ReturnsToOriginal()
    {
        var line = EntityFactory.CreateLine(5, 3, 15, 8);

        EntityTransformHelper.RotateEntity(line, 10, 5, 2 * Math.PI);

        line.StartPoint.X.Should().BeApproximately(5, 0.001);
        line.StartPoint.Y.Should().BeApproximately(3, 0.001);
        line.EndPoint.X.Should().BeApproximately(15, 0.001);
        line.EndPoint.Y.Should().BeApproximately(8, 0.001);
    }

    [Fact]
    public void RotateEntity_AroundNonOriginPivot()
    {
        var line = EntityFactory.CreateLine(20, 10, 30, 10);

        EntityTransformHelper.RotateEntity(line, 20, 10, Math.PI / 2);

        line.StartPoint.X.Should().BeApproximately(20, 0.001);
        line.StartPoint.Y.Should().BeApproximately(10, 0.001);
        line.EndPoint.X.Should().BeApproximately(20, 0.001);
        line.EndPoint.Y.Should().BeApproximately(20, 0.001);
    }
}
