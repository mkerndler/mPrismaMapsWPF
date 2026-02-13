using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Helpers;

[Collection("BoundingBox")]
public class BoundingBoxHelperTests
{
    [Fact]
    public void GetBounds_Line_ReturnsRectFromEndpoints()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(2, 3, 10, 7);
        doc.Document!.ModelSpace.Entities.Add(line);

        var bounds = BoundingBoxHelper.GetBounds(line);
        bounds.Should().NotBeNull();
        bounds!.Value.Left.Should().Be(2);
        bounds.Value.Top.Should().Be(3);
        bounds.Value.Width.Should().Be(8);
        bounds.Value.Height.Should().Be(4);
    }

    [Fact]
    public void GetBounds_Circle_ReturnsSquareAroundCenter()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var circle = EntityFactory.CreateCircle(10, 20, 5);
        doc.Document!.ModelSpace.Entities.Add(circle);

        var bounds = BoundingBoxHelper.GetBounds(circle);
        bounds.Should().NotBeNull();
        bounds!.Value.Left.Should().Be(5);
        bounds.Value.Top.Should().Be(15);
        bounds.Value.Width.Should().Be(10);
        bounds.Value.Height.Should().Be(10);
    }

    [Fact]
    public void GetBounds_Arc_ReturnsBoundingRect()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var arc = EntityFactory.CreateArc(10, 20, 5, 0, Math.PI);
        doc.Document!.ModelSpace.Entities.Add(arc);

        var bounds = BoundingBoxHelper.GetBounds(arc);
        bounds.Should().NotBeNull();
        // Conservative: uses full circle extent
        bounds!.Value.Left.Should().Be(5);
        bounds.Value.Top.Should().Be(15);
        bounds.Value.Width.Should().Be(10);
        bounds.Value.Height.Should().Be(10);
    }

    [Fact]
    public void GetBounds_LwPolyline_ReturnsEnclosingRect()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var poly = EntityFactory.CreateLwPolyline((0, 0), (10, 0), (10, 5), (0, 5));
        doc.Document!.ModelSpace.Entities.Add(poly);

        var bounds = BoundingBoxHelper.GetBounds(poly);
        bounds.Should().NotBeNull();
        bounds!.Value.Left.Should().Be(0);
        bounds.Value.Top.Should().Be(0);
        bounds.Value.Width.Should().Be(10);
        bounds.Value.Height.Should().Be(5);
    }

    [Fact]
    public void GetBounds_MText_ReturnsRectFromInsertPoint()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var mtext = EntityFactory.CreateMText(5, 10, "Hello");
        doc.Document!.ModelSpace.Entities.Add(mtext);

        var bounds = BoundingBoxHelper.GetBounds(mtext);
        bounds.Should().NotBeNull();
        bounds!.Value.Left.Should().Be(5);
        bounds.Value.Top.Should().Be(10);
    }

    [Fact]
    public void GetBounds_UnsupportedEntity_ReturnsNull()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var hatch = new ACadSharp.Entities.Hatch();
        doc.Document!.ModelSpace.Entities.Add(hatch);

        var bounds = BoundingBoxHelper.GetBounds(hatch);
        bounds.Should().BeNull();
    }

    [Fact]
    public void InvalidateCache_ClearsCachedValues()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line);

        BoundingBoxHelper.GetBounds(line); // Cache it
        BoundingBoxHelper.InvalidateCache();
        // Should still work after cache clear
        var bounds = BoundingBoxHelper.GetBounds(line);
        bounds.Should().NotBeNull();
    }

    [Fact]
    public void InvalidateEntity_ClearsSpecificEntity()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line);

        BoundingBoxHelper.GetBounds(line);
        BoundingBoxHelper.InvalidateEntity(line.Handle);
        var bounds = BoundingBoxHelper.GetBounds(line);
        bounds.Should().NotBeNull();
    }

    [Fact]
    public void GetBounds_Point_ReturnsSmallRect()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var point = EntityFactory.CreatePoint(5, 10);
        doc.Document!.ModelSpace.Entities.Add(point);

        var bounds = BoundingBoxHelper.GetBounds(point);
        bounds.Should().NotBeNull();
        bounds!.Value.Width.Should().Be(8); // pointSize * 2
        bounds.Value.Height.Should().Be(8);
    }
}
