using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class TransformEntitiesCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly IReadOnlyList<Entity> _entities;
    private readonly double _pivotX;
    private readonly double _pivotY;
    private readonly double? _scaleX;
    private readonly double? _scaleY;
    private readonly double? _angleRadians;

    public string Description { get; }

    public TransformEntitiesCommand(
        CadDocumentModel document,
        IReadOnlyList<Entity> entities,
        double pivotX,
        double pivotY,
        double? scaleX = null,
        double? scaleY = null,
        double? angleRadians = null)
    {
        _document = document;
        _entities = entities;
        _pivotX = pivotX;
        _pivotY = pivotY;
        _scaleX = scaleX;
        _scaleY = scaleY;
        _angleRadians = angleRadians;

        bool hasScale = _scaleX.HasValue && _scaleY.HasValue;
        bool hasRotation = _angleRadians.HasValue && Math.Abs(_angleRadians.Value) > 0.0001;
        string entityDesc = _entities.Count == 1
            ? _entities[0].GetType().Name
            : $"{_entities.Count} entities";

        Description = (hasScale, hasRotation) switch
        {
            (true, true) => $"Transform {entityDesc}",
            (true, false) => $"Scale {entityDesc}",
            (false, true) => $"Rotate {entityDesc}",
            _ => $"Transform {entityDesc}"
        };
    }

    public void Execute()
    {
        foreach (var entity in _entities)
        {
            if (_scaleX.HasValue && _scaleY.HasValue)
                EntityTransformHelper.ScaleEntity(entity, _pivotX, _pivotY, _scaleX.Value, _scaleY.Value);

            if (_angleRadians.HasValue)
                EntityTransformHelper.RotateEntity(entity, _pivotX, _pivotY, _angleRadians.Value);
        }
        _document.IsDirty = true;
    }

    public void Undo()
    {
        foreach (var entity in _entities)
        {
            if (_angleRadians.HasValue)
                EntityTransformHelper.RotateEntity(entity, _pivotX, _pivotY, -_angleRadians.Value);

            if (_scaleX.HasValue && _scaleY.HasValue
                && Math.Abs(_scaleX.Value) > 1e-6 && Math.Abs(_scaleY.Value) > 1e-6)
                EntityTransformHelper.ScaleEntity(entity, _pivotX, _pivotY,
                    1.0 / _scaleX.Value, 1.0 / _scaleY.Value);
        }
        _document.IsDirty = true;
    }
}
