using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DaxStudio.UI.Utils
{
    public static class ZoomHelper
    {
        public static void PreviewMouseWheel(System.Windows.Controls.UserControl sender, System.Windows.Input.MouseWheelEventArgs args)
        {
            if (System.Windows.Input.Keyboard.Modifiers == System.Windows.Input.ModifierKeys.Control)
            {
                var factor = 1.0 + System.Math.Abs(((double)args.Delta) / 1200.0);
                var scale = args.Delta >= 0 ? factor : (1.0 / factor); // choose appropriate scaling factor
                var scaler = sender.LayoutTransform as System.Windows.Media.ScaleTransform;
                if (scaler == null)
                {
                    sender.LayoutTransform = new System.Windows.Media.ScaleTransform(scale, scale);
                }
                else
                {
                    scaler.ScaleX *= scale;
                    scaler.ScaleY *= scale;
                }
                args.Handled = true;
            }
        }

        /// <summary>
        /// Calculates new scroll offsets to keep a specific content point under the cursor after zooming.
        /// This enables "zoom-to-cursor" behavior where the point under the mouse stays fixed during zoom.
        /// </summary>
        /// <param name="mouseX">Mouse X position relative to the ScrollViewer viewport.</param>
        /// <param name="mouseY">Mouse Y position relative to the ScrollViewer viewport.</param>
        /// <param name="currentHorizontalOffset">Current horizontal scroll offset.</param>
        /// <param name="currentVerticalOffset">Current vertical scroll offset.</param>
        /// <param name="oldZoom">Zoom level before the zoom operation.</param>
        /// <param name="newZoom">Zoom level after the zoom operation.</param>
        /// <param name="maxScrollableWidth">Maximum scrollable width (for clamping).</param>
        /// <param name="maxScrollableHeight">Maximum scrollable height (for clamping).</param>
        /// <returns>Tuple of (newHorizontalOffset, newVerticalOffset).</returns>
        public static (double newHorizontalOffset, double newVerticalOffset) CalculateZoomScrollOffsets(
            double mouseX,
            double mouseY,
            double currentHorizontalOffset,
            double currentVerticalOffset,
            double oldZoom,
            double newZoom,
            double maxScrollableWidth,
            double maxScrollableHeight)
        {
            // Calculate the content point under the mouse cursor before zoom
            var contentX = (currentHorizontalOffset + mouseX) / oldZoom;
            var contentY = (currentVerticalOffset + mouseY) / oldZoom;

            // Calculate new scroll position to keep the same content point under the cursor
            var newScrollX = (contentX * newZoom) - mouseX;
            var newScrollY = (contentY * newZoom) - mouseY;

            // Clamp to valid scroll range
            newScrollX = Math.Max(0, Math.Min(newScrollX, maxScrollableWidth));
            newScrollY = Math.Max(0, Math.Min(newScrollY, maxScrollableHeight));

            return (newScrollX, newScrollY);
        }
    }
}
