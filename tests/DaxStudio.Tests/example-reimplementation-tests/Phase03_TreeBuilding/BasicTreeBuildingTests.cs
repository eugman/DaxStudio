using System.Collections.Generic;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ExampleReimplementation.Phase03_TreeBuilding
{
    /// <summary>
    /// Phase 3: Basic Tree Building
    ///
    /// These tests verify the basic tree construction from EnrichedQueryPlan to PlanNodeViewModel tree.
    /// At this phase, folding is minimal - just column references with ScaLogOp/RelLogOp.
    ///
    /// Prerequisites: Phase 1-2 complete, PlanNodeViewModel.BuildTree() method
    ///
    /// Reference: docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md - Phase 3
    /// </summary>
    [TestClass]
    public class BasicTreeBuildingTests
    {
        #region Basic Tree Construction

        [TestMethod]
        public void BuildTree_SingleNode_ReturnsRoot()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Sum_Vertipaq: ScaLogOp MeasureRef=[Total]"
                    }
                }
            };
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count);
            Assert.AreEqual("Sum_Vertipaq", root.OperatorName);
        }

        [TestMethod]
        public void BuildTree_ParentChild_PreservesRelationship()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "AddColumns: RelLogOp"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "Filter: RelLogOp DependOnCols()()"
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "Regular operators should NOT be folded");
            Assert.AreEqual("Filter", root.Children[0].OperatorName);
            Assert.AreEqual(root, root.Children[0].Parent);
        }

        [TestMethod]
        public void BuildTree_ThreeLevels_PreservesHierarchy()
        {
            // Arrange - grandparent -> parent -> child
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "AddColumns: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Filter: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "Scan_Vertipaq: RelLogOp" }
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
            Assert.IsNotNull(root);
            Assert.AreEqual("AddColumns", root.OperatorName);
            Assert.AreEqual(1, root.Children.Count);
            Assert.AreEqual("Filter", root.Children[0].OperatorName);
            Assert.AreEqual(1, root.Children[0].Children.Count);
            Assert.AreEqual("Scan_Vertipaq", root.Children[0].Children[0].OperatorName);
        }

        [TestMethod]
        public void BuildTree_MultipleChildren_AllPreserved()
        {
            // Arrange - parent with multiple children
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
            Assert.AreEqual(2, root.Children.Count, "Both children should be preserved");
        }

        #endregion

        #region Column Reference Folding (Basic)

        [TestMethod]
        public void BuildTree_ColumnReferenceWithScaLogOp_IsFolded()
        {
            // Arrange - column reference node with ScaLogOp should be folded
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Sum_Vertipaq: ScaLogOp MeasureRef=[Total]"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "'Internet Sales'[Sales Amount]: ScaLogOp DependOnCols(106)('Internet Sales'[Sales Amount]) Currency"
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - column reference node should be folded (not in children)
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "Column reference with ScaLogOp should be folded");
        }

        [TestMethod]
        public void BuildTree_ColumnReferenceWithRelLogOp_IsFolded()
        {
            // Arrange - column reference node with RelLogOp should also be folded
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Filter: RelLogOp"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "'Customer'[Region]: RelLogOp DependOnCols(1)('Customer'[Region])"
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - column reference with RelLogOp should be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "Column reference with RelLogOp should be folded");
        }

        [TestMethod]
        public void BuildTree_ScanVertipaq_IsNotFolded()
        {
            // Arrange - Scan_Vertipaq should NOT be folded (it's an important SE operator)
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Sum_Vertipaq: ScaLogOp MeasureRef=[Total]"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "Scan_Vertipaq: RelLogOp RequiredCols(106)('Internet Sales'[Sales Amount])"
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Scan_Vertipaq should NOT be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "Scan_Vertipaq should NOT be folded");
            Assert.IsTrue(root.Children[0].Operation.Contains("Scan_Vertipaq"));
        }

        #endregion

        #region Child Promotion (When Parent Folded)

        [TestMethod]
        public void BuildTree_FoldedNodeChildren_ArePromotedToGrandparent()
        {
            // Arrange - when a node is folded, its children should be promoted
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Sum_Vertipaq: ScaLogOp"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "'Sales'[Amount]: ScaLogOp" // This will be folded
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Level = 2,
                        Operation = "Scan_Vertipaq: RelLogOp" // This is child of folded node
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[1].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Scan_Vertipaq should become direct child of Sum_Vertipaq
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "Grandchild should be promoted when parent is folded");
            Assert.IsTrue(root.Children[0].Operation.Contains("Scan_Vertipaq"),
                "Scan_Vertipaq should be promoted to root's children");
        }

        #endregion

        #region Null/Empty Handling

        [TestMethod]
        public void BuildTree_NullPlan_ReturnsNull()
        {
            // Act
            var root = PlanNodeViewModel.BuildTree(null);

            // Assert
            Assert.IsNull(root);
        }

        [TestMethod]
        public void BuildTree_EmptyAllNodes_ReturnsNull()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>()
            };

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNull(root);
        }

        #endregion
    }
}
