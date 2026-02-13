using FluentAssertions;
using mPrismaMapsWPF.Services;
using Moq;

namespace mPrismaMapsWPF.Tests.Services;

public class UndoRedoServiceTests
{
    private readonly UndoRedoService _service = new();

    private static Mock<IUndoableCommand> CreateMockCommand(string description = "test")
    {
        var mock = new Mock<IUndoableCommand>();
        mock.Setup(c => c.Description).Returns(description);
        return mock;
    }

    [Fact]
    public void Execute_CallsCommandExecute()
    {
        var cmd = CreateMockCommand();
        _service.Execute(cmd.Object);
        cmd.Verify(c => c.Execute(), Times.Once);
    }

    [Fact]
    public void Execute_MakesCanUndoTrue()
    {
        _service.CanUndo.Should().BeFalse();
        _service.Execute(CreateMockCommand().Object);
        _service.CanUndo.Should().BeTrue();
    }

    [Fact]
    public void Execute_ClearsRedoStack()
    {
        var cmd1 = CreateMockCommand();
        var cmd2 = CreateMockCommand();
        _service.Execute(cmd1.Object);
        _service.Undo();
        _service.CanRedo.Should().BeTrue();

        _service.Execute(cmd2.Object);
        _service.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Undo_CallsCommandUndo()
    {
        var cmd = CreateMockCommand();
        _service.Execute(cmd.Object);
        _service.Undo();
        cmd.Verify(c => c.Undo(), Times.Once);
    }

    [Fact]
    public void Undo_MovesToRedoStack()
    {
        _service.Execute(CreateMockCommand().Object);
        _service.Undo();

        _service.CanUndo.Should().BeFalse();
        _service.CanRedo.Should().BeTrue();
    }

    [Fact]
    public void Redo_CallsCommandExecute()
    {
        var cmd = CreateMockCommand();
        _service.Execute(cmd.Object);
        _service.Undo();
        _service.Redo();

        cmd.Verify(c => c.Execute(), Times.Exactly(2));
    }

    [Fact]
    public void Redo_MovesBackToUndoStack()
    {
        _service.Execute(CreateMockCommand().Object);
        _service.Undo();
        _service.Redo();

        _service.CanUndo.Should().BeTrue();
        _service.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void Clear_EmptiesBothStacks()
    {
        _service.Execute(CreateMockCommand().Object);
        _service.Execute(CreateMockCommand().Object);
        _service.Undo();

        _service.Clear();

        _service.CanUndo.Should().BeFalse();
        _service.CanRedo.Should().BeFalse();
    }

    [Fact]
    public void StateChanged_FiresOnExecute()
    {
        bool fired = false;
        _service.StateChanged += (_, _) => fired = true;
        _service.Execute(CreateMockCommand().Object);
        fired.Should().BeTrue();
    }

    [Fact]
    public void StateChanged_FiresOnUndo()
    {
        _service.Execute(CreateMockCommand().Object);
        bool fired = false;
        _service.StateChanged += (_, _) => fired = true;
        _service.Undo();
        fired.Should().BeTrue();
    }

    [Fact]
    public void StateChanged_FiresOnRedo()
    {
        _service.Execute(CreateMockCommand().Object);
        _service.Undo();
        bool fired = false;
        _service.StateChanged += (_, _) => fired = true;
        _service.Redo();
        fired.Should().BeTrue();
    }

    [Fact]
    public void UndoDescription_ReturnsTopCommandDescription()
    {
        _service.Execute(CreateMockCommand("Move entity").Object);
        _service.UndoDescription.Should().Be("Move entity");
    }

    [Fact]
    public void RedoDescription_ReturnsTopRedoCommandDescription()
    {
        _service.Execute(CreateMockCommand("Move entity").Object);
        _service.Undo();
        _service.RedoDescription.Should().Be("Move entity");
    }

    [Fact]
    public void UndoDescription_NullWhenEmpty()
    {
        _service.UndoDescription.Should().BeNull();
    }

    [Fact]
    public void StackTrimsAt100Commands()
    {
        for (int i = 0; i < 110; i++)
        {
            _service.Execute(CreateMockCommand($"cmd{i}").Object);
        }

        // Should still work, but oldest commands trimmed
        int undoCount = 0;
        while (_service.CanUndo)
        {
            _service.Undo();
            undoCount++;
        }

        undoCount.Should().Be(100);
    }

    [Fact]
    public void Undo_WhenEmpty_DoesNothing()
    {
        var act = () => _service.Undo();
        act.Should().NotThrow();
    }

    [Fact]
    public void Redo_WhenEmpty_DoesNothing()
    {
        var act = () => _service.Redo();
        act.Should().NotThrow();
    }
}
