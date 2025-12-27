using DaxStudio.UI.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.VisualQueryPlan
{
    /// <summary>
    /// Unit tests for ZoomHelper zoom-to-cursor calculations.
    /// These tests prevent regression in the zoom behavior that caused diagonal movement.
    /// </summary>
    [TestClass]
    public class ZoomHelperTests
    {
        #region Zoom-to-Cursor Scroll Offset Calculation Tests

        [TestMethod]
        public void CalculateZoomScrollOffsets_ZoomIn_KeepsPointUnderCursor()
        {
            // Arrange - Mouse at center of 800x600 viewport, currently scrolled to 100,100 at 1.0 zoom
            double mouseX = 400, mouseY = 300;
            double currentHOffset = 100, currentVOffset = 100;
            double oldZoom = 1.0, newZoom = 1.5;
            double maxScrollWidth = 2000, maxScrollHeight = 2000;

            // Act
            var (newHOffset, newVOffset) = ZoomHelper.CalculateZoomScrollOffsets(
                mouseX, mouseY,
                currentHOffset, currentVOffset,
                oldZoom, newZoom,
                maxScrollWidth, maxScrollHeight);

            // Assert - The content point under the cursor should stay in the same screen position
            // Content point was at (100+400)/1.0 = 500, (100+300)/1.0 = 400
            // After zoom: (500*1.5) - 400 = 350, (400*1.5) - 300 = 300
            Assert.AreEqual(350.0, newHOffset, 0.001, "Horizontal offset should keep content point under cursor");
            Assert.AreEqual(300.0, newVOffset, 0.001, "Vertical offset should keep content point under cursor");
        }

        [TestMethod]
        public void CalculateZoomScrollOffsets_ZoomOut_KeepsPointUnderCursor()
        {
            // Arrange - Mouse at 400,300 in viewport, scrolled to 200,200 at 1.5 zoom
            double mouseX = 400, mouseY = 300;
            double currentHOffset = 200, currentVOffset = 200;
            double oldZoom = 1.5, newZoom = 1.0;
            double maxScrollWidth = 2000, maxScrollHeight = 2000;

            // Act
            var (newHOffset, newVOffset) = ZoomHelper.CalculateZoomScrollOffsets(
                mouseX, mouseY,
                currentHOffset, currentVOffset,
                oldZoom, newZoom,
                maxScrollWidth, maxScrollHeight);

            // Assert - Content point was at (200+400)/1.5 = 400, (200+300)/1.5 = 333.33
            // After zoom: (400*1.0) - 400 = 0, (333.33*1.0) - 300 = 33.33
            Assert.AreEqual(0.0, newHOffset, 0.001, "Horizontal offset should stay at 0 when zooming out");
            Assert.AreEqual(33.333, newVOffset, 0.01, "Vertical offset should be adjusted correctly");
        }

        [TestMethod]
        public void CalculateZoomScrollOffsets_MouseAtOrigin_KeepsTopLeftFixed()
        {
            // Arrange - Mouse at top-left corner (0,0), no scroll offset
            double mouseX = 0, mouseY = 0;
            double currentHOffset = 0, currentVOffset = 0;
            double oldZoom = 1.0, newZoom = 2.0;
            double maxScrollWidth = 2000, maxScrollHeight = 2000;

            // Act
            var (newHOffset, newVOffset) = ZoomHelper.CalculateZoomScrollOffsets(
                mouseX, mouseY,
                currentHOffset, currentVOffset,
                oldZoom, newZoom,
                maxScrollWidth, maxScrollHeight);

            // Assert - When zooming from top-left, scroll should remain at 0,0
            Assert.AreEqual(0.0, newHOffset, 0.001, "Horizontal offset should stay at 0");
            Assert.AreEqual(0.0, newVOffset, 0.001, "Vertical offset should stay at 0");
        }

        [TestMethod]
        public void CalculateZoomScrollOffsets_ClampsToMinimum()
        {
            // Arrange - Scenario that would produce negative scroll offsets
            double mouseX = 500, mouseY = 500;
            double currentHOffset = 0, currentVOffset = 0;
            double oldZoom = 2.0, newZoom = 0.5;
            double maxScrollWidth = 2000, maxScrollHeight = 2000;

            // Act
            var (newHOffset, newVOffset) = ZoomHelper.CalculateZoomScrollOffsets(
                mouseX, mouseY,
                currentHOffset, currentVOffset,
                oldZoom, newZoom,
                maxScrollWidth, maxScrollHeight);

            // Assert - Negative values should be clamped to 0
            Assert.IsTrue(newHOffset >= 0, "Horizontal offset should not be negative");
            Assert.IsTrue(newVOffset >= 0, "Vertical offset should not be negative");
        }

        [TestMethod]
        public void CalculateZoomScrollOffsets_ClampsToMaximum()
        {
            // Arrange - Scenario that would exceed max scroll
            double mouseX = 100, mouseY = 100;
            double currentHOffset = 1800, currentVOffset = 1800;
            double oldZoom = 1.0, newZoom = 3.0;
            double maxScrollWidth = 2000, maxScrollHeight = 2000;

            // Act
            var (newHOffset, newVOffset) = ZoomHelper.CalculateZoomScrollOffsets(
                mouseX, mouseY,
                currentHOffset, currentVOffset,
                oldZoom, newZoom,
                maxScrollWidth, maxScrollHeight);

            // Assert - Values should not exceed the maximum
            Assert.IsTrue(newHOffset <= maxScrollWidth, "Horizontal offset should not exceed max scrollable width");
            Assert.IsTrue(newVOffset <= maxScrollHeight, "Vertical offset should not exceed max scrollable height");
        }

        [TestMethod]
        public void CalculateZoomScrollOffsets_NoZoomChange_KeepsSameOffsets()
        {
            // Arrange - Same zoom level (edge case)
            double mouseX = 400, mouseY = 300;
            double currentHOffset = 100, currentVOffset = 100;
            double oldZoom = 1.0, newZoom = 1.0;
            double maxScrollWidth = 2000, maxScrollHeight = 2000;

            // Act
            var (newHOffset, newVOffset) = ZoomHelper.CalculateZoomScrollOffsets(
                mouseX, mouseY,
                currentHOffset, currentVOffset,
                oldZoom, newZoom,
                maxScrollWidth, maxScrollHeight);

            // Assert - No zoom change means same scroll offsets
            Assert.AreEqual(100.0, newHOffset, 0.001, "Horizontal offset should remain unchanged");
            Assert.AreEqual(100.0, newVOffset, 0.001, "Vertical offset should remain unchanged");
        }

        [TestMethod]
        public void CalculateZoomScrollOffsets_PreventsDiagonalDrift()
        {
            // Arrange - Simulate the original diagonal drift bug scenario
            // User at center, zooming in repeatedly should keep the same point under cursor
            double mouseX = 400, mouseY = 300;
            double currentHOffset = 0, currentVOffset = 0;
            double maxScrollWidth = 5000, maxScrollHeight = 5000;

            // Simulate multiple zoom steps
            double zoom = 1.0;
            double hOffset = 0, vOffset = 0;

            for (int i = 0; i < 5; i++)
            {
                double oldZoom = zoom;
                zoom += 0.1; // Zoom in

                var (newH, newV) = ZoomHelper.CalculateZoomScrollOffsets(
                    mouseX, mouseY,
                    hOffset, vOffset,
                    oldZoom, zoom,
                    maxScrollWidth, maxScrollHeight);

                // Calculate content point under cursor before and after
                double contentXBefore = (hOffset + mouseX) / oldZoom;
                double contentYBefore = (vOffset + mouseY) / oldZoom;
                double contentXAfter = (newH + mouseX) / zoom;
                double contentYAfter = (newV + mouseY) / zoom;

                // Assert - Content point should remain the same
                Assert.AreEqual(contentXBefore, contentXAfter, 0.001,
                    $"Iteration {i}: Horizontal content point should remain stable");
                Assert.AreEqual(contentYBefore, contentYAfter, 0.001,
                    $"Iteration {i}: Vertical content point should remain stable");

                hOffset = newH;
                vOffset = newV;
            }
        }

        #endregion
    }
}
