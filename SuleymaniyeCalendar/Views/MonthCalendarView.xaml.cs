using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Models;
using System.Collections.Specialized;
using Microsoft.Maui.Controls.Shapes;

namespace SuleymaniyeCalendar.Views;

/// <summary>
/// Phase 20: Beautiful calendar grid view for monthly prayer times.
/// 🚀 PHASE 20.1D: Optimized with fast Grid rendering (10-20x faster than CollectionView).
/// 🔧 PHASE 20.1E: Element pooling - reuse Border/Label elements instead of recreating.
/// </summary>
public partial class MonthCalendarView : ContentView
{
    private MonthViewModel _viewModel;
    private CancellationTokenSource _renderCts;
    private int _lastRenderedHash; // Skip redundant renders
    
    // Cached brushes to avoid repeated resource lookups
    private SolidColorBrush _lightIndicatorBrush;
    private SolidColorBrush _darkIndicatorBrush;
    
    // 🚀 Element pool for reuse - avoids expensive element creation
    private readonly List<Border> _cellPool = new(42);
    private bool _poolInitialized;
    
    // Cached values
    private static readonly CornerRadius _cornerRadius = new(8);
    private static readonly Thickness _indicatorMargin = new(0, 0, 0, 4);

    public MonthCalendarView()
    {
        InitializeComponent();
        
        // Subscribe to BindingContext changes to hook up ViewModel
        BindingContextChanged += OnBindingContextChanged;
    }

