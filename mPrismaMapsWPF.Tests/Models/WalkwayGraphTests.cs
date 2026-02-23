using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using FluentAssertions;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Models;

public class WalkwayGraphTests
{
    private static (CadDocumentModel doc, Layer layer) SetupDoc()
    {
        var doc = EntityFactory.CreateDocumentModel();
        var layer = new Layer(CadDocumentModel.WalkwaysLayerName);
        doc.Document!.Layers.Add(layer);
        return (doc, layer);
    }

    [Fact]
    public void BuildFromEntities_CreatesNodesFromCircles()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var circle1 = EntityFactory.CreateCircle(0, 0, 1, layer);
        circle1.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle1);
        var circle2 = EntityFactory.CreateCircle(10, 0, 1, layer);
        circle2.Color = new Color(3); // entrance
        doc.Document!.ModelSpace.Entities.Add(circle2);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(circle1),
            new EntityModel(circle2)
        });

        graph.Nodes.Should().HaveCount(2);
        graph.Nodes.Values.Should().ContainSingle(n => n.IsEntrance);
    }

    [Fact]
    public void BuildFromEntities_CreatesEdgesFromLines()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var circle1 = EntityFactory.CreateCircle(0, 0, 1, layer);
        circle1.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle1);
        var circle2 = EntityFactory.CreateCircle(10, 0, 1, layer);
        circle2.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle2);
        var edge = EntityFactory.CreateLine(0, 0, 10, 0, layer);
        doc.Document!.ModelSpace.Entities.Add(edge);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(circle1),
            new EntityModel(circle2),
            new EntityModel(edge)
        });

        graph.Edges.Should().HaveCount(1);
    }

    [Fact]
    public void FindNearestNode_ReturnsClosest()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var circle1 = EntityFactory.CreateCircle(0, 0, 1, layer);
        circle1.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle1);
        var circle2 = EntityFactory.CreateCircle(10, 0, 1, layer);
        circle2.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle2);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(circle1),
            new EntityModel(circle2)
        });

        var nearest = graph.FindNearestNode(1, 0, 5);
        nearest.Should().NotBeNull();
        nearest!.X.Should().Be(0);
    }

    [Fact]
    public void FindNearestNode_ReturnsNull_WhenBeyondMaxDistance()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var circle = EntityFactory.CreateCircle(0, 0, 1, layer);
        circle.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle);

        graph.BuildFromEntities(new[] { new EntityModel(circle) });

        graph.FindNearestNode(100, 100, 5).Should().BeNull();
    }

    [Fact]
    public void FindPathToNearestEntrance_ReturnsPath()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var nodeA = EntityFactory.CreateCircle(0, 0, 1, layer);
        nodeA.Color = new Color(5); // regular
        doc.Document!.ModelSpace.Entities.Add(nodeA);
        var nodeB = EntityFactory.CreateCircle(10, 0, 1, layer);
        nodeB.Color = new Color(3); // entrance
        doc.Document!.ModelSpace.Entities.Add(nodeB);
        var edge = EntityFactory.CreateLine(0, 0, 10, 0, layer);
        doc.Document!.ModelSpace.Entities.Add(edge);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(nodeA),
            new EntityModel(nodeB),
            new EntityModel(edge)
        });

        var path = graph.FindPathToNearestEntrance(nodeA.Handle);
        path.Should().NotBeNull();
        path.Should().HaveCountGreaterThanOrEqualTo(2);
        path!.First().Should().Be(nodeA.Handle);
        path.Last().Should().Be(nodeB.Handle);
    }

    [Fact]
    public void FindPathToNearestEntrance_ReturnsNull_WhenNoEntrance()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var circle = EntityFactory.CreateCircle(0, 0, 1, layer);
        circle.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(circle);

        graph.BuildFromEntities(new[] { new EntityModel(circle) });

        graph.FindPathToNearestEntrance(circle.Handle).Should().BeNull();
    }

    [Fact]
    public void FindPathToNearestEntrance_StartsAtEntrance_ReturnsSingleNode()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var entrance = EntityFactory.CreateCircle(0, 0, 1, layer);
        entrance.Color = new Color(3); // entrance
        doc.Document!.ModelSpace.Entities.Add(entrance);

        graph.BuildFromEntities(new[] { new EntityModel(entrance) });

        var path = graph.FindPathToNearestEntrance(entrance.Handle);
        path.Should().NotBeNull();
        path.Should().HaveCount(1);
    }

    [Fact]
    public void GetEdgeHandlesForPath_ReturnsCorrectEdges()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var nodeA = EntityFactory.CreateCircle(0, 0, 1, layer);
        nodeA.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(nodeA);
        var nodeB = EntityFactory.CreateCircle(10, 0, 1, layer);
        nodeB.Color = new Color(3);
        doc.Document!.ModelSpace.Entities.Add(nodeB);
        var edge = EntityFactory.CreateLine(0, 0, 10, 0, layer);
        doc.Document!.ModelSpace.Entities.Add(edge);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(nodeA),
            new EntityModel(nodeB),
            new EntityModel(edge)
        });

        var path = graph.FindPathToNearestEntrance(nodeA.Handle);
        var edgeHandles = graph.GetEdgeHandlesForPath(path!);
        edgeHandles.Should().HaveCount(1);
    }

    [Fact]
    public void FindPathToNearestEntrance_DisconnectedNode_ReturnsNullWithoutHanging()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();

        // Two isolated nodes (no edges connecting them, neither is entrance)
        var nodeA = EntityFactory.CreateCircle(0, 0, 1, layer);
        nodeA.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(nodeA);
        var nodeB = EntityFactory.CreateCircle(100, 100, 1, layer);
        nodeB.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(nodeB);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(nodeA),
            new EntityModel(nodeB)
        });

        // No entrance exists — must return null, not hang
        var result = graph.FindPathToNearestEntrance(nodeA.Handle);
        result.Should().BeNull();
    }

    [Fact]
    public void FindPathToNearestEntrance_TwoNodesNoEntrance_ReturnsNull()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var nodeA = EntityFactory.CreateCircle(0, 0, 1, layer);
        nodeA.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(nodeA);
        var nodeB = EntityFactory.CreateCircle(10, 0, 1, layer);
        nodeB.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(nodeB);
        var edge = EntityFactory.CreateLine(0, 0, 10, 0, layer);
        doc.Document!.ModelSpace.Entities.Add(edge);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(nodeA),
            new EntityModel(nodeB),
            new EntityModel(edge)
        });

        // Connected but no entrance
        graph.FindPathToNearestEntrance(nodeA.Handle).Should().BeNull();
    }

    [Fact]
    public void GetAllHandlesForPath_IncludesNodesAndEdges()
    {
        var (doc, layer) = SetupDoc();
        var graph = new WalkwayGraph();
        var nodeA = EntityFactory.CreateCircle(0, 0, 1, layer);
        nodeA.Color = new Color(5);
        doc.Document!.ModelSpace.Entities.Add(nodeA);
        var nodeB = EntityFactory.CreateCircle(10, 0, 1, layer);
        nodeB.Color = new Color(3);
        doc.Document!.ModelSpace.Entities.Add(nodeB);
        var edge = EntityFactory.CreateLine(0, 0, 10, 0, layer);
        doc.Document!.ModelSpace.Entities.Add(edge);

        graph.BuildFromEntities(new[]
        {
            new EntityModel(nodeA),
            new EntityModel(nodeB),
            new EntityModel(edge)
        });

        var path = graph.FindPathToNearestEntrance(nodeA.Handle);
        var handles = graph.GetAllHandlesForPath(path!);

        handles.Should().Contain(nodeA.Handle);
        handles.Should().Contain(nodeB.Handle);
        handles.Should().HaveCountGreaterThanOrEqualTo(3); // 2 nodes + 1 edge
    }
}
