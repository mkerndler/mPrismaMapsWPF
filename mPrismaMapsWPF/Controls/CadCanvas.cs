using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ACadSharp.Entities;
using mPrismaMapsWPF.Drawing;
using mPrismaMapsWPF.Helpers;
using mPrismaMapsWPF.Models;
using mPrismaMapsWPF.Rendering;
using WpfPoint = System.Windows.Point;

namespace mPrismaMapsWPF.Controls;

public class CadCanvas : FrameworkElement
{
    private readonly RenderService _renderService;
    private readonly DrawingVisual _drawingVisual;
    private readonly VisualCollection _visuals;

    private WpfPoint _panStart;
    private bool _isPanning;
    private WpfPoint _offset;
    private double _scale = 1.0;
    private Extents? _extents;

    // View transforms
    private bool _flipX;
    private bool _flipY;
    private double _viewRotation; // degrees

    // Bitmap caching for performance
    private RenderTargetBitmap? _cachedBitmap;
    private double _cachedScale;
    private WpfPoint _cachedOffset;
    private int _cachedWidth;
    private int _cachedHeight;
    private bool _cacheValid;
    private HashSet<ulong>? _cachedSelectedHandles;
    private HashSet<string>? _cachedHiddenLayers;

    // Spatial index for hit testing
    private SpatialGrid? _spatialGrid;

    // Handle -> Entity lookup for fast selection overlay rendering
    private Dictionary<ulong, Entity>? _entityByHandle;

    // Cached selected handles HashSet (rebuilt only when SelectedHandles property changes)
    private HashSet<ulong> _selectedHandlesSet = new();

    // Cached viewport bounds per frame
    private Rect? _frameViewportBounds;

    // Drawing support
    private IDrawingTool? _currentTool;
    private WpfPoint _currentCadPoint;

    // Grid rendering
    private static readonly Pen GridPenMinor = CreateGridPen(Color.FromArgb(40, 128, 128, 128));
    private static readonly Pen GridPenMajor = CreateGridPen(Color.FromArgb(80, 128, 128, 128));
    private static readonly Pen PreviewPen = CreatePreviewPen();

    private static Pen CreateGridPen(Color color)
    {
        var pen = new Pen(new SolidColorBrush(color), 1);
        pen.Freeze();
        return pen;
    }

    private static Pen CreatePreviewPen()
    {
        var pen = new Pen(Brushes.Cyan, 1.5) { DashStyle = DashStyles.Dash };
        pen.Freeze();
        return pen;
    }

    public CadCanvas()
    {
        _renderService = new RenderService();
        _drawingVisual = new DrawingVisual();
        _visuals = new VisualCollection(this) { _drawingVisual };

        ClipToBounds = true;
        Focusable = true;

        MouseWheel += OnMouseWheel;
        MouseLeftButtonDown += OnMouseLeftButtonDown;
        MouseLeftButtonUp += OnMouseLeftButtonUp;
        MouseRightButtonDown += OnMouseRightButtonDown;
        MouseRightButtonUp += OnMouseRightButtonUp;
        MouseMove += OnMouseMove;
        SizeChanged += OnSizeChanged;
        KeyDown += OnKeyDown;
    }

