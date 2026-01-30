using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ACadSharp.Entities;
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

    // Bitmap caching for performance
    private RenderTargetBitmap? _cachedBitmap;
    private double _cachedScale;
    private WpfPoint _cachedOffset;
    private int _cachedWidth;
    private int _cachedHeight;
    private bool _cacheValid;
    private HashSet<ulong>? _cachedSelectedHandles;
    private HashSet<string>? _cachedHiddenLayers;

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

    public event EventHandler<CadMouseEventArgs>? CadMouseMove;
    public event EventHandler<CadEntityClickEventArgs>? EntityClicked;

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

        int width = (int)ActualWidth;
        int height = (int)ActualHeight;

        // Check if we need to invalidate the cache
        bool scaleChanged = Math.Abs(_scale - _cachedScale) > 0.0001;
        bool sizeChanged = width != _cachedWidth || height != _cachedHeight;
        bool selectionChanged = !SelectionMatchesCache();
        bool layersChanged = !HiddenLayersMatchCache();

        // During panning with valid cache, use fast bitmap offset rendering
        if (_isPanning && _cacheValid && !scaleChanged && !sizeChanged && !forceFullRender)
        {
            RenderFromCacheWithOffset(dc, width, height);
            return;
        }

        // If only selection changed, we can render cache + selection overlay
        if (_cacheValid && !scaleChanged && !sizeChanged && !layersChanged && selectionChanged && !forceFullRender)
        {
            RenderCacheWithSelectionOverlay(dc, entities, width, height);
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
    }

    private bool SelectionMatchesCache()
    {
        var currentHandles = SelectedHandles?.ToHashSet() ?? new HashSet<ulong>();
        if (_cachedSelectedHandles == null)
            return currentHandles.Count == 0;
        return _cachedSelectedHandles.SetEquals(currentHandles);
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
                ViewportBounds = CalculateViewportBounds()
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
        _cachedSelectedHandles = SelectedHandles?.ToHashSet() ?? new HashSet<ulong>();
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
        _cachedSelectedHandles = SelectedHandles?.ToHashSet() ?? new HashSet<ulong>();
    }

    private void RenderSelectionOverlay(DrawingContext dc, IEnumerable<Entity> entities)
    {
        if (SelectedHandles == null || !SelectedHandles.Any())
            return;

        var selectedSet = SelectedHandles.ToHashSet();

        var renderContext = new RenderContext
        {
            Scale = _scale,
            Offset = _offset,
            DefaultColor = Colors.White,
            LineThickness = 1.0,
            ShowSelection = true,
            ViewportBounds = CalculateViewportBounds()
        };

        foreach (var handle in selectedSet)
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

        // Only render selected entities
        var selectedEntities = entities.Where(e => selectedSet.Contains(e.Handle));
        _renderService.RenderEntities(dc, selectedEntities, renderContext);
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
            canvas.InvalidateCache(); // Entities changed - full cache rebuild needed
            canvas.Render();
        }
    }

    private static void OnSelectedHandlesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is CadCanvas canvas)
        {
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

        if (IsPanMode)
        {
            StartPan(e.GetPosition(this));
            return;
        }

        var screenPoint = e.GetPosition(this);
        var cadPoint = ScreenToCad(screenPoint);

        var entities = Entities;
        if (entities == null)
            return;

        double tolerance = 5.0 / _scale;

        Entity? hitEntity = null;
        foreach (var entity in entities)
        {
            if (HitTestHelper.HitTest(entity, cadPoint, tolerance))
            {
                hitEntity = entity;
                break;
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
        StartPan(e.GetPosition(this));
    }

    private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndPan();
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        var screenPoint = e.GetPosition(this);
        var cadPoint = ScreenToCad(screenPoint);

        CadMouseMove?.Invoke(this, new CadMouseEventArgs(cadPoint.X, cadPoint.Y));

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
