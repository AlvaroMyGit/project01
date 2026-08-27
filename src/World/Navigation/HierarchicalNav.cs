// HierarchicalNav.cs — Global graph + micro NavMesh
using System.Numerics;

namespace StalkerALifeSandbox.World.Navigation;

/// <summary>Edge in the navigation graph.</summary>
public sealed class NavEdge
{
    public string TargetNodeId { get; init; } = "";
    public float  Cost         { get; init; } = 1f;
}

/// <summary>Node in the navigation graph.</summary>
public sealed class NavNode
{
    public string         Id       { get; init; } = "";
    public Vector3        Position { get; init; }
    public List<NavEdge>  Edges    { get; } = new();
}

/// <summary>
/// Two-tier navigation: a coarse global graph for cross-zone
/// pathing and a local micro mesh for detailed movement.
/// </summary>
public sealed class HierarchicalNav
{
    private readonly Dictionary<string, NavNode> _nodes = new();
    public IReadOnlyDictionary<string, NavNode> Nodes => _nodes;

    public void AddNode(NavNode node) => _nodes[node.Id] = node;

    public void Connect(string fromId, string toId, float cost)
    {
        if (!_nodes.TryGetValue(fromId, out var from)) return;
        from.Edges.Add(new NavEdge { TargetNodeId = toId, Cost = cost });

        // bidirectional
        if (_nodes.TryGetValue(toId, out var to))
            to.Edges.Add(new NavEdge { TargetNodeId = fromId, Cost = cost });
    }

    /// <summary>
    /// Simple A* path from start to goal node. Returns ordered
    /// list of node IDs, or null if unreachable.
    /// </summary>
    public List<string>? FindPath(string startId, string goalId)
    {
        if (!_nodes.ContainsKey(startId) || !_nodes.ContainsKey(goalId))
            return null;

        var open = new PriorityQueue<string, float>();
        var cameFrom = new Dictionary<string, string>();
        var costSoFar = new Dictionary<string, float> { [startId] = 0f };
        open.Enqueue(startId, 0f);

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (current == goalId)
                return ReconstructPath(cameFrom, startId, goalId);

            foreach (var edge in _nodes[current].Edges)
            {
                float newCost = costSoFar[current] + edge.Cost;
                if (!costSoFar.ContainsKey(edge.TargetNodeId) ||
                    newCost < costSoFar[edge.TargetNodeId])
                {
                    costSoFar[edge.TargetNodeId] = newCost;
                    float h = Vector3.Distance(
                        _nodes[edge.TargetNodeId].Position,
                        _nodes[goalId].Position);
                    open.Enqueue(edge.TargetNodeId, newCost + h);
                    cameFrom[edge.TargetNodeId] = current;
                }
            }
        }
        return null;
    }

    private static List<string> ReconstructPath(
        Dictionary<string, string> cameFrom, string start, string goal)
    {
        var path = new List<string> { goal };
        var cur = goal;
        while (cur != start)
        {
            cur = cameFrom[cur];
            path.Add(cur);
        }
        path.Reverse();
        return path;
    }
}
