# mPrismaMapsWPF — Developer Documentation

## Table of Contents

1. [Project Overview](#1-project-overview)
2. [Solution Structure](#2-solution-structure)
3. [Architecture](#3-architecture)
4. [Startup & Dependency Injection](#4-startup--dependency-injection)
5. [Coordinate System](#5-coordinate-system)
6. [Models](#6-models)
7. [Services](#7-services)
8. [Commands (Undo/Redo)](#8-commands-undoredo)
9. [ViewModels](#9-viewmodels)
10. [Views & Dialogs](#10-views--dialogs)
11. [CadCanvas Control](#11-cadcanvas-control)
12. [Drawing Tools](#12-drawing-tools)
13. [Rendering Pipeline](#13-rendering-pipeline)
14. [Helpers & Utilities](#14-helpers--utilities)
15. [Special Layers](#15-special-layers)
16. [Key Patterns & Conventions](#16-key-patterns--conventions)
17. [Data Flow Walkthrough](#17-data-flow-walkthrough)
18. [Testing](#18-testing)

---

## 1. Project Overview

**mPrismaMapsWPF** is a WPF desktop application for viewing, editing, and managing CAD drawings (DWG/DXF files). Its primary purpose is to prepare mall/store floor plan maps for the **MPOL** indoor positioning system by:

- Loading and displaying DWG/DXF files
- Editing entities (move, scale, rotate, change color/layer)
- Placing unit numbers (MText labels) on unit areas
- Drawing walkway graphs for shortest-path calculation
- Generating unit area polygons and background contours
- Exporting/deploying the resulting map to a SQL Server database

**Target:** .NET 10.0, Windows only (WPF). Nullable reference types and implicit usings enabled.

---

## 2. Solution Structure

```
mPrismaMapsWPF.slnx
├── mPrismaMapsWPF/                     # Main WPF application
│   ├── App.xaml / App.xaml.cs          # Entry point, DI, logging, exception handlers
│   ├── MainWindow.xaml / .cs           # Main window, event wiring
│   ├── Commands/                       # 20 IUndoableCommand implementations
│   ├── Controls/                       # CadCanvas custom FrameworkElement
│   ├── Converters/                     # WPF value converters
│   ├── Drawing/                        # Drawing tool implementations
│   ├── Helpers/                        # Static utility classes
│   ├── Models/                         # Domain models and observable wrappers
│   ├── Rendering/                      # Per-entity Skia renderers
│   ├── Services/                       # Business logic services
│   ├── Themes/                         # Dark theme XAML resources
│   ├── ViewModels/                     # MVVM ViewModels
│   └── Views/                          # Dialog windows
└── mPrismaMapsWPF.Tests/               # xUnit test project
    ├── Commands/
    ├── Drawing/
    ├── Helpers/
    ├── Models/
    ├── Services/
    └── ViewModels/
```

---

## 3. Architecture

The application follows **MVVM** (Model-View-ViewModel) with a service layer. All services are registered as singletons in a Microsoft DI container.

```
┌─────────────────────────────────────────────────────────────┐
│  Views (MainWindow + Dialogs)                               │
│    - Forwards UI events to ViewModel via method calls       │
│    - Shows dialogs on ViewModel events                      │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│  ViewModels (MainWindowViewModel, LayerPanelViewModel, …)   │
│    - RelayCommands bound to menu/toolbar                    │
│    - Fires events to signal View (ZoomToFit, ShowDialog, …) │
│    - Delegates state changes to Services via Execute()      │
└──────┬─────────────────┬─────────────────┬──────────────────┘
       │                 │                 │
┌──────▼──────┐  ┌───────▼──────┐  ┌──────▼──────────┐
│  Document   │  │  Selection   │  │  UndoRedo       │
│  Service    │  │  Service     │  │  Service        │
└──────┬──────┘  └──────────────┘  └──────┬──────────┘
       │                                  │
┌──────▼────────────────────────────────────────────────────┐
│  CadDocumentModel  (wraps ACadSharp CadDocument)          │
│    - ModelSpaceEntities, Layers, Blocks, IsDirty         │
└───────────────────────────────────────────────────────────┘
```

**Key design decisions:**

- `CadCanvas` is a low-level FrameworkElement with no direct ViewModel binding; it communicates with `MainWindow.xaml.cs` via events, which forwards to the ViewModel.
- Every user action that changes document state goes through an `IUndoableCommand`.
- The `SelectionService` is the single source of truth for selected entities.

---

## 4. Startup & Dependency Injection

**`App.xaml.cs`** configures Serilog, builds the DI container, and creates `MainWindow`.

### Logging

```
Log path:       {AppDirectory}/logs/mPrismaMaps-.log
Rolling:        Daily
Max file size:  10 MB
Retention:      30 files
Min level:      Debug
```

### DI Registrations (all singletons)

| Type | Interface | Class |
|------|-----------|-------|
| Document I/O | `IDocumentService` | `DocumentService` |
| Selection state | `ISelectionService` | `SelectionService` |
| Undo/Redo | `IUndoRedoService` | `UndoRedoService` |
| Walkway graph | `IWalkwayService` | `WalkwayService` |
| SQL deploy | `IDeployService` | `DeployService` |
| File backups | `IBackupService` | `BackupService` |
| DWG merge | `IMergeDocumentService` | `MergeDocumentService` |
| Main ViewModel | — | `MainWindowViewModel` |
| Main Window | — | `MainWindow` |

### Global Exception Handlers

Three handlers are installed to log before crash:

```csharp
Application.DispatcherUnhandledException       // UI thread
AppDomain.CurrentDomain.UnhandledException     // Non-UI thread
TaskScheduler.UnobservedTaskException          // Async tasks
```

All call `Log.CloseAndFlush()` before allowing the process to exit.

---

## 5. Coordinate System

CAD space and screen space use opposite Y-axis orientations:

```
CAD space:     Y increases upward   (standard mathematical)
Screen space:  Y increases downward (WPF)
```

### Transformation Formulas

**CAD → Screen:**
```
screenX = (cadX + offset.X) * scale
screenY = (-cadY + offset.Y) * scale
```

**Screen → CAD:**
```
cadX =  screenX / scale - offset.X
cadY = -(screenY / scale - offset.Y)
```

`offset` and `scale` are maintained by `CadCanvas`. When view transforms (FlipX, FlipY, ViewRotation) are applied, they are composed on top of this base transform.

---

## 6. Models

### `CadDocumentModel`

Wraps an ACadSharp `CadDocument`. All services and commands hold a reference to this single instance.

```csharp
CadDocument?           Document              // Underlying ACadSharp document (null = no file open)
string?                FilePath              // Null for imported/unsaved documents
bool                   IsDirty              // Unsaved changes; triggers save prompt
IEnumerable<Entity>    ModelSpaceEntities   // Entities in model space
IEnumerable<Layer>     Layers               // All document layers
IEnumerable<BlockRecord> Blocks             // All block records
```

**Special layer name constants:**

| Constant | Value | Color (ACI) |
|----------|-------|-------------|
| `UserDrawingsLayerName` | `"User Drawings"` | Magenta (6) |
| `UnitNumbersLayerName` | `"Unit Numbers"` | Green (3) |
| `WalkwaysLayerName` | `"Walkways"` | Blue (5) |
| `UnitAreasLayerName` | `"Unit Areas"` | Cyan (4) |
| `BackgroundContoursLayerName` | `"Background Contours"` | Red (1) |

**Key methods:**

```csharp
Layer? GetOrCreateUserDrawingsLayer()
Layer? GetOrCreateUnitNumbersLayer()
Layer? GetOrCreateWalkwaysLayer()
Layer? GetOrCreateUnitAreasLayer()
Layer? GetOrCreateBackgroundContoursLayer()
void   EnsureDocumentExists()    // Creates a blank CadDocument if null
void   Load(CadDocument, string? filePath)
void   Clear()
Extents GetExtents()             // Bounding box of all entities (PLINQ)
```

---

### `EntityModel`

Observable wrapper (extends `ObservableObject`) for a single ACadSharp `Entity`. One `EntityModel` per entity is kept in `MainWindowViewModel.Entities` and `_entityLookup`.

```csharp
Entity    Entity        // Wrapped entity (read-only)
string    TypeName      // e.g. "Line", "MText"
string    LayerName     // Layer.Name or "0"
ulong     Handle        // ACadSharp handle (unique per document)
string    TypeIcon      // Unicode glyph for UI display
string    DisplayName   // "TypeName (Handle)"
bool      IsSelected    // Observable; drives canvas highlight
bool      IsLocked      // Observable; prevents selection
```

`GetProperty(string name)` returns entity-specific values by name string (used by the properties panel).

---

### `LayerModel`

Observable wrapper for an ACadSharp `Layer`.

```csharp
string  Name        // Layer name
bool    IsVisible   // Observable
bool    IsFrozen    // Observable (from Layer.Flags)
bool    IsSelected  // Observable (UI selection)
bool    IsLocked    // Observable
Color   Color       // WPF Color from ACI index
```

---

### `WalkwayGraph`, `WalkwayNode`, `WalkwayEdge`

The walkway graph is built from entities on the `"Walkways"` layer:

- **Circles** → `WalkwayNode` (entrance if `Color.Index == 3` / green)
- **Lines** → `WalkwayEdge` (weight = Euclidean distance between endpoints)

```csharp
// WalkwayGraph
Dictionary<ulong, WalkwayNode>  Nodes
Dictionary<ulong, WalkwayEdge>  Edges
void BuildFromEntities(IEnumerable<EntityModel> entities)
WalkwayNode? FindNearestNode(double x, double y, double maxDistance)
List<ulong>? FindPathToNearestEntrance(ulong fromNodeHandle)       // Dijkstra
HashSet<ulong>? GetAllHandlesForPath(List<ulong> path)
(List<(double x, double y)> coords, double dist)?
    FindPathCoordinatesToEntrance(double x, double y, double maxDist)
```

---

### `MergeOptions` & `MergeResult`

Used by `MergeDwgCommand` and `IMergeDocumentService`.

```csharp
public class MergeOptions
{
    public LayerConflictStrategy LayerConflictStrategy { get; set; } // default: KeepPrimary
    public double OffsetX { get; set; }
    public double OffsetY { get; set; }
}

public enum LayerConflictStrategy { KeepPrimary, KeepSecondary, RenameSecondary }

public class MergeResult
{
    public List<Entity>        AddedEntities          { get; init; }
    public List<Layer>         AddedLayers            { get; init; }
    public List<BlockRecord>   AddedBlocks            { get; init; }
    public List<(Layer, Color)> UpdatedLayers         { get; init; }
    public int EntitiesSkipped                        { get; init; }
    public int LayerConflictsResolved                 { get; init; }
    public int BlockConflictsResolved                 { get; init; }
}
```

---

## 7. Services

### `IDocumentService` / `DocumentService`

Handles all CAD file I/O.

```csharp
CadDocumentModel CurrentDocument { get; }

// Events
event EventHandler<DocumentLoadedEventArgs> DocumentLoaded;
event EventHandler DocumentClosed;

// Methods
Task<bool> OpenAsync(string filePath, ISet<Type>? excludedTypes, IProgress<int>? progress);
Task<bool> SaveAsync(string? filePath);
Task<IReadOnlyList<(Type EntityType, int Count)>> ScanEntityTypesAsync(string filePath);
void LoadImported(CadDocument document, string displayPath);
void Close();
```

**Save process:**
1. Validates that `CurrentLayerName` in the header exists (resets to `"0"` if deleted).
2. Writes to a temporary file first.
3. Moves temp file to final path only on success to prevent data loss.
4. DWG output uses a DXF round-trip to prevent duplicate ACadSharp handle errors.

---

### `ISelectionService` / `SelectionService`

Single source of truth for selected entities.

```csharp
IReadOnlyCollection<EntityModel> SelectedEntities { get; }

event EventHandler<SelectionChangedEventArgs> SelectionChanged;

void Select(EntityModel entity, bool addToSelection = false);
void SelectMultiple(IEnumerable<EntityModel> entities, bool addToSelection);
void ToggleSelection(EntityModel entity);
void Deselect(EntityModel entity);
void ClearSelection();
```

`SelectionChangedEventArgs` carries `SelectedEntities`, `AddedEntities`, and `RemovedEntities`.

Locked entities are silently skipped during selection. Uses `HashSet<EntityModel>` internally for O(1) lookup.

---

### `IUndoRedoService` / `UndoRedoService`

Maintains two stacks of `IUndoableCommand`.

```csharp
bool   CanUndo           { get; }
bool   CanRedo           { get; }
string? UndoDescription  { get; }
string? RedoDescription  { get; }

event EventHandler StateChanged;

void Execute(IUndoableCommand command);  // Execute and push to undo stack
void Undo();
void Redo();
void Clear();
```

**Stack limits:** Maximum 100 items. When the limit is exceeded, the oldest command is discarded. `Execute()` clears the redo stack.

---

### `IWalkwayService` / `WalkwayService`

Manages the walkway graph and path computation.

```csharp
WalkwayGraph Graph { get; }

void RebuildGraph(IEnumerable<EntityModel> entities);
HashSet<ulong>? GetPathHighlightsForUnit(double unitX, double unitY);
```

`GetPathHighlightsForUnit` computes the search radius as `averageEdgeWeight × 3` (falls back to 10% of bounding box). It finds the nearest node within that radius, then finds the shortest path to any entrance node via `WalkwayGraph.FindPathToNearestEntrance`.

---

### `IBackupService` / `BackupService`

Saves/restores JSON backups to `%AppData%\mPrismaMaps\backups\`.

```csharp
Task SaveBackupAsync(string storeId, string floor, string json);
Task<List<BackupInfo>> ListBackupsAsync();
Task<string> ReadBackupAsync(string filePath);
Task DeleteBackupAsync(string filePath);
```

File name format: `{storeId}_{floor}_{yyyy-MM-dd_HHmmss}.json`

---

### `IDeployService` / `DeployService`

Deploys map data to SQL Server.

```csharp
Task<bool> HasMapAsync(string connectionString, string storeId, string floor);
Task<string?> GetMapAsync(string connectionString, string storeId, string floor);
Task<(bool success, string? errorMessage)> DeployMapAsync(
    string connectionString, string storeId, string floor, string jsonData);
```

Uses the `Maps(StoreId, Floor, MappingData)` table. Wraps writes in a transaction.

---

### `IMergeDocumentService` / `MergeDocumentService`

Merges two CAD documents.

```csharp
CadDocument ReadFile(string filePath);
MergeResult Merge(CadDocument primary, CadDocument secondary, MergeOptions options);
```

**Merge runs in three passes:**

1. **Layer merge** — Conflict resolution per `LayerConflictStrategy`.
2. **Block merge** — Renames conflicting blocks with `"_merged"` suffix. Skips `*Model_Space` / `*Paper_Space`.
3. **Entity copy** — Clones model-space entities with layer/block name remapping. Applies `OffsetX`/`OffsetY`.

---

### `MpolExportService`

Converts the current document state into the MPOL JSON format for the indoor positioning system.

```csharp
MpolMap Export(CadDocumentModel document, string storeName,
               HashSet<string> hiddenLayers, bool flipX, bool flipY, double viewRotation);
```

**Export steps:**

1. Collect MText (unit numbers) and LwPolylines (unit areas, background contours).
2. Apply view transforms (rotation + flips) to all coordinates.
3. Compute shortest paths from each unit to an entrance via `WalkwayGraph`.
4. Scale all geometry to a **300 × 300 grid** with 3 px margin.
5. Calculate area, width, height, and path distance per unit.

---

## 8. Commands (Undo/Redo)

### `IUndoableCommand` Interface

```csharp
public interface IUndoableCommand
{
    string Description { get; }   // Displayed in Undo/Redo menus
    void Execute();
    void Undo();
}
```

All state-changing user actions must be wrapped in an `IUndoableCommand` and executed via `_undoRedoService.Execute(command)`.

### Command Reference

| Class | Description |
|-------|-------------|
| `AddEntityCommand` | Adds a single entity to model space |
| `DeleteEntitiesCommand` | Deletes entities (stores owner `BlockRecord` for undo) |
| `MoveEntitiesCommand` | Translates entities by (dx, dy) |
| `ChangeEntityColorCommand` | Changes entity color (stores originals) |
| `ChangeEntityLayerCommand` | Moves entities to a different layer |
| `PasteEntitiesCommand` | Pastes cloned entities from clipboard |
| `DeleteLayerCommand` | Deletes a layer from `Layers` collection |
| `DeleteEntitiesByTypeCommand` | Deletes all entities of specified type(s) |
| `DeleteHiddenEntitiesCommand` | Deletes entities on hidden/invisible layers |
| `DeleteEntitiesOutsideViewportCommand` | Deletes entities outside current viewport |
| `GenerateUnitAreasCommand` | Generates LwPolylines on "Unit Areas" layer via flood fill |
| `GenerateBackgroundContoursCommand` | Generates contour polylines on "Background Contours" layer |
| `EditUnitNumberCommand` | Edits MText value on "Unit Numbers" layer |
| `ResizeUnitNumbersCommand` | Changes MText height for all unit numbers |
| `ToggleEntranceCommand` | Toggles entrance flag (color) of a walkway node circle |
| `AddWalkwaySegmentCommand` | Adds a Line to the walkway graph |
| `AdjustWalkwayEdgesCommand` | Updates walkway edge endpoints after a node is moved |
| `TransformEntitiesCommand` | Scales and/or rotates selected entities from a pivot |
| `ScaleMapCommand` | Uniformly scales all entities around the origin |
| `MergeDwgCommand` | Merges a secondary DWG file into the current document |

### Writing a New Command

```csharp
public class MyCommand : IUndoableCommand
{
    private readonly CadDocumentModel _document;
    // Store enough state for both Execute and Undo
    private readonly List<(Entity entity, SomeType originalValue)> _originals = new();

    public string Description => "My operation";

    public MyCommand(CadDocumentModel document, /* params */)
    {
        _document = document;
    }

    public void Execute()
    {
        _originals.Clear(); // safe to call Execute multiple times
        foreach (var entity in /* targets */)
        {
            _originals.Add((entity, entity.SomeProperty));
            entity.SomeProperty = newValue;
        }
        _document.IsDirty = true;
    }

    public void Undo()
    {
        foreach (var (entity, original) in _originals)
            entity.SomeProperty = original;
        _document.IsDirty = true;
    }
}
```

---

## 9. ViewModels

### `MainWindowViewModel`

The primary orchestrator. Registered as a singleton in DI.

**Observable properties (selection):**

```csharp
int    EntityCount
int    SelectedCount
string WindowTitle
string StatusText
string DrawingStatusText
double MouseX, MouseY
double ZoomLevel
bool   IsLoading
int    LoadProgress
```

**View state:**

```csharp
DrawingMode          DrawingMode
bool                 FlipX, FlipY
double               ViewRotation
GridSnapSettings     GridSettings
bool                 IsSnapEnabled
double               GridSpacing
HashSet<ulong>?      HighlightedPathHandles
```

**Unit number placement:**

```csharp
string UnitNumberPrefix    // e.g. "A"
int    UnitNextNumber      // Auto-increments after placement
double UnitTextHeight      // Auto-scaled from document extents
```

**Child ViewModels:**

```csharp
LayerPanelViewModel      LayerPanel
PropertiesPanelViewModel PropertiesPanel
EntityViewerViewModel    EntityViewer
```

**Key events (subscribed by `MainWindow.xaml.cs`):**

```csharp
event EventHandler     ZoomToFitRequested
event EventHandler     RenderRequested
event EventHandler     EntitiesChanged
event EventHandler<SelectEntityTypesEventArgs>  SelectEntityTypesRequested
event EventHandler<RotateViewEventArgs>         RotateViewRequested
event EventHandler<ScaleMapRequestedEventArgs>  ScaleMapRequested
event EventHandler<ExportMpolRequestedEventArgs> ExportMpolRequested
event EventHandler<DeployMpolRequestedEventArgs> DeployMpolRequested
event EventHandler<RestoreBackupRequestedEventArgs> RestoreBackupRequested
event EventHandler<EditUnitNumberRequestedEventArgs> EditUnitNumberRequested
event Action<double>   ResizeUnitNumbersRequested
event EventHandler<ZoomToAreaEventArgs>         ZoomToAreaRequested
event EventHandler<ZoomToEntityEventArgs>       ZoomToEntityRequested
event EventHandler<DeleteOutsideViewportEventArgs> DeleteOutsideViewportRequested
event EventHandler<ResetViewTransformsEventArgs> ResetViewTransformsRequested
event EventHandler     CenterOnOriginRequested
```

**Public methods called by MainWindow:**

```csharp
void OnDrawingCompleted(DrawingCompletedEventArgs e)
void OnMoveCompleted(MoveCompletedEventArgs e)
void OnTransformCompleted(TransformCompletedEventArgs e)
void HandleToggleEntrance(ulong handle)
void EditUnitNumber(MText mtext)
void ApplyResizeUnitNumbers(double newHeight)
void RefreshEntities()
void ZoomToEntity(EntityModel entity)
EntityModel? GetEntityModel(Entity entity)
IEnumerable<WalkwayNode> GetWalkwayNodes()
double GetWalkwaySnapDistance()
double ComputeWalkwayNodeRadius()
```

**`CanExecuteChanged` notification sites:**

Whenever `RefreshEntities()`, `OnDocumentLoaded`, or `OnDocumentClosed` runs, it calls `NotifyCanExecuteChanged()` on every command that depends on document state. When adding a new command with a `CanExecute`, add it to those three sites.

---

### `LayerPanelViewModel`

Manages the layer list panel.

```csharp
ObservableCollection<LayerModel>  Layers
ObservableCollection<LayerModel>  SelectedLayers
LayerModel?                        SelectedLayer

// Events
event EventHandler LayerVisibilityChanged
event EventHandler LayerLockChanged
event EventHandler<DeleteLayerRequestedEventArgs> DeleteLayerRequested
event EventHandler<DeleteMultipleLayersRequestedEventArgs> DeleteMultipleLayersRequested
event EventHandler LayersChanged

// Commands
ShowAllLayersCommand
HideAllLayersCommand
IsolateSelectedLayerCommand
ToggleSelectedLayersVisibilityCommand
DeleteEmptyLayersCommand
DeleteSelectedLayersCommand
```

---

### `PropertiesPanelViewModel`

Displays and edits properties of the selected entity/entities.

```csharp
ObservableCollection<PropertyItem>  Properties         // Key/value rows
ObservableCollection<string>         AvailableLayers
ObservableCollection<ColorItem>      AvailableColors    // Standard ACI palette

string  SelectionSummary    // e.g. "3 entities selected"
bool    HasSelection
string? SelectedLayer
ColorItem? SelectedColorItem

// Commands
ApplyLayerChangeCommand   // → ChangeEntityLayerCommand
ApplyColorChangeCommand   // → ChangeEntityColorCommand

event EventHandler PropertiesUpdated
```

---

### `EntityViewerViewModel`

Groups entities for the tree-view panel.

```csharp
ObservableCollection<EntityGroupModel> Groups
bool GroupByType   // Toggle between group-by-type and group-by-layer

void SetEntities(ObservableCollection<EntityModel> entities)
void Refresh()
bool SuppressRefresh   // Batch update flag
```

---

## 10. Views & Dialogs

All dialogs follow the same pattern:

1. Named `XxxDialog.xaml` / `XxxDialog.xaml.cs`
2. Dark-themed `Window` with `ResizeMode="NoResize"` and `WindowStartupLocation="CenterOwner"`
3. Code-behind sets `DialogResult = true` on OK, `false` on Cancel
4. Public properties expose the result values

| Dialog | Purpose | Key Output Properties |
|--------|---------|----------------------|
| `EditUnitNumberDialog` | Edit a single unit number text | `UnitNumberValue` |
| `ResizeUnitNumbersDialog` | Set new height for all unit numbers | `NewHeight` |
| `DeleteLayerDialog` | Choose delete/move option for layer | `DeleteOption`, `TargetLayer` |
| `DeleteMultipleLayersDialog` | Batch layer deletion | `DeleteOption`, `TargetLayer` |
| `RotateViewDialog` | Set view rotation angle | `Angle` |
| `SelectEntityTypesDialog` | Filter entity types on open | `ExcludedTypes` |
| `ExportMpolDialog` | Enter store name for export | `StoreName` |
| `DeployMpolDialog` | SQL Server connection + store info | `StoreName`, `StoreId`, `Floor`, `Server`, `Username`, `Password` |
| `RestoreBackupDialog` | Pick a backup to restore | `SelectedBackup`, `Server`, `Username`, `Password` |
| `ScaleMapDialog` | Enter a scale factor | `ScaleFactor` |
| `MergeOptionsDialog` | Set merge strategy and offset | `Options` (MergeOptions) |

**Adding a new dialog — checklist:**

1. Create `Views/MyDialog.xaml` (copy `EditUnitNumberDialog.xaml` as template).
2. Create `Views/MyDialog.xaml.cs` with `public SomeType Result { get; private set; }`.
3. Add an event `MyDialogRequested` to `MainWindowViewModel`.
4. Fire the event with relevant args from a `[RelayCommand]`.
5. Subscribe in `MainWindow.xaml.cs` constructor, show dialog, call `ViewModel.ApplyMyResult(...)`.

---

## 11. CadCanvas Control

`CadCanvas` is a custom `FrameworkElement` (no templates, no styles). It renders entities using **SkiaSharp** into a `WriteableBitmap` and presents it to WPF.

### Rendering Architecture

```
┌──────────────────────────────────────────────────┐
│  Entity cache (WriteableBitmap)                  │
│    - Full Skia render of all visible entities    │
│    - Invalidated on: entities, scale, offset,    │
│      hidden layers, view transforms change       │
├──────────────────────────────────────────────────┤
│  Overlay cache (WriteableBitmap)                 │
│    - Selection highlights (cyan outline)         │
│    - Path highlights (orange fill)               │
│    - Drawing tool preview (dashed cyan)          │
│    - Transform handles (circles/rectangles)      │
│    - Invalidated on: selected handles,           │
│      highlighted path handles change             │
└──────────────────────────────────────────────────┘
         ↓ composited in OnRender()
```

### Key Public API

**Dependency properties (set by MainWindow):**

```csharp
IEnumerable<Entity>?   Entities
IEnumerable<ulong>?    SelectedHandles
IEnumerable<string>?   HiddenLayers
IEnumerable<ulong>?    LockedHandles
IEnumerable<string>?   LockedLayers
bool                   IsPanMode
Extents?               Extents
DrawingMode            DrawingMode
GridSnapSettings?      GridSettings
bool                   FlipX, FlipY
double                 ViewRotation
HashSet<ulong>?        HighlightedPathHandles
```

**Properties:**

```csharp
double Scale     { get; set; }
Point  Offset    { get; set; }
Rect   ViewportBounds   // Current visible CAD-space bounds
```

**Methods:**

```csharp
void Render()                          // Trigger a repaint
void InvalidateCache()                 // Force full re-render
void RebuildSpatialIndex()             // Rebuild SpatialGrid after entity changes
void ZoomToFit()
void ZoomToRect(double minX, minY, maxX, maxY)
void ZoomToEntity(Entity entity)
void CenterOnOrigin()
void ResetViewTransforms()
void ConfigureUnitNumberTool(string prefix, int nextNumber, string format, double height)
void ConfigureFairwayTool(IEnumerable<WalkwayNode> nodes, double snapDistance, double nodeRadius)
Rect GetViewportBounds()
```

**Events:**

```csharp
event EventHandler<CadMouseEventArgs>            CadMouseMove
event EventHandler<CadEntityClickEventArgs>      EntityClicked
event EventHandler<CadEntityClickEventArgs>      EntityDoubleClicked
event EventHandler<DrawingCompletedEventArgs>    DrawingCompleted
event EventHandler<MarqueeSelectionEventArgs>    MarqueeSelectionCompleted
event EventHandler<MoveCompletedEventArgs>       MoveCompleted
event EventHandler<TransformCompletedEventArgs>  TransformCompleted
event EventHandler<ToggleEntranceEventArgs>      ToggleEntranceRequested
```

### Hit Testing

1. `SpatialGrid.Query(point, tolerance)` returns a small set of candidate entities.
2. `HitTestHelper.HitTest(entity, cadPoint, tolerance)` does per-entity geometric test.
3. Default tolerance: **5 screen pixels ÷ current scale** (so tolerance shrinks as you zoom in).

### Marquee Selection

Dragging from empty space starts marquee mode. On release, all entities whose bounding boxes (`BoundingBoxHelper.GetBounds`) intersect the rectangle are selected.

### Transform Mode

When `DrawingMode == Transform` and entities are selected:
- **8 handles** (4 corners + 4 midpoints) are drawn as small circles.
- Dragging a corner handle **scales** relative to the opposite corner.
- Dragging a corner handle while holding **Ctrl** **rotates** around the selection centre.
- Dragging a midpoint handle scales on one axis only.
- Result fires `TransformCompleted` → `TransformEntitiesCommand`.

---

## 12. Drawing Tools

All tools implement `IDrawingTool`:

```csharp
public interface IDrawingTool
{
    string     Name          { get; }
    DrawingMode Mode         { get; }
    bool       IsDrawing     { get; }
    string     StatusText    { get; }

    void OnMouseDown(Point cadPoint, MouseButton button);
    void OnMouseMove(Point cadPoint);
    void OnMouseUp(Point cadPoint, MouseButton button);
    void OnKeyDown(Key key);

    IReadOnlyList<Point>? GetPreviewPoints();
    bool                  IsPreviewClosed { get; }

    event EventHandler<DrawingCompletedEventArgs>? Completed;
    event EventHandler?                            Cancelled;

    void Reset();
}
```

`CadCanvas` instantiates the correct tool via `UpdateDrawingTool()` when `DrawingMode` changes.

### Tool Summary

| Class | Mode | Gesture | Result |
|-------|------|---------|--------|
| `LineTool` | DrawLine | Click start → Click end | `Line` entity |
| `PolylineTool` | DrawPolyline | Click points → Enter/double-click | `LwPolyline` (open) |
| `PolygonTool` | DrawPolygon | Click points → Enter/double-click | `LwPolyline` (closed) |
| `FairwayTool` | DrawFairway | Click nodes | `Circle` + `Line` on Walkways layer |
| `UnitNumberTool` | PlaceUnitNumber | Click to place | `MText` on Unit Numbers layer |
| `ZoomAreaTool` | ZoomToArea | Drag rectangle | Fires `ZoomToAreaRequested` |

**Common gestures:**

| Key | Action |
|-----|--------|
| Right-click | Cancel (LineTool), or complete (Polyline/Polygon with ≥ 2/3 pts) |
| Enter | Complete drawing |
| Escape | Cancel and reset |
| Backspace | Remove last point (Polyline/Polygon) |
| Shift (held) | Snap to nearest 45° angle |

---

## 13. Rendering Pipeline

### Two-Pass Rendering

```
Pass 1 — Background:   "Unit Areas" and "Background Contours" layers
Pass 2 — Foreground:   All other layers
```

This ensures generated polygons appear behind the floor plan geometry.

### Per-Entity Rendering

`RenderService.RenderEntities()` dispatches to an `IEntityRenderer` implementation:

```csharp
public interface IEntityRenderer
{
    bool CanRender(Entity entity);
    void Render(SKCanvas canvas, Entity entity, RenderContext renderContext);
}
```

| Renderer | Handles |
|----------|---------|
| `LineRenderer` | `Line` |
| `CircleRenderer` | `Circle` (not Arc) |
| `ArcRenderer` | `Arc` |
| `PolylineRenderer` | `LwPolyline`, `Polyline2D` |
| `TextRenderer` | `TextEntity`, `MText` |
| `EllipseRenderer` | `Ellipse` |
| `PointRenderer` | `Point` (dot entity) |
| `InsertRenderer` | `Insert` (block reference) |

Unknown entity types are silently skipped with a debug log entry.

### `RenderContext`

Passed into every renderer. Provides coordinate transforms, visibility checks, and paint caching.

```csharp
double Scale
Point  Offset
Color  DefaultColor
double LineThickness
HashSet<ulong>  SelectedHandles
HashSet<string> HiddenLayers
Rect?           ViewportBounds     // null = render everything

Point Transform(double cadX, double cadY)   // CAD → screen
double TransformDistance(double distance)
bool IsLayerVisible(Entity entity)
bool IsSelected(Entity entity)
bool IsInViewport(Rect bounds)
```

### Skia Paint Cache (`SkiaRenderCache`)

`SKPaint` objects are expensive to create. `SkiaRenderCache` maintains a `ConcurrentDictionary` of stroke and fill paints keyed by `(SKColor, thickness)`:

```csharp
SKPaint GetStrokePaint(SKColor color, float thickness)
SKPaint GetFillPaint(SKColor color)
```

### Selection Highlight

Selected entities are re-drawn in **cyan** (`#00FFFF`) at **2× line thickness** on top of the entity cache, in the overlay pass.

### Path Highlight

Entities whose handles appear in `HighlightedPathHandles` are drawn with a semi-transparent **orange** fill/stroke on the overlay layer. This is used for both walkway path highlights and unit area highlights.

---

## 14. Helpers & Utilities

### `BoundingBoxHelper`

Computes and caches bounding boxes per entity handle.

```csharp
static Rect? GetBounds(Entity entity)          // Cached; uses handle as key
static void  InvalidateCache()                 // Called on document load/close
static void  InvalidateEntity(ulong handle)    // Called after entity transform
```

Cache must be invalidated whenever an entity's geometry changes (moves, scales, rotates). `EntityTransformHelper` calls `InvalidateEntity` after each transform.

---

### `EntityTransformHelper`

Applies geometric transforms to ACadSharp entity types.

```csharp
static void TranslateEntity(Entity entity, double dx, double dy)
static void RotateEntity(Entity entity, double pivotX, double pivotY, double angleRadians)
static void ScaleEntity(Entity entity, double pivotX, double pivotY, double scaleX, double scaleY)
static Entity? CloneEntity(Entity entity)     // Deep clone; null for unsupported types
```

**ACadSharp gotchas handled here:**

- `LwPolyline.Vertex` is a **struct** — the whole vertex must be replaced (no in-place mutation).
- `TextEntity.Rotation` is in **degrees**; `RotateEntity` converts from radians before assigning.
- `Insert.Rotation` is also in degrees.
- `Arc` inherits from `Circle`; check `Arc` first in type switches.
- `Insert` requires a `BlockRecord` in its constructor; `Block` property is read-only.

---

### `HitTestHelper`

Geometric hit testing in CAD space.

```csharp
static bool HitTest(Entity entity, Point point, double tolerance = 5.0)
static bool IsPointInPolygon(LwPolyline polygon, double x, double y)  // Ray casting
```

**Per-entity logic:**

| Entity | Test |
|--------|------|
| Line | Distance from point to segment ≤ tolerance |
| Circle | `|dist(center, point) - radius|` ≤ tolerance |
| Arc | On circle, and angle within `[startAngle, endAngle]` (degrees→radians conversion) |
| LwPolyline | Distance to any segment ≤ tolerance |
| TextEntity / MText | Inside approximate bounding box |
| Insert | Distance to insert point ≤ tolerance × 5 |

---

### `SpatialGrid`

A fixed 50×50 cell grid for fast spatial queries. Used by `CadCanvas` for hit testing.

```csharp
static SpatialGrid Build(IEnumerable<Entity> entities, Rect extents)
void Insert(Entity entity)
void Remove(Entity entity)
List<Entity> Query(Point point, double tolerance)
```

Cell assignment uses `Math.Floor` / `Math.Ceiling` (not truncation) to avoid boundary misses.

---

### `SnapHelper`

```csharp
static Point SnapToAngle(Point anchor, Point target)    // Nearest 45°
static Point SnapToGrid(Point point, GridSnapSettings settings)
```

Angle snapping is applied when the user holds **Shift** during drawing.

---

### `ColorHelper`

```csharp
static WpfColor GetEntityColor(Entity entity, WpfColor defaultColor)
static SKColor  ToSKColor(this WpfColor color)
```

Color resolution order: entity color → by-layer → by-block → ACI index → defaultColor.

---

### `FloodFillGrid`

Used by `GenerateUnitAreasCommand` to find enclosed regions around unit number insertion points.

---

## 15. Special Layers

Five layer names are reserved for generated content. They are defined as constants on `CadDocumentModel` and must not be used for user geometry.

| Layer | Content | How created |
|-------|---------|-------------|
| `"User Drawings"` | User-drawn entities (Lines, Polylines, etc.) | `AddEntityCommand` |
| `"Unit Numbers"` | MText labels with unit identifiers | `UnitNumberTool` / placement |
| `"Walkways"` | Lines (edges) and Circles (nodes) | `FairwayTool` / `AddWalkwaySegmentCommand` |
| `"Unit Areas"` | Closed LwPolylines around each unit | `GenerateUnitAreasCommand` |
| `"Background Contours"` | Polygon outlines of background geometry | `GenerateBackgroundContoursCommand` |

---

## 16. Key Patterns & Conventions

### MVVM

- ViewModels extend `ObservableObject` from CommunityToolkit.Mvvm.
- Observable properties use `[ObservableProperty]` source generator where possible.
- Commands use `[RelayCommand(CanExecute = nameof(CanXxx))]`.
- The View never directly accesses Services — it goes through the ViewModel.

### Event Pattern for Dialog Coordination

When a ViewModel needs user input from a dialog, it fires an event rather than opening the dialog itself:

```csharp
// ViewModel fires:
public event Action<double>? ResizeUnitNumbersRequested;

[RelayCommand(CanExecute = nameof(CanResizeUnitNumbers))]
private void ResizeUnitNumbers()
{
    ResizeUnitNumbersRequested?.Invoke(suggestedHeight);
}

// MainWindow.xaml.cs handles:
_viewModel.ResizeUnitNumbersRequested += OnResizeUnitNumbersRequested;

private void OnResizeUnitNumbersRequested(double suggestedHeight)
{
    var dialog = new ResizeUnitNumbersDialog(suggestedHeight) { Owner = this };
    if (dialog.ShowDialog() == true)
        _viewModel.ApplyResizeUnitNumbers(dialog.NewHeight);
}
```

### CanExecute Notification

Commands whose `CanExecute` depends on document state must have `NotifyCanExecuteChanged()` called in:

1. `OnLayerVisibilityChangedForCommand`
2. `OnDocumentLoaded` (inside the method that loads entities)
3. `OnDocumentClosed`
4. `RefreshEntities()`
5. Any other location that changes the relevant precondition

### Entity Modification via Commands

Never mutate entity properties directly in a ViewModel or View. Always:

```csharp
var command = new SomeCommand(document, entities, newValue);
_undoRedoService.Execute(command);
RefreshEntities();  // if entity collection changes
```

### Dirty Flag

Always set `_document.IsDirty = true` in both `Execute()` and `Undo()` of any command that modifies entity or layer state.

---

## 17. Data Flow Walkthrough

### Opening a File

```
User: File → Open
  MainWindow calls → _viewModel.OpenFileCommand.Execute()
    DocumentService.OpenAsync(filePath)
      ACadSharp reads DWG/DXF
      CadDocumentModel.Load(document, filePath)
    DocumentService fires DocumentLoaded
      MainWindowViewModel.OnDocumentLoaded()
        Builds EntityModel list → _entityLookup
        LayerPanel.RefreshLayers()
        WalkwayService.RebuildGraph()
        NotifyCanExecuteChanged() on all document-dependent commands
        Fires ZoomToFitRequested, RenderRequested
          MainWindow.OnZoomToFitRequested → CadCanvas.ZoomToFit()
          MainWindow.OnRenderRequested → CadCanvas.Render()
```

### Placing a Unit Number

```
User: selects PlaceUnitNumber mode, clicks on canvas
  CadCanvas.UnitNumberTool.OnMouseDown()
    Tool fires Completed
  CadCanvas fires DrawingCompleted
  MainWindow.OnDrawingCompleted(e)
    _viewModel.OnDrawingCompleted(e)
      HandlePlaceUnitNumber(e)
        Gets/creates "Unit Numbers" layer
        Creates MText entity
        _undoRedoService.Execute(new AddEntityCommand(...))
        RefreshEntities()
        UnitNextNumber++
        GenerateUnitAreasCommand.NotifyCanExecuteChanged()
        ResizeUnitNumbersCommand.NotifyCanExecuteChanged()
```

### Selecting an Entity

```
User: clicks on canvas
  CadCanvas.OnMouseLeftButtonDown
    SpatialGrid.Query(point, tolerance) → candidates
    HitTestHelper.HitTest() per candidate → hit entity
  CadCanvas fires EntityClicked
  MainWindow.OnEntityClicked(e)
    SelectionService.Select(entityModel)
      entityModel.IsSelected = true
      SelectionChanged fired
    MainWindowViewModel.OnSelectionChanged(e)
      UpdatePathHighlightsForSelection()
        Checks for unit area polygon containing MText insert point
        Merges unit area handle + walkway path handles
        HighlightedPathHandles = combined
    MainWindow.OnViewModelPropertyChanged (HighlightedPathHandles changed)
      CadCanvas.HighlightedPathHandles = ...
      CadCanvas.Render() → redraws overlay
```

### Undo

```
User: Ctrl+Z
  UndoCommand.Execute() → _undoRedoService.Undo()
    command = _undoStack.Pop()
    command.Undo()            // Mutates entity/document state
    _redoStack.Push(command)
    StateChanged fired
  MainWindow.OnUndoRedoStateChanged()
    _viewModel.RefreshEntities()
    LayerPanel.RefreshLayers()
    CadCanvas.RebuildSpatialIndex()
```

---

## 18. Testing

Tests use **xUnit** + **FluentAssertions** + **Moq**.

```
mPrismaMapsWPF.Tests/
├── Commands/     # IUndoableCommand Execute/Undo correctness
├── Drawing/      # Tool state transitions, completion/cancellation
├── Helpers/      # BoundingBoxHelper, HitTestHelper, SpatialGrid, SnapHelper
├── Models/       # WalkwayGraph path finding, EntityModel
├── Services/     # UndoRedoService stack behaviour, SelectionService
└── ViewModels/   # MainWindowViewModel integration tests
```

**`EntityFactory`** provides test entity creation helpers (e.g. `EntityFactory.CreateLine(0, 0, 10, 10)`).

### Running Tests

```bash
# All tests
dotnet test

# Single test method
dotnet test --filter "FullyQualifiedName~TestClass.TestMethod"

# By category folder
dotnet test --filter "FullyQualifiedName~Commands"
```

### Adding a Test

1. Mirror the source tree: `Commands/ResizeUnitNumbersCommandTests.cs`
2. Use `EntityFactory` for entities, `Mock<IUndoRedoService>` for service stubs.
3. Assert state with FluentAssertions: `result.Should().Be(expected)`.
