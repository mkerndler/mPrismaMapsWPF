using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class ScaleMapCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly double _factor;

    public string Description => $"Scale map by {_factor}x";

    public ScaleMapCommand(CadDocumentModel document, double factor)
    {
        _document = document;
        _factor = factor;
    }

    public void Execute()
    {
        if (_document.Document == null)
            return;

        foreach (var entity in _document.ModelSpaceEntities.ToList())
            EntityTransformHelper.ScaleEntity(entity, _factor);

        _document.IsDirty = true;
    }

    public void Undo()
    {
        if (_document.Document == null)
            return;

        double inverseFactor = 1.0 / _factor;
        foreach (var entity in _document.ModelSpaceEntities.ToList())
            EntityTransformHelper.ScaleEntity(entity, inverseFactor);

        _document.IsDirty = true;
    }
}
