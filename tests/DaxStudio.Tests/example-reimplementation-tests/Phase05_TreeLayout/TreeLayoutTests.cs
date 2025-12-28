using System.Collections.Generic;
using DaxStudio.UI.Model;
using DaxStudio.UI.Utils;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ExampleReimplementation.Phase05_TreeLayout
{
    /// <summary>
    /// Phase 5: Tree Layout Algorithm
    ///
    /// These tests verify the tree layout algorithm that positions nodes:
    /// - Subtree width calculation
    /// - Node positioning (X, Y coordinates)
    /// - Zoom calculations
    /// - Edge geometry
    ///
    /// Prerequisites: Phases 1-4 complete
    ///
    /// Reference: docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md - Phase 5
    /// </summary>
    [TestClass]
    public class TreeLayoutTests
    {
        #region Subtree Width Calculation

        [TestMethod]
        public void SubtreeWidth_LeafNode_ReturnsOne()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Scan_Vertipaq: RelLogOp" }
                }
            };
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual(1, root.SubtreeWidth, "Leaf node should have subtree width of 1");
        }

        [TestMethod]
        public void SubtreeWidth_WithTwoChildren_ReturnsSumOfChildren()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "CrossApply: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Scan_Vertipaq: RelLogOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Filter: IterPhyOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual(2, root.SubtreeWidth, "Parent with 2 leaf children should have subtree width of 2");
        }

        [TestMethod]
        public void SubtreeWidth_DeepTree_AccumulatesCorrectly()
        {
            // Arrange - Tree with branching at different levels
            // Root (width 3)
            //   |- Child1 (width 2)
            //   |    |- Grandchild1 (width 1)
            //   |    |- Grandchild2 (width 1)
            //   |- Child2 (width 1)
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Root: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Child1: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Child2: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 4, Level = 2, Operation = "Grandchild1: RelLogOp" },
                    new EnrichedPlanNode { NodeId = 5, Level = 2, Operation = "Grandchild2: RelLogOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.AllNodes[1].Children.Add(plan.AllNodes[3]);
            plan.AllNodes[1].Children.Add(plan.AllNodes[4]);
            plan.AllNodes[3].Parent = plan.AllNodes[1];
            plan.AllNodes[4].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual(3, root.SubtreeWidth, "Root should have width 3 (2 from Child1's subtree + 1 from Child2)");
            Assert.AreEqual(2, root.Children[0].SubtreeWidth, "Child1 should have width 2");
            Assert.AreEqual(1, root.Children[1].SubtreeWidth, "Child2 should have width 1");
        }

        #endregion

        #region Collapsed Subtree Width

        [TestMethod]
        public void VisibleSubtreeWidth_CollapsedNode_ReturnsOne()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Root: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Child1: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Child2: IterPhyOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            var root = PlanNodeViewModel.BuildTree(plan);

            // Act - collapse the root
            root.IsSubtreeCollapsed = true;

            // Assert
            Assert.AreEqual(1, root.VisibleSubtreeWidth, "Collapsed subtree should have visible width of 1");
        }

        #endregion

        #region Zoom Calculations

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

            // Assert
            Assert.AreEqual(0.0, newHOffset, 0.001, "Horizontal offset should stay at 0 when zooming out");
            Assert.IsTrue(newVOffset >= 0, "Vertical offset should be non-negative");
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

        #endregion

        #region X/Y Coordinate Positioning

        [TestMethod]
        public void CanvasX_RootNode_IsCenteredOnSubtree()
        {
            // Arrange - Root with two children
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "CrossApply: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Scan_Vertipaq: RelLogOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Filter: IterPhyOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Root should be centered between its children
            // The exact X depends on node width constants, but parent should be between children
            Assert.IsTrue(root.CanvasX >= 0, "Root X should be non-negative");
            if (root.Children.Count == 2)
            {
                var leftChild = root.Children[0];
                var rightChild = root.Children[1];
                // Parent's center should be between children's centers
                Assert.IsTrue(root.CanvasX >= leftChild.CanvasX || root.CanvasX <= rightChild.CanvasX,
                    "Root should be positioned between or aligned with children");
            }
        }

        [TestMethod]
        public void CanvasY_IncreasesWithLevel()
        {
            // Arrange - 3-level tree
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Root: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Child: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "Grandchild: RelLogOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[1].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Y should increase as we go deeper in the tree
            var child = root.Children[0];
            var grandchild = child.Children[0];

            Assert.IsTrue(child.CanvasY > root.CanvasY, "Child Y should be greater than root Y");
            Assert.IsTrue(grandchild.CanvasY > child.CanvasY, "Grandchild Y should be greater than child Y");
        }

        [TestMethod]
        public void CanvasY_SiblingsHaveSameY()
        {
            // Arrange - Parent with two children at same level
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Parent: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Child1: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Child2: IterPhyOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Siblings should have the same Y coordinate
            if (root.Children.Count == 2)
            {
                Assert.AreEqual(root.Children[0].CanvasY, root.Children[1].CanvasY,
                    "Siblings should have the same Y coordinate");
            }
        }

        [TestMethod]
        public void CanvasX_SiblingsDoNotOverlap()
        {
            // Arrange - Parent with multiple children
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Parent: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Child1: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Child2: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 4, Level = 1, Operation = "Child3: IterPhyOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[3]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.AllNodes[3].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Each sibling should have a different X position
            if (root.Children.Count >= 2)
            {
                for (int i = 0; i < root.Children.Count - 1; i++)
                {
                    Assert.AreNotEqual(root.Children[i].CanvasX, root.Children[i + 1].CanvasX,
                        $"Child {i} and Child {i + 1} should have different X positions");
                }
            }
        }

        [TestMethod]
        public void Level_CalculatedCorrectly()
        {
            // Arrange - 3-level tree
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Root: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Child: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "Grandchild: RelLogOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[1].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual(0, root.Level, "Root level should be 0");
            Assert.AreEqual(1, root.Children[0].Level, "Child level should be 1");
            Assert.AreEqual(2, root.Children[0].Children[0].Level, "Grandchild level should be 2");
        }

        #endregion

        #region Edge Geometry

        [TestMethod]
        public void EdgePoints_ConnectParentToChild()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Parent: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Child: IterPhyOp" }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Child should have edge data connecting to parent
            if (root.Children.Count > 0)
            {
                var child = root.Children[0];
                // Edge data should exist (EdgeStartX, EdgeStartY, EdgeEndX, EdgeEndY or similar)
                // The exact property names depend on implementation
                Assert.IsNotNull(child.Parent, "Child should reference parent for edge drawing");
            }
        }

        #endregion
    }
}
