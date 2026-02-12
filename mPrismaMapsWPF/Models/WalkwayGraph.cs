using ACadSharp.Entities;

namespace mPrismaMapsWPF.Models;

public class WalkwayNode
{
    public ulong Handle { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public bool IsEntrance { get; set; }
    public List<ulong> AdjacentEdgeHandles { get; } = new();
}

public class WalkwayEdge
{
    public ulong Handle { get; set; }
    public ulong NodeAHandle { get; set; }
    public ulong NodeBHandle { get; set; }
    public double Weight { get; set; }
}

public class WalkwayGraph
{
    public Dictionary<ulong, WalkwayNode> Nodes { get; } = new();
    public Dictionary<ulong, WalkwayEdge> Edges { get; } = new();

    public void BuildFromEntities(IEnumerable<EntityModel> entities)
    {
        Nodes.Clear();
        Edges.Clear();

        var walkwayEntities = entities
            .Where(e => e.Entity.Layer?.Name == CadDocumentModel.WalkwaysLayerName)
            .ToList();

        // First pass: build nodes from circles, track max radius for tolerance
        double maxRadius = 2.0;
        foreach (var em in walkwayEntities)
        {
            if (em.Entity is Circle circle and not Arc)
            {
                var node = new WalkwayNode
                {
                    Handle = circle.Handle,
                    X = circle.Center.X,
                    Y = circle.Center.Y,
                    IsEntrance = circle.Color.Index == 3 // green = entrance
                };
                Nodes[node.Handle] = node;
                if (circle.Radius > maxRadius)
                    maxRadius = circle.Radius;
            }
        }

        // Use the largest node radius * 2 as match tolerance
        double matchTolerance = maxRadius * 2;

        // Second pass: build edges from lines, match endpoints to nearest node
        foreach (var em in walkwayEntities)
        {
            if (em.Entity is Line line)
            {
                var nodeA = FindNearestNode(line.StartPoint.X, line.StartPoint.Y, matchTolerance);
                var nodeB = FindNearestNode(line.EndPoint.X, line.EndPoint.Y, matchTolerance);

                if (nodeA != null && nodeB != null && nodeA.Handle != nodeB.Handle)
                {
                    double dx = nodeA.X - nodeB.X;
                    double dy = nodeA.Y - nodeB.Y;
                    double weight = Math.Sqrt(dx * dx + dy * dy);

                    var edge = new WalkwayEdge
                    {
                        Handle = line.Handle,
                        NodeAHandle = nodeA.Handle,
                        NodeBHandle = nodeB.Handle,
                        Weight = weight
                    };
                    Edges[edge.Handle] = edge;

                    nodeA.AdjacentEdgeHandles.Add(edge.Handle);
                    nodeB.AdjacentEdgeHandles.Add(edge.Handle);
                }
            }
        }
    }

    public WalkwayNode? FindNearestNode(double x, double y, double maxDistance)
    {
        WalkwayNode? nearest = null;
        double bestDist = maxDistance;

        foreach (var node in Nodes.Values)
        {
            double dx = node.X - x;
            double dy = node.Y - y;
            double dist = Math.Sqrt(dx * dx + dy * dy);
            if (dist < bestDist)
            {
                bestDist = dist;
                nearest = node;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Dijkstra from a given node to the nearest entrance node.
    /// Returns the path as a list of node handles, or null if no path found.
    /// </summary>
    public List<ulong>? FindPathToNearestEntrance(ulong fromNodeHandle)
    {
        if (!Nodes.ContainsKey(fromNodeHandle))
            return null;

        // Check if the start node itself is an entrance
        if (Nodes[fromNodeHandle].IsEntrance)
            return new List<ulong> { fromNodeHandle };

        var dist = new Dictionary<ulong, double>();
        var prev = new Dictionary<ulong, ulong>();
        var visited = new HashSet<ulong>();
        var pq = new PriorityQueue<ulong, double>();

        dist[fromNodeHandle] = 0;
        pq.Enqueue(fromNodeHandle, 0);

        ulong? nearestEntrance = null;

        while (pq.Count > 0)
        {
            var current = pq.Dequeue();

            if (visited.Contains(current))
                continue;
            visited.Add(current);

            if (Nodes[current].IsEntrance)
            {
                nearestEntrance = current;
                break;
            }

            var node = Nodes[current];
            foreach (var edgeHandle in node.AdjacentEdgeHandles)
            {
                if (!Edges.TryGetValue(edgeHandle, out var edge))
                    continue;

                var neighbor = edge.NodeAHandle == current ? edge.NodeBHandle : edge.NodeAHandle;

                if (visited.Contains(neighbor) || !Nodes.ContainsKey(neighbor))
                    continue;

                double newDist = dist[current] + edge.Weight;
                if (!dist.ContainsKey(neighbor) || newDist < dist[neighbor])
                {
                    dist[neighbor] = newDist;
                    prev[neighbor] = current;
                    pq.Enqueue(neighbor, newDist);
                }
            }
        }

        if (nearestEntrance == null)
            return null;

        // Reconstruct path
        var path = new List<ulong>();
        var step = nearestEntrance.Value;
        while (step != fromNodeHandle)
        {
            path.Add(step);
            if (!prev.ContainsKey(step))
                return null;
            step = prev[step];
        }
        path.Add(fromNodeHandle);
        path.Reverse();

        return path;
    }

    /// <summary>
    /// Finds the path coordinates from a point to the nearest entrance.
    /// Returns the coordinate list and total distance, or null if no path found.
    /// </summary>
    public (List<(double x, double y)> path, double distance)? FindPathCoordinatesToEntrance(
        double unitX, double unitY, double maxDistance)
    {
        var nearestNode = FindNearestNode(unitX, unitY, maxDistance);
        if (nearestNode == null)
            return null;

        var handlePath = FindPathToNearestEntrance(nearestNode.Handle);
        if (handlePath == null)
            return null;

        var coordinates = new List<(double x, double y)>();
        double totalDistance = 0;

        for (int i = 0; i < handlePath.Count; i++)
        {
            var node = Nodes[handlePath[i]];
            coordinates.Add((node.X, node.Y));

            if (i > 0)
            {
                var prev = Nodes[handlePath[i - 1]];
                double dx = node.X - prev.X;
                double dy = node.Y - prev.Y;
                totalDistance += Math.Sqrt(dx * dx + dy * dy);
            }
        }

        return (coordinates, totalDistance);
    }

    /// <summary>
    /// Given a path of node handles, returns the edge handles connecting them.
    /// </summary>
    public List<ulong> GetEdgeHandlesForPath(List<ulong> nodeHandles)
    {
        var edgeHandles = new List<ulong>();

        for (int i = 0; i < nodeHandles.Count - 1; i++)
        {
            var a = nodeHandles[i];
            var b = nodeHandles[i + 1];

            foreach (var edge in Edges.Values)
            {
                if ((edge.NodeAHandle == a && edge.NodeBHandle == b) ||
                    (edge.NodeAHandle == b && edge.NodeBHandle == a))
                {
                    edgeHandles.Add(edge.Handle);
                    break;
                }
            }
        }

        return edgeHandles;
    }

    /// <summary>
    /// Returns all node + edge handles for a path, suitable for highlighting.
    /// </summary>
    public HashSet<ulong> GetAllHandlesForPath(List<ulong> nodeHandles)
    {
        var handles = new HashSet<ulong>(nodeHandles);
        foreach (var edgeHandle in GetEdgeHandlesForPath(nodeHandles))
        {
            handles.Add(edgeHandle);
        }
        return handles;
    }
}
