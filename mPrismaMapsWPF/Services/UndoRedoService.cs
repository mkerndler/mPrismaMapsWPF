namespace mPrismaMapsWPF.Services;

/// <summary>
/// Implementation of undo/redo service with a maximum stack size of 100 commands.
/// </summary>
public class UndoRedoService : IUndoRedoService
{
    private const int MaxStackSize = 100;

    private readonly Stack<IUndoableCommand> _undoStack = new();
    private readonly Stack<IUndoableCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public string? UndoDescription => _undoStack.TryPeek(out var command) ? command.Description : null;
    public string? RedoDescription => _redoStack.TryPeek(out var command) ? command.Description : null;

    public event EventHandler? StateChanged;

    public void Execute(IUndoableCommand command)
    {
        command.Execute();

        _undoStack.Push(command);
        _redoStack.Clear();

        // Trim undo stack if it exceeds max size
        TrimStack(_undoStack);

        RaiseStateChanged();
    }

    public void Undo()
    {
        if (!CanUndo)
            return;

        var command = _undoStack.Pop();
        command.Undo();
        _redoStack.Push(command);

        RaiseStateChanged();
    }

    public void Redo()
    {
        if (!CanRedo)
            return;

        var command = _redoStack.Pop();
        command.Execute();
        _undoStack.Push(command);

        RaiseStateChanged();
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        RaiseStateChanged();
    }

    private static void TrimStack(Stack<IUndoableCommand> stack)
    {
        if (stack.Count <= MaxStackSize)
            return;

        // Convert to list, keep most recent MaxStackSize items
        var items = stack.ToArray();
        stack.Clear();

        // Push back in reverse order (oldest first, so newest ends up on top)
        for (int i = MaxStackSize - 1; i >= 0; i--)
        {
            stack.Push(items[i]);
        }
    }

    private void RaiseStateChanged()
    {
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}
