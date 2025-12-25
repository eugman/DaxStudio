using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using DaxStudio.UI.Controls;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Views
{
    /// <summary>
    /// Interaction logic for VisualQueryPlanView.xaml
    /// </summary>
    public partial class VisualQueryPlanView : ZoomableUserControl
    {
        private bool _isPanning;
        private Point _panStartPoint;
        private double _panStartHorizontalOffset;
        private double _panStartVerticalOffset;

        public VisualQueryPlanView()
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;

            // Attach mouse events for canvas panning
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (PlanScrollViewer != null)
            {
                PlanScrollViewer.PreviewMouseLeftButtonDown += ScrollViewer_PreviewMouseLeftButtonDown;
                PlanScrollViewer.PreviewMouseMove += ScrollViewer_PreviewMouseMove;
                PlanScrollViewer.PreviewMouseLeftButtonUp += ScrollViewer_PreviewMouseLeftButtonUp;
                PlanScrollViewer.MouseLeave += ScrollViewer_MouseLeave;
            }
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (PlanScrollViewer != null)
            {
                PlanScrollViewer.PreviewMouseLeftButtonDown -= ScrollViewer_PreviewMouseLeftButtonDown;
                PlanScrollViewer.PreviewMouseMove -= ScrollViewer_PreviewMouseMove;
                PlanScrollViewer.PreviewMouseLeftButtonUp -= ScrollViewer_PreviewMouseLeftButtonUp;
                PlanScrollViewer.MouseLeave -= ScrollViewer_MouseLeave;
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Only start panning if clicking on the canvas background (not on a node)
            // Check if the original source is the Canvas or ScrollViewer background
            var originalSource = e.OriginalSource as FrameworkElement;

            // If clicking directly on Canvas or ScrollContentPresenter, start panning
            if (originalSource is System.Windows.Controls.Canvas ||
                originalSource is System.Windows.Controls.ScrollContentPresenter ||
                (originalSource != null && originalSource.Name == "PlanCanvas"))
            {
                _panStartPoint = e.GetPosition(PlanScrollViewer);
                _panStartHorizontalOffset = PlanScrollViewer.HorizontalOffset;
                _panStartVerticalOffset = PlanScrollViewer.VerticalOffset;
                _isPanning = false; // Will become true on mouse move

                PlanScrollViewer.CaptureMouse();
                PlanScrollViewer.Cursor = Cursors.Hand;
            }
        }

        private void ScrollViewer_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!PlanScrollViewer.IsMouseCaptured || e.LeftButton != MouseButtonState.Pressed)
                return;

            var currentPoint = e.GetPosition(PlanScrollViewer);
            var deltaX = currentPoint.X - _panStartPoint.X;
            var deltaY = currentPoint.Y - _panStartPoint.Y;

            // Start panning only after moving a minimum distance (3 pixels)
            if (!_isPanning && (Math.Abs(deltaX) > 3 || Math.Abs(deltaY) > 3))
            {
                _isPanning = true;
                PlanScrollViewer.Cursor = Cursors.ScrollAll;
            }

            if (_isPanning)
            {
                // Scroll in opposite direction of mouse movement (drag-to-pan)
                PlanScrollViewer.ScrollToHorizontalOffset(_panStartHorizontalOffset - deltaX);
                PlanScrollViewer.ScrollToVerticalOffset(_panStartVerticalOffset - deltaY);
                e.Handled = true;
            }
        }

        private void ScrollViewer_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (PlanScrollViewer.IsMouseCaptured)
            {
                PlanScrollViewer.ReleaseMouseCapture();
                PlanScrollViewer.Cursor = Cursors.Arrow;

                // If it was a pan operation, mark as handled to prevent node selection
                if (_isPanning)
                {
                    e.Handled = true;
                }

                _isPanning = false;
            }
        }

        private void ScrollViewer_MouseLeave(object sender, MouseEventArgs e)
        {
            // Release capture if mouse leaves during pan operation or before pan threshold was reached
            if (PlanScrollViewer.IsMouseCaptured)
            {
                PlanScrollViewer.ReleaseMouseCapture();
                PlanScrollViewer.Cursor = Cursors.Arrow;
                _isPanning = false;
            }
        }

        private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
        {
            // Unsubscribe from old ViewModel
            if (e.OldValue is VisualQueryPlanViewModel oldVm)
            {
                oldVm.PlanLayoutUpdated -= OnPlanLayoutUpdated;
                oldVm.NodeScrollRequested -= OnNodeScrollRequested;
            }

            // Subscribe to new ViewModel
            if (e.NewValue is VisualQueryPlanViewModel newVm)
            {
                newVm.PlanLayoutUpdated += OnPlanLayoutUpdated;
                newVm.NodeScrollRequested += OnNodeScrollRequested;
            }
        }

        private void OnNodeScrollRequested(object sender, NodeScrollEventArgs e)
        {
            if (e?.Node == null || PlanScrollViewer == null) return;

            // Get the node's position and center it in the viewport
            var vm = DataContext as VisualQueryPlanViewModel;
            if (vm == null) return;

            // Account for zoom level
            var zoomLevel = vm.ZoomLevel;
            var nodeX = e.Node.X * zoomLevel;
            var nodeY = e.Node.Y * zoomLevel;
            var nodeWidth = e.Node.Width * zoomLevel;
            var nodeHeight = e.Node.Height * zoomLevel;

            // Calculate scroll position to center the node in the viewport
            var targetHorizontal = nodeX + (nodeWidth / 2) - (PlanScrollViewer.ViewportWidth / 2);
            var targetVertical = nodeY + (nodeHeight / 2) - (PlanScrollViewer.ViewportHeight / 2);

            // Clamp to valid scroll range
            targetHorizontal = Math.Max(0, Math.Min(targetHorizontal, PlanScrollViewer.ScrollableWidth));
            targetVertical = Math.Max(0, Math.Min(targetVertical, PlanScrollViewer.ScrollableHeight));

            // Scroll to the position
            PlanScrollViewer.ScrollToHorizontalOffset(targetHorizontal);
            PlanScrollViewer.ScrollToVerticalOffset(targetVertical);
        }

        private void OnPlanLayoutUpdated(object sender, EventArgs e)
        {
            // Snap scroll position if we're scrolled past the actual content bounds
            // Use Dispatcher to ensure layout is complete before checking
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (PlanScrollViewer == null) return;

                // Get actual content bounds from ViewModel (not Canvas fixed size)
                var vm = DataContext as VisualQueryPlanViewModel;
                if (vm == null) return;

                var actualContentHeight = vm.ActualContentHeight;
                var actualContentWidth = vm.ActualContentWidth;

                // Calculate max scroll offset based on actual content, not fixed Canvas size
                var maxVerticalOffset = Math.Max(0, actualContentHeight - PlanScrollViewer.ViewportHeight);
                if (PlanScrollViewer.VerticalOffset > maxVerticalOffset)
                {
                    PlanScrollViewer.ScrollToVerticalOffset(maxVerticalOffset);
                }

                var maxHorizontalOffset = Math.Max(0, actualContentWidth - PlanScrollViewer.ViewportWidth);
                if (PlanScrollViewer.HorizontalOffset > maxHorizontalOffset)
                {
                    PlanScrollViewer.ScrollToHorizontalOffset(maxHorizontalOffset);
                }
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Opens hyperlinks in the default browser.
        /// </summary>
        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}
