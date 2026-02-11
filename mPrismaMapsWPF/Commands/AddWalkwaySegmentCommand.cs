using ACadSharp;
using ACadSharp.Entities;
using ACadSharp.Tables;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class AddWalkwaySegmentCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly double _nodeX;
    private readonly double _nodeY;
    private readonly double _nodeRadius;
    private readonly ulong? _snappedToHandle;
    private readonly double? _prevX;
    private readonly double? _prevY;
    private readonly ulong? _previousNodeHandle;

    private Circle? _createdNode;
    private Line? _createdEdge;
    private BlockRecord? _owner;

    public ulong? CreatedNodeHandle => _createdNode?.Handle;
    public ulong? CreatedEdgeHandle => _createdEdge?.Handle;

    public string Description => "Add walkway segment";

    public AddWalkwaySegmentCommand(
        CadDocumentModel document,
        double nodeX, double nodeY,
        double nodeRadius,
        ulong? snappedToHandle,
        double? prevX, double? prevY,
        ulong? previousNodeHandle)
    {
        _document = document;
        _nodeX = nodeX;
        _nodeY = nodeY;
        _nodeRadius = nodeRadius;
        _snappedToHandle = snappedToHandle;
        _prevX = prevX;
        _prevY = prevY;
        _previousNodeHandle = previousNodeHandle;
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        _owner = _document.Document.ModelSpace;
        var layer = _document.GetOrCreateWalkwaysLayer();
        if (layer == null)
            return;

        // Create node circle if not snapping to an existing node
        if (_snappedToHandle == null)
        {
            _createdNode = new Circle
            {
                Center = new CSMath.XYZ(_nodeX, _nodeY, 0),
                Radius = _nodeRadius,
                Layer = layer,
                Color = new Color(5) // blue = regular node
            };
            _owner.Entities.Add(_createdNode);
        }

        // Create edge line if there's a previous node
        if (_prevX.HasValue && _prevY.HasValue)
        {
            _createdEdge = new Line
            {
                StartPoint = new CSMath.XYZ(_prevX.Value, _prevY.Value, 0),
                EndPoint = new CSMath.XYZ(_nodeX, _nodeY, 0),
                Layer = layer,
                Color = new Color(5) // blue
            };
            _owner.Entities.Add(_createdEdge);
        }

        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_owner == null)
            return;

        if (_createdEdge != null)
            _owner.Entities.Remove(_createdEdge);

        if (_createdNode != null)
            _owner.Entities.Remove(_createdNode);

        _document.IsDirty = true;
    }
}
