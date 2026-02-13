using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Tests.Helpers;

public class HitTestHelperTests
{
    [Fact]
    public void HitTest_Line_PointOnLine_ReturnsTrue()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 0);
        HitTestHelper.HitTest(line, new WpfPoint(5, 0), 1.0).Should().BeTrue();
    }

    [Fact]
    public void HitTest_Line_PointWithinTolerance_ReturnsTrue()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 0);
        HitTestHelper.HitTest(line, new WpfPoint(5, 2), 3.0).Should().BeTrue();
    }

    [Fact]
    public void HitTest_Line_PointFarAway_ReturnsFalse()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 0);
        HitTestHelper.HitTest(line, new WpfPoint(5, 50), 5.0).Should().BeFalse();
    }

    [Fact]
    public void HitTest_Circle_PointOnCircumference_ReturnsTrue()
    {
        var circle = EntityFactory.CreateCircle(0, 0, 10);
        HitTestHelper.HitTest(circle, new WpfPoint(10, 0), 2.0).Should().BeTrue();
    }

    [Fact]
    public void HitTest_Circle_PointFarAway_ReturnsFalse()
    {
        var circle = EntityFactory.CreateCircle(0, 0, 10);
        HitTestHelper.HitTest(circle, new WpfPoint(50, 50), 5.0).Should().BeFalse();
    }

    [Fact]
    public void HitTest_Circle_PointAtCenter_ReturnsFalse()
    {
        // Center is not on circumference
        var circle = EntityFactory.CreateCircle(0, 0, 10);
        HitTestHelper.HitTest(circle, new WpfPoint(0, 0), 2.0).Should().BeFalse();
    }

    [Fact]
    public void HitTest_Arc_PointWithinAngleRange_ReturnsTrue()
    {
        var arc = EntityFactory.CreateArc(0, 0, 10, 0, Math.PI / 2);
        // Point on arc at 45 degrees
        HitTestHelper.HitTest(arc, new WpfPoint(10 * Math.Cos(Math.PI / 4), 10 * Math.Sin(Math.PI / 4)), 2.0)
            .Should().BeTrue();
    }

    [Fact]
    public void HitTest_LwPolyline_PointOnSegment_ReturnsTrue()
    {
        var poly = EntityFactory.CreateLwPolyline((0, 0), (10, 0), (10, 10));
        HitTestHelper.HitTest(poly, new WpfPoint(5, 0), 2.0).Should().BeTrue();
    }

    [Fact]
    public void HitTest_LwPolyline_PointFarAway_ReturnsFalse()
    {
        var poly = EntityFactory.CreateLwPolyline((0, 0), (10, 0), (10, 10));
        HitTestHelper.HitTest(poly, new WpfPoint(50, 50), 2.0).Should().BeFalse();
    }

    [Fact]
    public void HitTest_MText_PointInBoundingBox_ReturnsTrue()
    {
        var mtext = EntityFactory.CreateMText(0, 0, "Hello");
        HitTestHelper.HitTest(mtext, new WpfPoint(1, 1), 1.0).Should().BeTrue();
    }

    [Fact]
    public void HitTest_MText_PointFarAway_ReturnsFalse()
    {
        var mtext = EntityFactory.CreateMText(0, 0, "Hello");
        HitTestHelper.HitTest(mtext, new WpfPoint(100, 100), 1.0).Should().BeFalse();
    }

    [Fact]
    public void HitTest_ToleranceParameter_AffectsDetection()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 0);
        var point = new WpfPoint(5, 8);
        HitTestHelper.HitTest(line, point, 5.0).Should().BeFalse();
        HitTestHelper.HitTest(line, point, 10.0).Should().BeTrue();
    }
}
