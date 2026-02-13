using System.Windows;
using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Tests.Helpers;

[Collection("BoundingBox")]
public class SpatialGridTests
{
    [Fact]
    public void Build_CreatesGridFromEntities()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line);

        var grid = SpatialGrid.Build(new[] { line }, new Rect(0, 0, 100, 100));
        grid.Should().NotBeNull();
    }

    [Fact]
    public void Query_FindsNearbyEntities()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(5, 5, 15, 15);
        doc.Document!.ModelSpace.Entities.Add(line);

        var grid = SpatialGrid.Build(new[] { line }, new Rect(0, 0, 100, 100));
        var results = grid.Query(new WpfPoint(10, 10), 20);
        results.Should().Contain(line);
    }

    [Fact]
    public void Query_DistantPoint_ReturnsEmpty()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(0, 0, 5, 5);
        doc.Document!.ModelSpace.Entities.Add(line);

        var grid = SpatialGrid.Build(new[] { line }, new Rect(0, 0, 100, 100));
        var results = grid.Query(new WpfPoint(90, 90), 2);
        results.Should().NotContain(line);
    }

    [Fact]
    public void Remove_EliminatesEntityFromGrid()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line = EntityFactory.CreateLine(5, 5, 15, 15);
        doc.Document!.ModelSpace.Entities.Add(line);

        var grid = SpatialGrid.Build(new[] { line }, new Rect(0, 0, 100, 100));
        grid.Remove(line);
        var results = grid.Query(new WpfPoint(10, 10), 20);
        results.Should().NotContain(line);
    }

    [Fact]
    public void Insert_AddsEntityToGrid()
    {
        BoundingBoxHelper.InvalidateCache();
        var doc = EntityFactory.CreateDocumentModel();
        var line1 = EntityFactory.CreateLine(0, 0, 10, 10);
        doc.Document!.ModelSpace.Entities.Add(line1);
        var line2 = EntityFactory.CreateLine(50, 50, 60, 60);
        doc.Document!.ModelSpace.Entities.Add(line2);

        var grid = SpatialGrid.Build(new[] { line1 }, new Rect(0, 0, 100, 100));
        grid.Insert(line2);

        var results = grid.Query(new WpfPoint(55, 55), 20);
        results.Should().Contain(line2);
    }
}
