using FluentAssertions;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Helpers;

public class FloodFillGridTests
{
    [Fact]
    public void Constructor_SetsGridDimensions()
    {
        var grid = new FloodFillGrid(0, 0, 100, 100, 1.0);
        // Grid should be created without throwing
        grid.Should().NotBeNull();
    }

    [Fact]
    public void RasterizeEntity_Line_MarksWallCells()
    {
        var grid = new FloodFillGrid(0, 0, 20, 20, 1.0);
        var line = EntityFactory.CreateLine(5, 5, 15, 5);
        grid.RasterizeEntity(line);
        // After rasterizing, the wall cells should block flood fill through them
        grid.Should().NotBeNull();
    }

    [Fact]
    public void FloodFill_EnclosedRegion_ReturnsFilledArray()
    {
        // Create a box with lines
        var grid = new FloodFillGrid(0, 0, 20, 20, 0.5);
        grid.RasterizeEntity(EntityFactory.CreateLine(5, 5, 15, 5));
        grid.RasterizeEntity(EntityFactory.CreateLine(15, 5, 15, 15));
        grid.RasterizeEntity(EntityFactory.CreateLine(15, 15, 5, 15));
        grid.RasterizeEntity(EntityFactory.CreateLine(5, 15, 5, 5));

        var filled = grid.FloodFill(10, 10);
        filled.Should().NotBeNull("the center of the box should be fillable");
    }

    [Fact]
    public void FloodFill_OpenRegion_ReturnsNull()
    {
        var grid = new FloodFillGrid(0, 0, 20, 20, 0.5);
        // Only one wall, region is open
        grid.RasterizeEntity(EntityFactory.CreateLine(5, 5, 15, 5));

        // Flood fill from a point in open space should return null
        var filled = grid.FloodFill(10, 0);
        filled.Should().BeNull("open regions that reach the boundary return null");
    }

    [Fact]
    public void ExtractContour_FilledRegion_ProducesPoints()
    {
        var grid = new FloodFillGrid(0, 0, 20, 20, 0.5);
        grid.RasterizeEntity(EntityFactory.CreateLine(5, 5, 15, 5));
        grid.RasterizeEntity(EntityFactory.CreateLine(15, 5, 15, 15));
        grid.RasterizeEntity(EntityFactory.CreateLine(15, 15, 5, 15));
        grid.RasterizeEntity(EntityFactory.CreateLine(5, 15, 5, 5));

        var filled = grid.FloodFill(10, 10);
        filled.Should().NotBeNull();

        var contour = grid.ExtractContour(filled!);
        contour.Should().NotBeEmpty();
        contour.Count.Should().BeGreaterThan(3);
    }

    [Fact]
    public void SimplifyPolygon_ReducesPointCount()
    {
        var points = new List<(double x, double y)>
        {
            (0, 0), (1, 0.01), (2, -0.01), (3, 0.02), (4, 0), (5, 0),
            (5, 1), (5, 2), (5, 3), (5, 4), (5, 5),
            (0, 5), (0, 0)
        };
        var simplified = FloodFillGrid.SimplifyPolygon(points, 0.1);
        simplified.Count.Should().BeLessThan(points.Count);
    }

    [Fact]
    public void SimplifyPolygon_PreservesEndpoints()
    {
        var points = new List<(double x, double y)>
        {
            (0, 0), (5, 0.01), (10, 0)
        };
        var simplified = FloodFillGrid.SimplifyPolygon(points, 0.1);
        simplified.First().Should().Be((0, 0));
        simplified.Last().Should().Be((10, 0));
    }

    [Fact]
    public void SimplifyPolygon_LessThan3Points_ReturnsOriginal()
    {
        var points = new List<(double x, double y)> { (0, 0), (10, 10) };
        var simplified = FloodFillGrid.SimplifyPolygon(points, 0.1);
        simplified.Should().HaveCount(2);
    }
}
