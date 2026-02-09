using ACadSharp.Entities;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class MoveEntitiesCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly IReadOnlyList<Entity> _entities;
    private readonly double _dx;
    private readonly double _dy;

    public string Description { get; }

    public MoveEntitiesCommand(CadDocumentModel document, IReadOnlyList<Entity> entities, double dx, double dy)
    {
        _document = document;
        _entities = entities;
        _dx = dx;
        _dy = dy;
        Description = _entities.Count == 1
            ? $"Move {_entities[0].GetType().Name}"
            : $"Move {_entities.Count} entities";
    }

    public void Execute()
    {
        foreach (var entity in _entities)
        {
            EntityTransformHelper.TranslateEntity(entity, _dx, _dy);
        }
        _document.IsDirty = true;
    }

    public void Undo()
    {
        foreach (var entity in _entities)
        {
            EntityTransformHelper.TranslateEntity(entity, -_dx, -_dy);
        }
        _document.IsDirty = true;
    }
}
