using FluentAssertions;
using mPrismaMapsWPF.Services;
using mPrismaMapsWPF.Tests.TestHelpers;

namespace mPrismaMapsWPF.Tests.Services;

public class SelectionServiceTests
{
    private readonly SelectionService _service = new();

    [Fact]
    public void Select_AddsSingleEntity()
    {
        var line = EntityFactory.CreateLine(0, 0, 10, 10);
        var model = EntityFactory.CreateEntityModel(line);

        _service.Select(model);

        _service.SelectedEntities.Should().ContainSingle().Which.Should().Be(model);
        model.IsSelected.Should().BeTrue();
    }

    [Fact]
    public void Select_WithoutAddToSelection_ClearsPrevious()
    {
        var line1 = EntityFactory.CreateLine(0, 0, 10, 10);
        var line2 = EntityFactory.CreateLine(20, 20, 30, 30);
        var model1 = EntityFactory.CreateEntityModel(line1);
        var model2 = EntityFactory.CreateEntityModel(line2);

        _service.Select(model1);
        _service.Select(model2, addToSelection: false);

        _service.SelectedEntities.Should().ContainSingle().Which.Should().Be(model2);
        model1.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void Select_WithAddToSelection_KeepsPrevious()
    {
        var line1 = EntityFactory.CreateLine(0, 0, 10, 10);
        var line2 = EntityFactory.CreateLine(20, 20, 30, 30);
        var model1 = EntityFactory.CreateEntityModel(line1);
        var model2 = EntityFactory.CreateEntityModel(line2);

        _service.Select(model1);
        _service.Select(model2, addToSelection: true);

        _service.SelectedEntities.Should().HaveCount(2);
    }

    [Fact]
    public void SelectMultiple_AddsAll()
    {
        var model1 = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        var model2 = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(20, 20, 30, 30));

        _service.SelectMultiple(new[] { model1, model2 });

        _service.SelectedEntities.Should().HaveCount(2);
    }

    [Fact]
    public void Deselect_RemovesEntity()
    {
        var model = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        _service.Select(model);
        _service.Deselect(model);

        _service.SelectedEntities.Should().BeEmpty();
        model.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void ClearSelection_EmptiesAll()
    {
        var model1 = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        var model2 = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(20, 20, 30, 30));
        _service.SelectMultiple(new[] { model1, model2 });

        _service.ClearSelection();

        _service.SelectedEntities.Should().BeEmpty();
        model1.IsSelected.Should().BeFalse();
        model2.IsSelected.Should().BeFalse();
    }

    [Fact]
    public void ToggleSelection_Adds_WhenNotSelected()
    {
        var model = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        _service.ToggleSelection(model);
        _service.SelectedEntities.Should().Contain(model);
    }

    [Fact]
    public void ToggleSelection_Removes_WhenAlreadySelected()
    {
        var model = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        _service.Select(model);
        _service.ToggleSelection(model);
        _service.SelectedEntities.Should().NotContain(model);
    }

    [Fact]
    public void Select_LockedEntity_IsRejected()
    {
        var model = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        model.IsLocked = true;

        _service.Select(model);

        _service.SelectedEntities.Should().BeEmpty();
    }

    [Fact]
    public void SelectMultiple_SkipsLockedEntities()
    {
        var model1 = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        var model2 = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(20, 20, 30, 30));
        model2.IsLocked = true;

        _service.SelectMultiple(new[] { model1, model2 });

        _service.SelectedEntities.Should().ContainSingle().Which.Should().Be(model1);
    }

    [Fact]
    public void SelectionChanged_FiresWithCorrectArgs()
    {
        var model = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        SelectionChangedEventArgs? args = null;
        _service.SelectionChanged += (_, e) => args = e;

        _service.Select(model);

        args.Should().NotBeNull();
        args!.AddedEntities.Should().Contain(model);
        args.SelectedEntities.Should().Contain(model);
    }

    [Fact]
    public void SelectionChanged_FiresOnClear()
    {
        var model = EntityFactory.CreateEntityModel(EntityFactory.CreateLine(0, 0, 10, 10));
        _service.Select(model);

        SelectionChangedEventArgs? args = null;
        _service.SelectionChanged += (_, e) => args = e;
        _service.ClearSelection();

        args.Should().NotBeNull();
        args!.RemovedEntities.Should().Contain(model);
    }
}