    private void OnBindingContextChanged(object sender, EventArgs e)
    {
        // Unsubscribe from old ViewModel
        if (_viewModel != null && _viewModel.CalendarDays != null)
        {
            _viewModel.CalendarDays.CollectionChanged -= OnCalendarDaysChanged;
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new ViewModel
        if (BindingContext is MonthViewModel viewModel)
        {
            _viewModel = viewModel;
            
            // Subscribe to property changes (to catch when CalendarDays itself changes)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            
            // Subscribe to collection changes
            if (_viewModel.CalendarDays != null)
            {
                _viewModel.CalendarDays.CollectionChanged += OnCalendarDaysChanged;
                
                // Initial render if data already exists
                if (_viewModel.CalendarDays.Count > 0)
                {
                    RenderCalendarGrid();
                }
            }
        }
    }

    /// <summary>
    /// Called when ViewModel properties change - re-subscribe if CalendarDays collection is replaced.
    /// </summary>
    private void OnViewModelPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MonthViewModel.CalendarDays))
        {
            System.Diagnostics.Debug.WriteLine($"🔄 CalendarDays property changed");
            
            // Unsubscribe from old collection
            if (sender is MonthViewModel vm && vm.CalendarDays != null)
            {
                // Remove old handler (safe even if not subscribed)
                vm.CalendarDays.CollectionChanged -= OnCalendarDaysChanged;
                
                // Subscribe to new collection
                vm.CalendarDays.CollectionChanged += OnCalendarDaysChanged;
                
                // Cancel any pending render
                _renderCts?.Cancel();
                _renderCts = new CancellationTokenSource();
                var token = _renderCts.Token;
                
                // The new collection is already populated, so CollectionChanged won't fire.
                // We must render now with debounce.
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(20, token); // Reduced from 30ms
                        if (!token.IsCancellationRequested)
                        {
                            await MainThread.InvokeOnMainThreadAsync(() =>
                            {
                                if (!token.IsCancellationRequested)
                                {
                                    RenderCalendarGrid();
                                }
                            });
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected when canceled
                    }
                });
            }
        }
    }

    /// <summary>
    /// Called when CalendarDays collection changes - rebuild the grid.
    /// </summary>
    private void OnCalendarDaysChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        // Cancel any pending render and use debounced approach
        _renderCts?.Cancel();
        _renderCts = new CancellationTokenSource();
        var token = _renderCts.Token;
        
        // Debounce: 20ms delay to coalesce rapid updates
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(20, token);
                if (!token.IsCancellationRequested)
                {
                    await MainThread.InvokeOnMainThreadAsync(() =>
                    {
                        if (!token.IsCancellationRequested)
                        {
                            RenderCalendarGrid();
                        }
                    });
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when canceled
            }
        });
    }

    /// <summary>
    /// 🚀 PHASE 20.1E: Initialize element pool once - creates all 42 cells upfront.
    /// This avoids expensive element creation during navigation.
    /// </summary>
    private void InitializePool()
    {
        if (_poolInitialized) return;
        
        // Cache brushes once
        _lightIndicatorBrush ??= new SolidColorBrush((Color)Application.Current.Resources["PrimaryColor"]);
        _darkIndicatorBrush ??= new SolidColorBrush((Color)Application.Current.Resources["Primary50"]);
        
        // Create 42 reusable cells (max needed for 6-week month)
        for (int i = 0; i < 42; i++)
        {
            var border = CreateCell();
            _cellPool.Add(border);
        }
        
        _poolInitialized = true;
        System.Diagnostics.Debug.WriteLine($"🏊 Pool initialized with 42 cells");
    }
    
    /// <summary>
    /// Creates a single cell with all child elements.
    /// </summary>
    private Border CreateCell()
    {
        var border = new Border
        {
            StrokeShape = new RoundRectangle { CornerRadius = _cornerRadius },
            HeightRequest = 48,
            Padding = 4
        };

        var contentGrid = new Grid();

        var dayLabel = new Label
        {
            FontSize = 14,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            LineHeight = 1.2
        };
        contentGrid.Add(dayLabel);

        var indicator = new Ellipse
        {
            WidthRequest = 4,
            HeightRequest = 4,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.End,
            Margin = _indicatorMargin
        };
        indicator.SetAppTheme(Ellipse.FillProperty, _lightIndicatorBrush, _darkIndicatorBrush);
        contentGrid.Add(indicator);

        border.Content = contentGrid;

        // Tap gesture - will update handler when binding data
        var tapGesture = new TapGestureRecognizer();
        border.GestureRecognizers.Add(tapGesture);

        return border;
    }
    
    /// <summary>
    /// Updates a pooled cell with new day data - much faster than creating new elements.
    /// </summary>
    private void UpdateCell(Border border, CalendarDay day, int row, int col)
    {
        // Update visual properties directly (no binding overhead for initial values)
        border.BackgroundColor = day.BackgroundColor;
        border.Stroke = day.BorderColor;
        border.StrokeThickness = day.BorderThickness;
        
        // Clear old bindings and set new ones for properties that change on selection
        border.RemoveBinding(Border.BackgroundColorProperty);
        border.RemoveBinding(Border.StrokeProperty);
        border.RemoveBinding(Border.StrokeThicknessProperty);
        border.SetBinding(Border.BackgroundColorProperty, new Binding(nameof(CalendarDay.BackgroundColor), source: day));
        border.SetBinding(Border.StrokeProperty, new Binding(nameof(CalendarDay.BorderColor), source: day));
        border.SetBinding(Border.StrokeThicknessProperty, new Binding(nameof(CalendarDay.BorderThickness), source: day));

        // Update label
        if (border.Content is Grid contentGrid && contentGrid.Children.Count >= 1)
        {
            if (contentGrid.Children[0] is Label dayLabel)
            {
                dayLabel.Text = day.Day.ToString();
                dayLabel.FontAttributes = day.FontAttributesValue;
                dayLabel.TextColor = day.TextColor;
                
                // Rebind for selection changes
                dayLabel.RemoveBinding(Label.FontAttributesProperty);
                dayLabel.RemoveBinding(Label.TextColorProperty);
                dayLabel.SetBinding(Label.FontAttributesProperty, new Binding(nameof(CalendarDay.FontAttributesValue), source: day));
                dayLabel.SetBinding(Label.TextColorProperty, new Binding(nameof(CalendarDay.TextColor), source: day));
            }
            
            // Update indicator visibility
            if (contentGrid.Children.Count >= 2 && contentGrid.Children[1] is Ellipse indicator)
            {
                indicator.IsVisible = day.HasData;
            }
        }

        // Update tap gesture handler
        if (border.GestureRecognizers.Count > 0 && border.GestureRecognizers[0] is TapGestureRecognizer tapGesture)
        {
            // Remove old handler and add new one
            tapGesture.Tapped -= OnCellTapped;
            border.BindingContext = day; // Store day for tap handler
            tapGesture.Tapped += OnCellTapped;
        }

        // Set grid position
        Grid.SetRow(border, row);
        Grid.SetColumn(border, col);
    }
    
    /// <summary>
    /// Unified tap handler for pooled cells.
    /// </summary>
    private async void OnCellTapped(object sender, TappedEventArgs e)
    {
        if (_viewModel != null && sender is TapGestureRecognizer gesture && 
            gesture.Parent is Border tappedBorder && 
            tappedBorder.BindingContext is CalendarDay day)
        {
            // Subtle scale animation
            await tappedBorder.ScaleTo(0.92, 80, Easing.CubicOut);
            await tappedBorder.ScaleTo(1.0, 120, Easing.CubicOut);
            
            // Execute selection
            _viewModel.SelectDayCommand.Execute(day.Date);
            
            // Animate selected day card
            if (SelectedDayCard != null && SelectedDayCard.IsVisible)
            {
                await AnimateSelectedDayCardAsync();
            }
        }
    }

    /// <summary>
    /// 🚀 PHASE 20.1E: Fast grid rendering using element pooling.
    /// Reuses existing elements instead of destroying and recreating - 5-10x faster.
    /// </summary>
    private void RenderCalendarGrid()
    {
        if (_viewModel?.CalendarDays == null || CalendarGrid == null)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ RenderCalendarGrid skipped: ViewModel={_viewModel != null}, CalendarDays={_viewModel?.CalendarDays != null}, CalendarGrid={CalendarGrid != null}");
            return;
        }

        var daysCount = _viewModel.CalendarDays.Count;
        if (daysCount == 0)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ RenderCalendarGrid skipped: CalendarDays is empty");
            return;
        }

        // Skip redundant renders by checking hash of displayed month
        var firstDay = _viewModel.CalendarDays.FirstOrDefault();
        var hash = firstDay?.Date.GetHashCode() ?? 0;
        if (hash == _lastRenderedHash && CalendarGrid.Children.Count > 0)
        {
            System.Diagnostics.Debug.WriteLine($"⏭️ RenderCalendarGrid skipped: Same month already rendered");
            return;
        }
        _lastRenderedHash = hash;

        System.Diagnostics.Debug.WriteLine($"✅ RenderCalendarGrid: Updating grid with {daysCount} days");

        // Initialize pool on first use
        InitializePool();

        // Clear grid but keep pooled elements
        CalendarGrid.Clear();

        int row = 0;
        int col = 0;
        int index = 0;

        foreach (var day in _viewModel.CalendarDays)
        {
            if (index >= _cellPool.Count) break; // Safety check
            
            var border = _cellPool[index];
            UpdateCell(border, day, row, col);
            CalendarGrid.Add(border);

            col++;
            if (col >= 7)
            {
                col = 0;
                row++;
            }
            index++;
        }
    }

    /// <summary>
    /// Handles day cell taps by invoking the SelectDayCommand from the ViewModel.
    /// This avoids binding context issues with DataTemplate bindings.
    /// </summary>
    private void OnDayTapped(object sender, TappedEventArgs e)
    {
        if (sender is Border border && 
            border.BindingContext is CalendarDay day &&
            this.BindingContext is MonthViewModel viewModel)
        {
            viewModel.SelectDayCommand.Execute(day.Date);
        }
    }

    /// <summary>
    /// 🎨 PHASE 20.2C: Subtle fade-in animation for selected day card.
    /// Creates smooth visual feedback when selecting a day.
    /// </summary>
    private async Task AnimateSelectedDayCardAsync()
    {
        if (SelectedDayCard == null) return;

        try
        {
            // Start from slightly transparent and small
            SelectedDayCard.Opacity = 0.7;
            SelectedDayCard.Scale = 0.98;

            // Fade in and scale up smoothly
            var opacityTask = SelectedDayCard.FadeTo(1.0, 200, Easing.CubicOut);
            var scaleTask = SelectedDayCard.ScaleTo(1.0, 200, Easing.CubicOut);

            await Task.WhenAll(opacityTask, scaleTask);
        }
        catch
        {
            // Silently handle any animation errors (e.g., if card is removed during animation)
        }
    }
}
