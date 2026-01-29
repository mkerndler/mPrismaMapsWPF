using ACadSharp;
using ACadSharp.Entities;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

/// <summary>
/// Command to change entity colors with undo support.
/// </summary>
public class ChangeEntityColorCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly IReadOnlyList<EntityModel> _entityModels;
    private readonly Color _newColor;
    private readonly List<(Entity Entity, Color OriginalColor)> _originalColors = new();

    public string Description { get; }

    /// <summary>
    /// Creates a ChangeEntityColorCommand.
    /// </summary>
    /// <param name="document">The CAD document model.</param>
    /// <param name="entityModels">The entities to change color.</param>
    /// <param name="newColor">The new color to apply.</param>
    public ChangeEntityColorCommand(
        CadDocumentModel document,
        IEnumerable<EntityModel> entityModels,
        Color newColor)
    {
        _document = document;
        _entityModels = entityModels.ToList();
        _newColor = newColor;

        Description = _entityModels.Count == 1
            ? $"Change color of {_entityModels[0].TypeName}"
            : $"Change color of {_entityModels.Count} entities";
    }

    public void Execute()
    {
        _originalColors.Clear();

        foreach (var entityModel in _entityModels)
        {
            var entity = entityModel.Entity;
            _originalColors.Add((entity, entity.Color));
            entity.Color = _newColor;
        }

        _document.IsDirty = true;
    }

    public void Undo()
    {
        foreach (var (entity, originalColor) in _originalColors)
        {
            entity.Color = originalColor;
        }

        _document.IsDirty = true;
    }
}
