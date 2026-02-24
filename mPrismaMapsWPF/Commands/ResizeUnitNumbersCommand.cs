using ACadSharp.Entities;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Services;

namespace mPrismaMapsWPF.Commands;

public class ResizeUnitNumbersCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    private readonly List<MText> _unitNumbers;
    private readonly double _newHeight;
    private readonly List<(MText Entity, double OriginalHeight)> _originalHeights = new();

    public string Description => $"Resize {_unitNumbers.Count} unit numbers to height {_newHeight}";

    public ResizeUnitNumbersCommand(CadDocumentModel document,
        IEnumerable<MText> unitNumbers, double newHeight)
    {
        _document = document;
        _unitNumbers = unitNumbers.ToList();
        _newHeight = newHeight;
    }

    public void Execute()
    {
        _originalHeights.Clear();
        foreach (var mt in _unitNumbers)
        {
            _originalHeights.Add((mt, mt.Height));
            mt.Height = _newHeight;
        }
        _document.IsDirty = true;
    }

    public void Undo()
    {
        foreach (var (mt, h) in _originalHeights)
            mt.Height = h;
        _document.IsDirty = true;
    }
}
