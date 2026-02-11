using mPrismaMapsWPF.Models;

namespace mPrismaMapsWPF.Services;

public class WalkwayService : IWalkwayService
{
    public WalkwayGraph Graph { get; } = new();

    public void RebuildGraph(IEnumerable<EntityModel> entities)
    {
        Graph.BuildFromEntities(entities);
    }

    public HashSet<ulong>? GetPathHighlightsForUnit(double unitX, double unitY)
    {
        if (Graph.Nodes.Count == 0)
            return null;

        // Compute a reasonable search radius from the graph's extent
        double maxNodeDistance = ComputeMaxNodeDistance();

        var nearestNode = Graph.FindNearestNode(unitX, unitY, maxNodeDistance);
        if (nearestNode == null)
            return null;

        var path = Graph.FindPathToNearestEntrance(nearestNode.Handle);
        if (path == null)
            return null;

        return Graph.GetAllHandlesForPath(path);
    }

    private double ComputeMaxNodeDistance()
    {
        if (Graph.Nodes.Count < 2)
            return double.MaxValue;

        // Use the average edge length * 3 as a reasonable search radius,
        // or fall back to a fraction of the graph's bounding box
        if (Graph.Edges.Count > 0)
        {
            double avgWeight = Graph.Edges.Values.Average(e => e.Weight);
            return avgWeight * 3;
        }

        // Fallback: compute bounding box of all nodes
        double minX = double.MaxValue, maxX = double.MinValue;
        double minY = double.MaxValue, maxY = double.MinValue;
        foreach (var node in Graph.Nodes.Values)
        {
            minX = Math.Min(minX, node.X);
            maxX = Math.Max(maxX, node.X);
            minY = Math.Min(minY, node.Y);
            maxY = Math.Max(maxY, node.Y);
        }
        double span = Math.Max(maxX - minX, maxY - minY);
        return Math.Max(span * 0.1, 50.0);
    }
}
