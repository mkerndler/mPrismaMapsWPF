using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using FluentAssertions;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Services;

public class WalkwayServiceTests
{
    [Fact]
    public void RebuildGraph_PopulatesNodesFromCircles()
    {
        var service = new WalkwayService();
        var doc = EntityFactory.CreateDocumentModel();
        var walkwayLayer = new Layer(CadDocumentModel.WalkwaysLayerName);
        doc.Document!.Layers.Add(walkwayLayer);

        var circle1 = EntityFactory.CreateCircle(0, 0, 1, walkwayLayer);
        circle1.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle1);
        var circle2 = EntityFactory.CreateCircle(10, 0, 1, walkwayLayer);
        circle2.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle2);

        var entities = new[]
        {
            new EntityModel(circle1),
            new EntityModel(circle2)
        };

        service.RebuildGraph(entities);

        service.Graph.Nodes.Should().HaveCount(2);
    }

    [Fact]
    public void GetPathHighlightsForUnit_ReturnsNull_WhenNoNodes()
    {
        var service = new WalkwayService();
        service.GetPathHighlightsForUnit(5, 5).Should().BeNull();
    }

    [Fact]
    public void GetPathHighlightsForUnit_ReturnsNull_WhenNoEntrance()
    {
        var service = new WalkwayService();
        var doc = EntityFactory.CreateDocumentModel();
        var walkwayLayer = new Layer(CadDocumentModel.WalkwaysLayerName);
        doc.Document!.Layers.Add(walkwayLayer);

        // Two regular nodes connected by edge, no entrance
        var circle1 = EntityFactory.CreateCircle(0, 0, 1, walkwayLayer);
        circle1.Color = new Color(5); // regular (blue)
        doc.Document!.ModelSpace.Entities.Add(circle1);
        var circle2 = EntityFactory.CreateCircle(10, 0, 1, walkwayLayer);
        circle2.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle2);
        var edge = EntityFactory.CreateLine(0, 0, 10, 0, walkwayLayer);
        doc.Document!.ModelSpace.Entities.Add(edge);

        var entities = new[]
        {
            new EntityModel(circle1),
            new EntityModel(circle2),
            new EntityModel(edge)
        };

        service.RebuildGraph(entities);
        service.GetPathHighlightsForUnit(0, 0).Should().BeNull();
    }
}