    public static readonly DependencyProperty EntitiesProperty =
        DependencyProperty.Register(
            nameof(Entities),
            typeof(IEnumerable<Entity>),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnEntitiesChanged));

    public IEnumerable<Entity>? Entities
    {
        get => (IEnumerable<Entity>?)GetValue(EntitiesProperty);
        set => SetValue(EntitiesProperty, value);
    }

    public static readonly DependencyProperty SelectedHandlesProperty =
        DependencyProperty.Register(
            nameof(SelectedHandles),
            typeof(IEnumerable<ulong>),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnSelectedHandlesChanged));

    public IEnumerable<ulong>? SelectedHandles
    {
        get => (IEnumerable<ulong>?)GetValue(SelectedHandlesProperty);
        set => SetValue(SelectedHandlesProperty, value);
    }

    public static readonly DependencyProperty HiddenLayersProperty =
        DependencyProperty.Register(
            nameof(HiddenLayers),
            typeof(IEnumerable<string>),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnHiddenLayersChanged));

    public IEnumerable<string>? HiddenLayers
    {
        get => (IEnumerable<string>?)GetValue(HiddenLayersProperty);
        set => SetValue(HiddenLayersProperty, value);
    }

    public static readonly DependencyProperty IsPanModeProperty =
        DependencyProperty.Register(
            nameof(IsPanMode),
            typeof(bool),
            typeof(CadCanvas),
            new PropertyMetadata(false));

    public bool IsPanMode
    {
        get => (bool)GetValue(IsPanModeProperty);
        set => SetValue(IsPanModeProperty, value);
    }

    public static readonly DependencyProperty ExtentsProperty =
        DependencyProperty.Register(
            nameof(Extents),
            typeof(Extents),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnExtentsChanged));

    public Extents? Extents
    {
        get => (Extents?)GetValue(ExtentsProperty);
        set => SetValue(ExtentsProperty, value);
    }

    public static readonly DependencyProperty DrawingModeProperty =
        DependencyProperty.Register(
            nameof(DrawingMode),
            typeof(DrawingMode),
            typeof(CadCanvas),
            new PropertyMetadata(DrawingMode.Select, OnDrawingModeChanged));

    public DrawingMode DrawingMode
    {
        get => (DrawingMode)GetValue(DrawingModeProperty);
        set => SetValue(DrawingModeProperty, value);
    }

    public static readonly DependencyProperty GridSettingsProperty =
        DependencyProperty.Register(
            nameof(GridSettings),
            typeof(GridSnapSettings),
            typeof(CadCanvas),
            new PropertyMetadata(null, OnGridSettingsChanged));

    public GridSnapSettings? GridSettings
    {
        get => (GridSnapSettings?)GetValue(GridSettingsProperty);
        set => SetValue(GridSettingsProperty, value);
    }

    public static readonly DependencyProperty FlipXProperty =
        DependencyProperty.Register(
            nameof(FlipX),
            typeof(bool),
            typeof(CadCanvas),
            new PropertyMetadata(false, OnViewTransformChanged));

    public bool FlipX
    {
        get => (bool)GetValue(FlipXProperty);
        set => SetValue(FlipXProperty, value);
    }

    public static readonly DependencyProperty FlipYProperty =
        DependencyProperty.Register(
            nameof(FlipY),
            typeof(bool),
            typeof(CadCanvas),
            new PropertyMetadata(false, OnViewTransformChanged));

    public bool FlipY
    {
        get => (bool)GetValue(FlipYProperty);
        set => SetValue(FlipYProperty, value);
    }

    public static readonly DependencyProperty ViewRotationProperty =
        DependencyProperty.Register(
            nameof(ViewRotation),
            typeof(double),
            typeof(CadCanvas),
            new PropertyMetadata(0.0, OnViewTransformChanged));

    public double ViewRotation
    {
        get => (double)GetValue(ViewRotationProperty);
        set => SetValue(ViewRotationProperty, value);
    }

    public event EventHandler<CadMouseEventArgs>? CadMouseMove;
    public event EventHandler<CadEntityClickEventArgs>? EntityClicked;
    public event EventHandler<DrawingCompletedEventArgs>? DrawingCompleted;

    public double Scale
    {
        get => _scale;
        set
        {
            _scale = Math.Max(0.001, Math.Min(1000, value));
            InvalidateCache(); // Scale changed
            Render();
        }
    }

    public void ZoomToFit()
    {
        var extents = Extents ?? _extents;
        if (extents == null || !extents.IsValid || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        double margin = 50;
        double availableWidth = ActualWidth - margin * 2;
        double availableHeight = ActualHeight - margin * 2;

        double scaleX = availableWidth / extents.Width;
        double scaleY = availableHeight / extents.Height;
        _scale = Math.Min(scaleX, scaleY);

        _offset = new WpfPoint(
            -extents.CenterX + (ActualWidth / 2) / _scale,
            extents.CenterY + (ActualHeight / 2) / _scale
        );

        InvalidateCache(); // View changed completely
        Render();
    }

    /// <summary>
    /// Centers the view on the origin (0,0).
    /// </summary>
    public void CenterOnOrigin()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        _offset = new WpfPoint(
            (ActualWidth / 2) / _scale,
            (ActualHeight / 2) / _scale
        );

        InvalidateCache();
        Render();
    }

    /// <summary>
    /// Resets all view transforms (flip, rotation) to default.
    /// </summary>
    public void ResetViewTransforms()
    {
        FlipX = false;
        FlipY = false;
        ViewRotation = 0;
    }

    public void ZoomIn()
    {
        WpfPoint center = new(ActualWidth / 2, ActualHeight / 2);
        ZoomAtPoint(center, 1.25);
    }

    public void ZoomOut()
    {
        WpfPoint center = new(ActualWidth / 2, ActualHeight / 2);
        ZoomAtPoint(center, 1.0 / 1.25);
    }

    /// <summary>
    /// Zooms to fit the specified entity in the view.
    /// </summary>
    public void ZoomToEntity(Entity entity)
    {
        if (entity == null || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        var entityExtents = new Extents();
        entityExtents.Expand(entity);

        if (!entityExtents.IsValid)
            return;

        ZoomToRect(entityExtents.MinX, entityExtents.MinY, entityExtents.MaxX, entityExtents.MaxY);
    }

    /// <summary>
    /// Zooms to fit the specified rectangle (in CAD coordinates) in the view.
    /// </summary>
    public void ZoomToRect(double minX, double minY, double maxX, double maxY)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
            return;

        double width = maxX - minX;
        double height = maxY - minY;
        double centerX = (minX + maxX) / 2;
        double centerY = (minY + maxY) / 2;

        // Ensure minimum size for points or very small entities
        if (width < 1) width = 100;
        if (height < 1) height = 100;

        double margin = 50;
        double availableWidth = ActualWidth - margin * 2;
        double availableHeight = ActualHeight - margin * 2;

        double scaleX = availableWidth / width;
        double scaleY = availableHeight / height;
        _scale = Math.Min(scaleX, scaleY);

        // Limit max zoom to prevent excessive zooming on small entities
        _scale = Math.Min(_scale, 50);

        _offset = new WpfPoint(
            -centerX + (ActualWidth / 2) / _scale,
            centerY + (ActualHeight / 2) / _scale
        );

        InvalidateCache();
        Render();
    }

    public WpfPoint ScreenToCad(WpfPoint screenPoint)
    {
        return new WpfPoint(
            screenPoint.X / _scale - _offset.X,
            -(screenPoint.Y / _scale - _offset.Y)
        );
    }

    public void Render()
    {
        RenderWithCache(forceFullRender: false);
    }

    /// <summary>
    /// Sets the current drawing tool based on the drawing mode.
    /// </summary>
    public void SetDrawingTool(IDrawingTool? tool)
    {
        if (_currentTool != null)
        {
            _currentTool.Completed -= OnDrawingToolCompleted;
            _currentTool.Cancelled -= OnDrawingToolCancelled;
            _currentTool.Reset();
        }

        _currentTool = tool;

        if (_currentTool != null)
        {
            _currentTool.Completed += OnDrawingToolCompleted;
            _currentTool.Cancelled += OnDrawingToolCancelled;
        }

        UpdateCursor();
        Render();
    }

    /// <summary>
    /// Gets the snapped CAD point based on current grid settings.
    /// </summary>
    public WpfPoint GetSnappedCadPoint(WpfPoint screenPoint)
    {
        var cadPoint = ScreenToCad(screenPoint);

        if (GridSettings != null && GridSettings.IsEnabled)
        {
            var snapped = SnapHelper.SnapToGrid(cadPoint, GridSettings);
            return snapped;
        }

        return cadPoint;
    }

    /// <summary>
    /// Forces a full re-render, invalidating any cached bitmap.
    /// </summary>
    public void InvalidateCache()
    {
        _cacheValid = false;
        _cachedBitmap = null;
    }

    private void RenderWithCache(bool forceFullRender)
    {
        using var dc = _drawingVisual.RenderOpen();

        dc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, ActualWidth, ActualHeight));

        var entities = Entities;
        if (entities == null || ActualWidth <= 0 || ActualHeight <= 0)
            return;

        // Cache viewport bounds for this frame to avoid recalculating multiple times
        _frameViewportBounds = CalculateViewportBounds();

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;

        // Apply view transforms (flip and rotation)
        ApplyViewTransform(dc, width, height);

        // Check if we need to invalidate the cache
        bool scaleChanged = Math.Abs(_scale - _cachedScale) > 0.0001;
        bool sizeChanged = width != _cachedWidth || height != _cachedHeight;
        bool selectionChanged = !SelectionMatchesCache();
        bool layersChanged = !HiddenLayersMatchCache();

        // During panning with valid cache, use fast bitmap offset rendering
        if (_isPanning && _cacheValid && !scaleChanged && !sizeChanged && !forceFullRender)
        {
            RenderFromCacheWithOffset(dc, width, height);
            RestoreViewTransform(dc);
            return;
        }

        // If only selection changed, we can render cache + selection overlay
        if (_cacheValid && !scaleChanged && !sizeChanged && !layersChanged && selectionChanged && !forceFullRender)
        {
            RenderCacheWithSelectionOverlay(dc, entities, width, height);
            RestoreViewTransform(dc);
            return;
        }

        // Full render required - rebuild the cache
        if (!_cacheValid || scaleChanged || sizeChanged || layersChanged || forceFullRender)
        {
            RebuildCache(entities, width, height);
        }

        // Draw from cache
        if (_cachedBitmap != null)
        {
            dc.DrawImage(_cachedBitmap, new Rect(0, 0, width, height));
        }

        // Draw selection overlay
        RenderSelectionOverlay(dc, entities);

        // Draw grid (on top of entities but below preview)
        RenderGrid(dc);

        // Draw drawing preview
        RenderDrawingPreview(dc);

        RestoreViewTransform(dc);
    }

    private void ApplyViewTransform(DrawingContext dc, int width, int height)
    {
        if (!_flipX && !_flipY && Math.Abs(_viewRotation) < 0.001)
            return;

        double centerX = width / 2.0;
        double centerY = height / 2.0;

        var transformGroup = new TransformGroup();

        // Apply flip transforms using ScaleTransform with center point
        if (_flipX || _flipY)
        {
            double scaleX = _flipX ? -1 : 1;
            double scaleY = _flipY ? -1 : 1;
            transformGroup.Children.Add(new ScaleTransform(scaleX, scaleY, centerX, centerY));
        }

        // Apply rotation around center
        if (Math.Abs(_viewRotation) >= 0.001)
        {
            transformGroup.Children.Add(new RotateTransform(_viewRotation, centerX, centerY));
        }

        dc.PushTransform(transformGroup);
    }

    private void RestoreViewTransform(DrawingContext dc)
    {
        if (!_flipX && !_flipY && Math.Abs(_viewRotation) < 0.001)
            return;

        dc.Pop();
    }

    private void RenderGrid(DrawingContext dc)
    {
        if (GridSettings == null || !GridSettings.ShowGrid)
            return;

        var viewport = _frameViewportBounds ?? CalculateViewportBounds();
        double spacingX = GridSettings.SpacingX;
        double spacingY = GridSettings.SpacingY;

        if (spacingX <= 0 || spacingY <= 0)
            return;

        // Don't render grid if spacing is too small on screen
        double screenSpacingX = spacingX * _scale;
        double screenSpacingY = spacingY * _scale;

        if (screenSpacingX < 5 || screenSpacingY < 5)
            return;

        // Calculate grid line range
        double startX = Math.Floor((viewport.Left - GridSettings.OriginX) / spacingX) * spacingX + GridSettings.OriginX;
        double endX = viewport.Right;
        double startY = Math.Floor((viewport.Top - GridSettings.OriginY) / spacingY) * spacingY + GridSettings.OriginY;
        double endY = viewport.Bottom;

        // Limit number of grid lines for performance
        int maxLines = 200;
        int lineCount = 0;

        // Draw vertical lines
        for (double x = startX; x <= endX && lineCount < maxLines; x += spacingX)
        {
            var screenStart = CadToScreen(x, viewport.Top);
            var screenEnd = CadToScreen(x, viewport.Bottom);

            bool isMajor = Math.Abs(x % (spacingX * 5)) < 0.0001;
            dc.DrawLine(isMajor ? GridPenMajor : GridPenMinor, screenStart, screenEnd);
            lineCount++;
        }

        // Draw horizontal lines
        for (double y = startY; y <= endY && lineCount < maxLines; y += spacingY)
        {
            var screenStart = CadToScreen(viewport.Left, y);
            var screenEnd = CadToScreen(viewport.Right, y);

            bool isMajor = Math.Abs(y % (spacingY * 5)) < 0.0001;
            dc.DrawLine(isMajor ? GridPenMajor : GridPenMinor, screenStart, screenEnd);
            lineCount++;
        }
    }

    private void RenderDrawingPreview(DrawingContext dc)
    {
        if (_currentTool == null || !_currentTool.IsDrawing)
            return;

        var previewPoints = _currentTool.GetPreviewPoints();
        if (previewPoints == null || previewPoints.Count < 2)
            return;

        // Convert CAD points to screen points
        var screenPoints = previewPoints.Select(p => CadToScreen(p.X, p.Y)).ToList();

        // Draw preview lines
        for (int i = 0; i < screenPoints.Count - 1; i++)
        {
            dc.DrawLine(PreviewPen, screenPoints[i], screenPoints[i + 1]);
        }

        // Close the shape if it's a polygon preview
        if (_currentTool.IsPreviewClosed && screenPoints.Count > 2)
        {
            dc.DrawLine(PreviewPen, screenPoints[^1], screenPoints[0]);
        }

        // Draw points
        var pointBrush = Brushes.Cyan;
        foreach (var point in screenPoints)
        {
            dc.DrawEllipse(pointBrush, null, point, 4, 4);
        }
    }

    private WpfPoint CadToScreen(double cadX, double cadY)
    {
        return new WpfPoint(
            (cadX + _offset.X) * _scale,
            (-cadY + _offset.Y) * _scale
        );
    }

    private bool SelectionMatchesCache()
    {
        if (_cachedSelectedHandles == null)
            return _selectedHandlesSet.Count == 0;
        return _cachedSelectedHandles.SetEquals(_selectedHandlesSet);
    }

    private bool HiddenLayersMatchCache()
    {
        var currentLayers = HiddenLayers?.ToHashSet() ?? new HashSet<string>();
        if (_cachedHiddenLayers == null)
            return currentLayers.Count == 0;
        return _cachedHiddenLayers.SetEquals(currentLayers);
    }

    private void RebuildCache(IEnumerable<Entity> entities, int width, int height)
    {
        // Create a new bitmap for caching
        var dpi = VisualTreeHelper.GetDpi(this);
        _cachedBitmap = new RenderTargetBitmap(
            (int)(width * dpi.DpiScaleX),
            (int)(height * dpi.DpiScaleY),
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32);

        var cacheVisual = new DrawingVisual();
        using (var cacheDc = cacheVisual.RenderOpen())
        {
            // Draw background
            cacheDc.DrawRectangle(Brushes.Black, null, new Rect(0, 0, width, height));

            // Create render context WITHOUT selection (cache unselected entities)
            var renderContext = new RenderContext
            {
                Scale = _scale,
                Offset = _offset,
                DefaultColor = Colors.White,
                LineThickness = 1.0,
                ShowSelection = false, // Don't show selection in cache
                ViewportBounds = _frameViewportBounds ?? CalculateViewportBounds()
            };

            if (HiddenLayers != null)
            {
                foreach (var layer in HiddenLayers)
                {
                    renderContext.HiddenLayers.Add(layer);
                }
            }

            _renderService.RenderEntities(cacheDc, entities, renderContext);
        }

        _cachedBitmap.Render(cacheVisual);
        _cachedBitmap.Freeze(); // Freeze for performance

        // Update cache metadata
        _cachedScale = _scale;
        _cachedOffset = _offset;
        _cachedWidth = width;
        _cachedHeight = height;
        _cachedHiddenLayers = HiddenLayers?.ToHashSet() ?? new HashSet<string>();
        _cachedSelectedHandles = new HashSet<ulong>(_selectedHandlesSet);
        _cacheValid = true;
    }

    private void RenderFromCacheWithOffset(DrawingContext dc, int width, int height)
    {
        if (_cachedBitmap == null)
            return;

        // Calculate the pixel offset from cached position
        double deltaX = (_offset.X - _cachedOffset.X) * _scale;
        double deltaY = (_offset.Y - _cachedOffset.Y) * _scale;

        // Draw the cached bitmap with offset
        dc.DrawImage(_cachedBitmap, new Rect(deltaX, deltaY, width, height));

        // Draw selection overlay at current position
        var entities = Entities;
        if (entities != null)
        {
            RenderSelectionOverlay(dc, entities);
        }
    }

    private void RenderCacheWithSelectionOverlay(DrawingContext dc, IEnumerable<Entity> entities, int width, int height)
    {
        // Draw cached bitmap
        if (_cachedBitmap != null)
        {
            dc.DrawImage(_cachedBitmap, new Rect(0, 0, width, height));
        }

        // Draw selection overlay
        RenderSelectionOverlay(dc, entities);

        // Update cached selection
        _cachedSelectedHandles = new HashSet<ulong>(_selectedHandlesSet);
    }

    private void RenderSelectionOverlay(DrawingContext dc, IEnumerable<Entity> entities)
    {
        if (_selectedHandlesSet.Count == 0)
            return;

        var renderContext = new RenderContext
        {
            Scale = _scale,
            Offset = _offset,
            DefaultColor = Colors.White,
            LineThickness = 1.0,
            ShowSelection = true,
            ViewportBounds = _frameViewportBounds ?? CalculateViewportBounds()
        };

        foreach (var handle in _selectedHandlesSet)
        {
            renderContext.SelectedHandles.Add(handle);
        }

        if (HiddenLayers != null)
        {
            foreach (var layer in HiddenLayers)
            {
                renderContext.HiddenLayers.Add(layer);
            }
        }

        // Use handle->entity dictionary for O(1) lookup per selected handle
        if (_entityByHandle != null)
        {
            var selectedEntities = new List<Entity>(_selectedHandlesSet.Count);
            foreach (var handle in _selectedHandlesSet)
            {
                if (_entityByHandle.TryGetValue(handle, out var entity))
                    selectedEntities.Add(entity);
            }
            _renderService.RenderEntities(dc, selectedEntities, renderContext);
        }
        else
        {
            // Fallback: linear scan
            var selectedEntities = entities.Where(e => _selectedHandlesSet.Contains(e.Handle));
            _renderService.RenderEntities(dc, selectedEntities, renderContext);
        }
    }

    /// <summary>
    /// Gets the current viewport bounds in CAD coordinates.
    /// </summary>
    public Rect GetViewportBounds()
    {
        return CalculateViewportBounds();
    }

    private Rect CalculateViewportBounds()
    {
        // Convert screen corners to CAD coordinates
        var topLeft = ScreenToCad(new WpfPoint(0, 0));
        var bottomRight = ScreenToCad(new WpfPoint(ActualWidth, ActualHeight));

        double minX = Math.Min(topLeft.X, bottomRight.X);
        double maxX = Math.Max(topLeft.X, bottomRight.X);
        double minY = Math.Min(topLeft.Y, bottomRight.Y);
        double maxY = Math.Max(topLeft.Y, bottomRight.Y);

        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    protected override int VisualChildrenCount => _visuals.Count;

    protected override Visual GetVisualChild(int index) => _visuals[index];

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        drawingContext.DrawRectangle(Brushes.Black, null, new Rect(RenderSize));
    }

    private static void OnEntitiesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            canvas.CalculateExtents();
            canvas.RebuildSpatialIndex();
            canvas.InvalidateCache(); // Entities changed - full cache rebuild needed
            canvas.Render();
        }
    }

    private static void OnSelectedHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            // Rebuild the cached selected handles hashset
            canvas._selectedHandlesSet = canvas.SelectedHandles?.ToHashSet() ?? new HashSet<ulong>();
            // Selection changes don't invalidate cache - we render selection as overlay
            canvas.Render();
        }
    }

    private static void OnHiddenLayersChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            canvas.InvalidateCache(); // Layer visibility changed - full cache rebuild needed
            canvas.Render();
        }
    }

    private static void OnExtentsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas && e.NewValue is Extents extents)
        {
            canvas._extents = extents;
        }
    }

    private static void OnDrawingModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            canvas.UpdateDrawingTool();
        }
    }

    private static void OnGridSettingsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            if (e.OldValue is GridSnapSettings oldSettings)
            {
                oldSettings.PropertyChanged -= canvas.OnGridSettingsPropertyChanged;
            }

            if (e.NewValue is GridSnapSettings newSettings)
            {
                newSettings.PropertyChanged += canvas.OnGridSettingsPropertyChanged;
            }

            canvas.Render();
        }
    }

    private static void OnViewTransformChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
            canvas._flipX = canvas.FlipX;
            canvas._flipY = canvas.FlipY;
            canvas._viewRotation = canvas.ViewRotation;
            canvas.InvalidateCache();
            canvas.Render();
        }
    }

    private void OnGridSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        Render();
    }

    private void UpdateDrawingTool()
    {
        IDrawingTool? tool = DrawingMode switch
        {
            DrawingMode.DrawLine => new LineTool(),
            DrawingMode.DrawPolyline => new PolylineTool(),
            DrawingMode.DrawPolygon => new PolygonTool(),
            _ => null
        };

        SetDrawingTool(tool);
    }

    private void OnDrawingToolCompleted(object? sender, DrawingCompletedEventArgs e)
    {
        DrawingCompleted?.Invoke(this, e);

        // Reset tool for next drawing
        _currentTool?.Reset();
        Render();
    }

    private void OnDrawingToolCancelled(object? sender, EventArgs e)
    {
        Render();
    }

    private void UpdateCursor()
    {
        Cursor = DrawingMode switch
        {
            DrawingMode.Pan => Cursors.Hand,
            DrawingMode.DrawLine or DrawingMode.DrawPolyline or DrawingMode.DrawPolygon => Cursors.Cross,
            _ => Cursors.Arrow
        };
    }

    private void CalculateExtents()
    {
        var entities = Entities;
        if (entities == null)
        {
            _extents = null;
            return;
        }

        _extents = new Extents();
        foreach (var entity in entities)
        {
            _extents.Expand(entity);
        }
    }

    private void RebuildSpatialIndex()
    {
        var entities = Entities;
        if (entities == null)
        {
            _spatialGrid = null;
            _entityByHandle = null;
            return;
        }

        var entityList = entities.ToList();

        // Build handle -> entity dictionary
        _entityByHandle = new Dictionary<ulong, Entity>(entityList.Count);
        foreach (var entity in entityList)
        {
            _entityByHandle[entity.Handle] = entity;
        }

        // Compute grid extents from BoundingBoxHelper (same source as spatial grid insertion)
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool hasBounds = false;

        foreach (var entity in entityList)
        {
            var bounds = BoundingBoxHelper.GetBounds(entity);
            if (bounds.HasValue)
            {
                hasBounds = true;
                minX = Math.Min(minX, bounds.Value.Left);
                minY = Math.Min(minY, bounds.Value.Top);
                maxX = Math.Max(maxX, bounds.Value.Right);
                maxY = Math.Max(maxY, bounds.Value.Bottom);
            }
        }

        if (hasBounds && maxX > minX && maxY > minY)
        {
            var rect = new Rect(minX, minY, maxX - minX, maxY - minY);
            _spatialGrid = SpatialGrid.Build(entityList, rect);
        }
        else
        {
            _spatialGrid = null;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_currentTool != null)
        {
            _currentTool.OnKeyDown(e.Key);
            Render();
            e.Handled = true;
        }
    }

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        double factor = e.Delta > 0 ? 1.1 : 1.0 / 1.1;
        WpfPoint mousePos = e.GetPosition(this);
        ZoomAtPoint(mousePos, factor);
    }

    private void ZoomAtPoint(WpfPoint screenPoint, double factor)
    {
        var cadPoint = ScreenToCad(screenPoint);

        _scale *= factor;
        _scale = Math.Max(0.001, Math.Min(1000, _scale));

        _offset = new WpfPoint(
            screenPoint.X / _scale - cadPoint.X,
            screenPoint.Y / _scale + cadPoint.Y
        );

        // Scale changed - cache will be automatically invalidated in RenderWithCache
        Render();
    }

    private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        Focus();

        var screenPoint = e.GetPosition(this);

        // Handle drawing mode - use snapped point for precise drawing
        if (_currentTool != null && DrawingMode != DrawingMode.Select && DrawingMode != DrawingMode.Pan)
        {
            var snappedPoint = GetSnappedCadPoint(screenPoint);
            _currentTool.OnMouseDown(snappedPoint, MouseButton.Left);
            Render();
            return;
        }

        if (IsPanMode || DrawingMode == DrawingMode.Pan)
        {
            StartPan(screenPoint);
            return;
        }

        // Selection mode - use raw (unsnapped) point for accurate hit testing
        var cadPoint = ScreenToCad(screenPoint);

        var entities = Entities;
        if (entities == null)
            return;

        double tolerance = 5.0 / _scale;

        // Use spatial grid for fast hit testing when available
        Entity? hitEntity = null;
        if (_spatialGrid != null)
        {
            var candidates = _spatialGrid.Query(cadPoint, tolerance);
            foreach (var entity in candidates)
            {
                if (HitTestHelper.HitTest(entity, cadPoint, tolerance))
                {
                    hitEntity = entity;
                    break;
                }
            }
        }
        else
        {
            // Fallback: linear scan
            foreach (var entity in entities)
            {
                if (HitTestHelper.HitTest(entity, cadPoint, tolerance))
                {
                    hitEntity = entity;
                    break;
                }
            }
        }

        bool addToSelection = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        EntityClicked?.Invoke(this, new CadEntityClickEventArgs(hitEntity, addToSelection));
    }

    private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning && IsPanMode)
        {
            EndPan();
        }
    }

    private void OnMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var screenPoint = e.GetPosition(this);
        var cadPoint = GetSnappedCadPoint(screenPoint);

        // If drawing, right-click can complete or cancel
        if (_currentTool != null && _currentTool.IsDrawing)
        {
            _currentTool.OnMouseDown(cadPoint, MouseButton.Right);
            Render();
            return;
        }

        StartPan(screenPoint);
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isPanning)
        {
            EndPan();
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screenPoint = e.GetPosition(this);
        var cadPoint = GetSnappedCadPoint(screenPoint);
        _currentCadPoint = cadPoint;

        CadMouseMove?.Invoke(this, new CadMouseEventArgs(cadPoint.X, cadPoint.Y));

        // Update drawing tool preview
        if (_currentTool != null && _currentTool.IsDrawing)
        {
            _currentTool.OnMouseMove(cadPoint);
            Render();
        }

        if (_isPanning)
        {
            var currentPos = screenPoint;
            var delta = new WpfPoint(
                (currentPos.X - _panStart.X) / _scale,
                (currentPos.Y - _panStart.Y) / _scale
            );

            _offset = new WpfPoint(_offset.X + delta.X, _offset.Y + delta.Y);
            _panStart = currentPos;

            Render();
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        InvalidateCache(); // Size changed - need new bitmap
        Render();
    }

    private void StartPan(WpfPoint startPoint)
    {
        _isPanning = true;
        _panStart = startPoint;
        CaptureMouse();
        Cursor = Cursors.Hand;
    }

    private void EndPan()
    {
        _isPanning = false;
        ReleaseMouseCapture();
        Cursor = IsPanMode ? Cursors.Hand : Cursors.Arrow;

        // Rebuild cache at new offset position for crisp rendering
        InvalidateCache();
        Render();
    }
}

public class CadMouseEventArgs : EventArgs
{
    public double X { get; }
    public double Y { get; }

    public CadMouseEventArgs(double x, double y)
    {
        X = x;
        Y = y;
    }
}

public class CadEntityClickEventArgs : EventArgs
{
    public Entity? Entity { get; }
    public bool AddToSelection { get; }

    public CadEntityClickEventArgs(Entity? entity, bool addToSelection)
    {
        Entity = entity;
        AddToSelection = addToSelection;
    }
}
