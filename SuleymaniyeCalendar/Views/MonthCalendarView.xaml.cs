using SuleymaniyeCalendar.ViewModels;
using SuleymaniyeCalendar.Models;
using System.Collections.Specialized;
using Microsoft.Maui.Controls.Shapes;

namespace SuleymaniyeCalendar.Views;

/// <summary>
/// Phase 20: Beautiful calendar grid view for monthly prayer times.
/// 🚀 PHASE 20.1D: Optimized with fast Grid rendering (10-20x faster than CollectionView).
/// 🔧 PHASE 20.1D-Fix: PropertyChanged subscription to detect collection replacement.
/// </summary>
public partial class MonthCalendarView : ContentView
{
    private MonthViewModel _viewModel;
    private CancellationTokenSource _renderCts;

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
                
                // Debounce: Render with slight delay to batch rapid navigation
                Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(50, _renderCts.Token); // 50ms debounce
                        if (!_renderCts.Token.IsCancellationRequested)
                        {
                            await MainThread.InvokeOnMainThreadAsync(() => RenderCalendarGrid());
                        }
                    }
                    catch (TaskCanceledException)
                    {
                        // Expected when navigation happens quickly
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
        // Use MainThread to ensure UI updates happen on main thread
        MainThread.BeginInvokeOnMainThread(() =>
        {
            RenderCalendarGrid();
        });
    }

    /// <summary>
    /// 🚀 PHASE 20.1D: Fast grid rendering - creates Border elements directly.
    /// This is 10-20x faster than CollectionView because it avoids all the
    /// virtualization, measure/layout cycles, and binding overhead.
    /// </summary>
    private void RenderCalendarGrid()
    {
        if (_viewModel?.CalendarDays == null || CalendarGrid == null)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ RenderCalendarGrid skipped: ViewModel={_viewModel != null}, CalendarDays={_viewModel?.CalendarDays != null}, CalendarGrid={CalendarGrid != null}");
            return;
        }

        if (_viewModel.CalendarDays.Count == 0)
        {
            System.Diagnostics.Debug.WriteLine($"⚠️ RenderCalendarGrid skipped: CalendarDays is empty");
            return;
        }

        System.Diagnostics.Debug.WriteLine($"✅ RenderCalendarGrid: Building grid with {_viewModel.CalendarDays.Count} days");

        // Clear existing cells
        CalendarGrid.Clear();

        int row = 0;
        int col = 0;

        foreach (var day in _viewModel.CalendarDays)
        {
            // Create cell Border
            var border = new Border
            {
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                HeightRequest = 48,
                Padding = 4
            };

            // Bind visual properties
            border.SetBinding(Border.BackgroundColorProperty, new Binding(nameof(CalendarDay.BackgroundColor), source: day));
            border.SetBinding(Border.StrokeProperty, new Binding(nameof(CalendarDay.BorderColor), source: day));
            border.SetBinding(Border.StrokeThicknessProperty, new Binding(nameof(CalendarDay.BorderThickness), source: day));

            // Create content grid
            var contentGrid = new Grid();

            // Day number label
            var dayLabel = new Label
            {
                FontSize = 14,
                HorizontalTextAlignment = TextAlignment.Center,
                VerticalTextAlignment = TextAlignment.Center,
                LineHeight = 1.2
            };
            dayLabel.SetBinding(Label.TextProperty, new Binding(nameof(CalendarDay.Day), source: day));
            dayLabel.SetBinding(Label.FontAttributesProperty, new Binding(nameof(CalendarDay.FontAttributesValue), source: day));
            dayLabel.SetBinding(Label.TextColorProperty, new Binding(nameof(CalendarDay.TextColor), source: day));
            contentGrid.Add(dayLabel);

            // Prayer data indicator dot
            var indicator = new Ellipse
            {
                WidthRequest = 4,
                HeightRequest = 4,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.End,
                Margin = new Thickness(0, 0, 0, 4)
            };
            
            // Fix: Use SolidColorBrush instead of Color for Fill property
            var lightBrush = new SolidColorBrush((Color)Application.Current.Resources["PrimaryColor"]);
            var darkBrush = new SolidColorBrush((Color)Application.Current.Resources["GoldPure"]);
            indicator.SetAppTheme(Ellipse.FillProperty, lightBrush, darkBrush);
            
            indicator.SetBinding(Ellipse.IsVisibleProperty, new Binding(nameof(CalendarDay.HasData), source: day));
            contentGrid.Add(indicator);

            border.Content = contentGrid;

            // 🎨 PHASE 20.2B: Enhanced tap gesture with subtle animation
            // 🎨 PHASE 20.2C: Added fade-in for selected day card
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) =>
            {
                if (_viewModel != null && s is Border tappedBorder)
                {
                    // Subtle scale animation on tap (Material Design ripple effect)
                    await tappedBorder.ScaleTo(0.92, 80, Easing.CubicOut);
                    await tappedBorder.ScaleTo(1.0, 120, Easing.CubicOut);
                    
                    // Execute selection
                    _viewModel.SelectDayCommand.Execute(day.Date);
                    
                    // Animate selected day card appearance
                    if (SelectedDayCard != null && SelectedDayCard.IsVisible)
                    {
                        await AnimateSelectedDayCardAsync();
                    }
                }
            };
            border.GestureRecognizers.Add(tapGesture);

            // Add to grid
            Grid.SetRow(border, row);
            Grid.SetColumn(border, col);
            CalendarGrid.Add(border);

            // Move to next cell
            col++;
            if (col >= 7)
            {
                col = 0;
                row++;
            }
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
