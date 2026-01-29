namespace mPrismaMapsWPF.Services;

/// <summary>
/// Represents a command that can be undone and redone.
/// </summary>
public interface IUndoableCommand
{
    /// <summary>
    /// Description of what this command does (for UI display).
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Execute the command.
    /// </summary>
    void Execute();

    /// <summary>
    /// Undo the command.
    /// </summary>
    void Undo();
}

/// <summary>
/// Service for managing undo/redo operations.
/// </summary>
public interface IUndoRedoService
{
    /// <summary>
    /// Whether there are commands that can be undone.
    /// </summary>
    bool CanUndo { get; }

    /// <summary>
    /// Whether there are commands that can be redone.
    /// </summary>
    bool CanRedo { get; }

    /// <summary>
    /// Description of the next command to undo (or null if none).
    /// </summary>
    string? UndoDescription { get; }

    /// <summary>
    /// Description of the next command to redo (or null if none).
    /// </summary>
    string? RedoDescription { get; }

    /// <summary>
    /// Event raised when the undo/redo state changes.
    /// </summary>
    event EventHandler? StateChanged;

    /// <summary>
    /// Execute a command and add it to the undo stack.
    /// </summary>
    void Execute(IUndoableCommand command);

    /// <summary>
    /// Undo the most recent command.
    /// </summary>
    void Undo();

    /// <summary>
    /// Redo the most recently undone command.
    /// </summary>
    void Redo();

    /// <summary>
    /// Clear all undo and redo history.
    /// </summary>
    void Clear();
}
