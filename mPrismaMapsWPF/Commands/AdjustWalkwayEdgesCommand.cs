using ACadSharp.Entities;
using CSMath;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Adjusts walkway edge (Line) endpoints when a connected node is moved.
/// Only moves the endpoint(s) attached to the moved node, not the whole line.
/// </summary>
public class AdjustWalkwayEdgesCommand : IUndoableCommand
{
    private readonly List<(Line line, bool adjustStart, bool adjustEnd)> _adjustments;
    private readonly double _dx;
    private readonly double _dy;

    public string Description => "Adjust walkway edges";

    public AdjustWalkwayEdgesCommand(
        List<(Line line, bool adjustStart, bool adjustEnd)> adjustments,
        double dx, double dy)
    {
        _adjustments = adjustments;
        _dx = dx;
        _dy = dy;
    }

    public void Execute()
    {
        foreach (var (line, adjustStart, adjustEnd) in _adjustments)
        {
            if (adjustStart)
            {
                line.StartPoint = new XYZ(
                    line.StartPoint.X + _dx,
                    line.StartPoint.Y + _dy,
                    line.StartPoint.Z);
            }

            if (adjustEnd)
            {
                line.EndPoint = new XYZ(
                    line.EndPoint.X + _dx,
                    line.EndPoint.Y + _dy,
                    line.EndPoint.Z);
            }
        }
    }

    public void Undo()
    {
        foreach (var (line, adjustStart, adjustEnd) in _adjustments)
        {
            if (adjustStart)
            {
                line.StartPoint = new XYZ(
                    line.StartPoint.X - _dx,
                    line.StartPoint.Y - _dy,
                    line.StartPoint.Z);
            }

            if (adjustEnd)
            {
                line.EndPoint = new XYZ(
                    line.EndPoint.X - _dx,
                    line.EndPoint.Y - _dy,
                    line.EndPoint.Z);
            }
        }
    }
}
