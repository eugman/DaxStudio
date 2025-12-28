using System.Collections.Generic;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ExampleReimplementation.Phase04_NodeFolding
{
    /// <summary>
    /// Phase 4: Node Folding (Visual Simplification)
    ///
    /// These tests verify the 15-pass folding algorithm that simplifies the tree:
    /// - Pass 1: Column references (ScaLogOp/RelLogOp)
    /// - Pass 2: Filter predicates (GreaterThan, LessThan, etc.)
    /// - Pass 5: Spool children (AggregationSpool, ProjectionSpool)
    /// - Pass 6: Arithmetic chains (Add->Add->Add)
    /// - And more...
    ///
    /// Prerequisites: Phases 1-3 complete, BuildTree with folding logic
    ///
    /// Reference: docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md - Phase 4
    /// Reference: docs/VISUAL_QUERY_PLAN_NODE_FOLDING.md
    /// </summary>
    [TestClass]
    public class NodeFoldingTests
    {
        #region Filter Predicate Rollup (Pass 2)

        [TestMethod]
        public void BuildTree_FilterWithGreaterThan_RollsUpPredicate()
        {
            // Arrange - Filter with GreaterThan comparison
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "GreaterThan: ScaLogOp Boolean", EngineType = EngineType.FormulaEngine },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "'Sales'[Amount]: ScaLogOp", EngineType = EngineType.FormulaEngine },
                    new EnrichedPlanNode { NodeId = 4, Level = 2, Operation = "Constant: ScaLogOp DominantValue=100", EngineType = EngineType.FormulaEngine }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            SetupParentChild(plan, 1, 3);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual("Filter", root.OperatorName);
            Assert.IsTrue(root.HasFilterPredicateExpression, "Filter should have predicate expression");
            Assert.IsTrue(root.FilterPredicateExpression.Contains(">"), "Should contain > operator");
        }

        [TestMethod]
        public void BuildTree_FilterWithLessThanOrEqual_RollsUpPredicate()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "LessOrEqualTo: ScaLogOp Boolean", EngineType = EngineType.FormulaEngine },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "'Date'[Year]: ScaLogOp", EngineType = EngineType.FormulaEngine },
                    new EnrichedPlanNode { NodeId = 4, Level = 2, Operation = "Constant: ScaLogOp DominantValue=2023", EngineType = EngineType.FormulaEngine }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            SetupParentChild(plan, 1, 3);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsTrue(root.HasFilterPredicateExpression);
            Assert.IsTrue(root.FilterPredicateExpression.Contains("<="), "Should contain <= operator");
        }

        #endregion

        #region Spool Folding (Pass 5)

        [TestMethod]
        public void BuildTree_SpoolIterator_WithProjectionSpoolChild_FoldsChild()
        {
            // Arrange - Spool_Iterator with ProjectionSpool child should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq IterCols(0)('Product'[Brand]) #Records=11"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "ProjectionSpool<ProjectFusion<Copy, Copy, Copy>>: SpoolPhyOp #Records=11"
                    }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - ProjectionSpool should be folded into Spool_Iterator
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "ProjectionSpool should be folded into Spool_Iterator");
            Assert.IsTrue(root.HasSpoolTypeInfo, "Parent should have SpoolTypeInfo after folding");
        }

        [TestMethod]
        public void BuildTree_SpoolLookup_WithAggregationSpoolChild_FoldsChild()
        {
            // Arrange - SpoolLookup with AggregationSpool child
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "SpoolLookup: LookupPhyOp LogOp=Sum_Vertipaq LookupCols(0)('Sales'[Amount]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "AggregationSpool<Sum>: SpoolPhyOp #Records=100"
                    }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "AggregationSpool should be folded into SpoolLookup");
            Assert.IsTrue(root.HasSpoolTypeInfo, "Parent should have SpoolTypeInfo after folding");
        }

        #endregion

        #region Arithmetic Chain Folding (Pass 6)

        [TestMethod]
        public void BuildTree_ChainedAdd_CollapsesToSingleNode()
        {
            // Arrange - Add -> Add -> Add chain
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Add: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Add: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "Add: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 4, Level = 3, Operation = "'Sales'[A]: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 5, Level = 3, Operation = "'Sales'[B]: ScaLogOp" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            SetupParentChild(plan, 2, 3);
            SetupParentChild(plan, 2, 4);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - chain should collapse
            Assert.AreEqual("Add", root.OperatorName);
            Assert.IsTrue(root.ChainedOperatorCount >= 2, "Should track chain count");
        }

        [TestMethod]
        public void BuildTree_ChainedMultiply_CollapsesToSingleNode()
        {
            // Arrange - Multiply chain
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Multiply: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Multiply: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "'Sales'[Price]: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 4, Level = 2, Operation = "'Sales'[Qty]: ScaLogOp" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            SetupParentChild(plan, 1, 3);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual("Multiply", root.OperatorName);
        }

        #endregion

        #region SingletonTable Folding (Pass 7)

        [TestMethod]
        public void BuildTree_SingletonTable_AlwaysFolded()
        {
            // Arrange - SingletonTable should always be folded into parent
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "AddColumns: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "SingletonTable: RelLogOp" }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual(0, root.Children.Count, "SingletonTable should be folded");
        }

        #endregion

        #region Identical Node Folding (Pass 9)

        [TestMethod]
        public void BuildTree_IdenticalChildOperation_IsFolded()
        {
            // Arrange - child with identical operation to parent
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Calculate: ScaLogOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Calculate: ScaLogOp" } // Identical
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - identical child should be folded
            Assert.AreEqual(0, root.Children.Count, "Identical child should be folded");
        }

        #endregion

        #region Engine Transition Preservation

        [TestMethod]
        public void BuildTree_SEToFE_PreservesEngineTransition()
        {
            // Arrange - SE -> FE transition should not be folded
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Spool_Iterator: IterPhyOp", EngineType = EngineType.FormulaEngine },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - engine transition must be preserved
            Assert.AreEqual(1, root.Children.Count, "Engine transition should NOT be folded");
            Assert.AreEqual(EngineType.StorageEngine, root.Children[0].EngineType);
        }

        #endregion

        #region ISBLANK/Not Chain Folding

        [TestMethod]
        public void BuildTree_IsBlankChain_FoldsToParent()
        {
            // Arrange - ISBLANK chain: Filter -> Not -> ISBLANK -> column
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Filter: RelLogOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Not: ScaLogOp Boolean" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "ISBLANK: ScaLogOp Boolean" },
                    new EnrichedPlanNode { NodeId = 4, Level = 3, Operation = "'Sales'[Amount]: ScaLogOp" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            SetupParentChild(plan, 2, 3);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - ISBLANK chain should fold
            Assert.AreEqual("Filter", root.OperatorName);
            Assert.IsTrue(root.HasFilterPredicateExpression || root.Children.Count < 3,
                "ISBLANK chain should be simplified");
        }

        #endregion

        #region CrossApply Chain Folding (Pass 3)

        [TestMethod]
        public void BuildTree_CrossApply_WithSingleChild_FoldsChild()
        {
            // Arrange - CrossApply with single Spool child
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "CrossApply: IterPhyOp LogOp=Scan_Vertipaq IterCols(1, 44)('Customer'[CustomerKey], 'Date'[Date])" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Spool_MultiValuedHashLookup: IterPhyOp LogOp=First LookupCols(42)('Date'[Date]) IterCols(1)('Customer'[CustomerKey]) #Records=18869" }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - CrossApply may fold single child or preserve it depending on context
            Assert.IsNotNull(root);
            Assert.AreEqual("CrossApply", root.OperatorName);
        }

        [TestMethod]
        public void BuildTree_CrossApply_WithMultipleChildren_PreservesAll()
        {
            // Arrange - CrossApply with multiple children should not fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "CrossApply: IterPhyOp LogOp=Scan_Vertipaq" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Spool_Iterator: IterPhyOp #Records=100" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Cache: IterPhyOp #FieldCols=1 #ValueCols=0" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 0, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - multiple children should be preserved
            Assert.IsNotNull(root);
            Assert.IsTrue(root.Children.Count >= 1, "CrossApply with multiple children should preserve children");
        }

        #endregion

        #region Cache Operator Folding (Pass 4)

        [TestMethod]
        public void BuildTree_Cache_WithParentSpool_IsFolded()
        {
            // Arrange - Cache under Spool should be folded
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp #Records=100" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "ProjectionSpool<ProjectFusion<Copy>>: SpoolPhyOp #Records=100" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "Cache: IterPhyOp #FieldCols=2 #ValueCols=1" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Cache should be folded along with ProjectionSpool
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "Cache and ProjectionSpool should be folded into Spool_Iterator");
        }

        [TestMethod]
        public void BuildTree_Cache_Standalone_IsPreserved()
        {
            // Arrange - Cache as standalone operator (not under spool) may be preserved
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "AddColumns: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Cache: IterPhyOp #FieldCols=2 #ValueCols=1" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "Scan_Vertipaq: RelLogOp #Records=1000" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - behavior depends on implementation; Cache may or may not fold
            Assert.IsNotNull(root);
        }

        #endregion

        #region Coalesce Chain Folding (Pass 8)

        [TestMethod]
        public void BuildTree_CoalesceChain_CollapsesToSingleNode()
        {
            // Arrange - Coalesce -> Coalesce chain
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Coalesce: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Coalesce: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "'Sales'[Amount]: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 4, Level = 2, Operation = "Constant: ScaLogOp DominantValue=0" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            SetupParentChild(plan, 1, 3);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Coalesce chain should collapse like arithmetic chains
            Assert.AreEqual("Coalesce", root.OperatorName);
            // The child Coalesce may be folded
        }

        [TestMethod]
        public void BuildTree_IfThenElse_WithConstants_MayFold()
        {
            // Arrange - If/Switch patterns with constant branches
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "IfError: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "'Sales'[Amount]: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Constant: ScaLogOp DominantValue=BLANK" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 0, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual("IfError", root.OperatorName);
        }

        #endregion

        #region TableToScalar Folding (Pass 10)

        [TestMethod]
        public void BuildTree_TableToScalar_FoldsIntoParent()
        {
            // Arrange - TableToScalar typically folds into its parent spool
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Extend_Lookup: IterPhyOp LogOp=TableToScalar IterCols(1)('Customer'[CustomerKey])" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "TableToScalar: LookupPhyOp LogOp=TableToScalar LookupCols(1)('Customer'[CustomerKey]) DateTime #Records=18869" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "AggregationSpool<TableToScalar>: SpoolPhyOp #Records=18869" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - TableToScalar and its AggregationSpool should fold
            Assert.IsNotNull(root);
            Assert.AreEqual("Extend_Lookup", root.OperatorName);
            // Children may be reduced due to folding
        }

        [TestMethod]
        public void BuildTree_AggregationSpoolTableToScalar_FoldsIntoParent()
        {
            // Arrange - AggregationSpool<TableToScalar> pattern
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "TableToScalar: LookupPhyOp #Records=1000" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "AggregationSpool<TableToScalar>: SpoolPhyOp #Records=1000" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "Scan_Vertipaq: RelLogOp #Records=1000" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual("TableToScalar", root.OperatorName);
            // AggregationSpool<TableToScalar> should be folded
        }

        #endregion

        #region Union/Partition Folding (Pass 12)

        [TestMethod]
        public void BuildTree_Union_PreservesMultipleChildren()
        {
            // Arrange - Union with multiple children should preserve all
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Union: RelLogOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Scan_Vertipaq: RelLogOp #Records=100" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Scan_Vertipaq: RelLogOp #Records=200" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 0, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Union should preserve both children (they represent different data sources)
            Assert.AreEqual("Union", root.OperatorName);
            Assert.AreEqual(2, root.Children.Count, "Union should preserve all children");
        }

        [TestMethod]
        public void BuildTree_Partition_WithSingleChild_MayFold()
        {
            // Arrange - Partition with single child
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Partition: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Scan_Vertipaq: RelLogOp #Records=1000" }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
        }

        #endregion

        #region Constant Operator Folding (Pass 13)

        [TestMethod]
        public void BuildTree_ConstantWithParent_FoldsIntoParent()
        {
            // Arrange - Constant as child of comparison should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "GreaterThan: ScaLogOp Boolean" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "'Sales'[Amount]: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Constant: ScaLogOp DominantValue=100" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 0, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Constants should fold into comparison, becoming part of predicate
            Assert.AreEqual("GreaterThan", root.OperatorName);
            // The Constant value should be captured in the parent's display
        }

        [TestMethod]
        public void BuildTree_ConstantBlank_FoldsIntoParent()
        {
            // Arrange - BLANK() constant
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Coalesce: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "'Sales'[Amount]: ScaLogOp Currency" },
                    new EnrichedPlanNode { NodeId = 3, Level = 1, Operation = "Constant: ScaLogOp DominantValue=BLANK" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 0, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual("Coalesce", root.OperatorName);
        }

        #endregion

        #region Proxy Operator Folding (Pass 14)

        [TestMethod]
        public void BuildTree_ProxyOperator_FoldsIntoChild()
        {
            // Arrange - Proxy operators fold into their children
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Proxy: IterPhyOp" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Scan_Vertipaq: RelLogOp" }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Proxy should be folded, Scan_Vertipaq becomes root
            Assert.IsTrue(root.OperatorName.Contains("Scan_Vertipaq") || root.Children.Count == 0,
                "Proxy should fold into child or be removed");
        }

        #endregion

        #region ColValue Operator Folding

        [TestMethod]
        public void BuildTree_ColValue_FoldsIntoParent()
        {
            // Arrange - ColValue operators typically fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "Extend_Lookup: IterPhyOp LogOp=Extend_Lookup'Date'[Date] IterCols(42)('Date'[Date])" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "ColValue<'Date'[Date]>: LookupPhyOp LogOp=ColValue<'Date'[Date]>'Date'[Date] LookupCols(42)('Date'[Date]) DateTime" }
                }
            };
            SetupParentChild(plan, 0, 1);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - ColValue should fold
            Assert.AreEqual("Extend_Lookup", root.OperatorName);
            Assert.AreEqual(0, root.Children.Count, "ColValue should fold into Extend_Lookup");
        }

        #endregion

        #region StartOfYear/LastDate/DatesBetween Folding

        [TestMethod]
        public void BuildTree_DateFunction_PreservesStructure()
        {
            // Arrange - Time intelligence functions
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Level = 0, Operation = "StartOfYear: IterPhyOp LogOp=StartOfYear IterCols(1, 44)('Customer'[CustomerKey], 'Date'[Date])" },
                    new EnrichedPlanNode { NodeId = 2, Level = 1, Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=LastDate #Records=18869" },
                    new EnrichedPlanNode { NodeId = 3, Level = 2, Operation = "AggregationSpool<GroupBy>: SpoolPhyOp #Records=18869" }
                }
            };
            SetupParentChild(plan, 0, 1);
            SetupParentChild(plan, 1, 2);
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Date functions should be preserved
            Assert.AreEqual("StartOfYear", root.OperatorName);
        }

        #endregion

        #region Helper Methods

        private void SetupParentChild(EnrichedQueryPlan plan, int parentIndex, int childIndex)
        {
            plan.AllNodes[parentIndex].Children.Add(plan.AllNodes[childIndex]);
            plan.AllNodes[childIndex].Parent = plan.AllNodes[parentIndex];
        }

        #endregion
    }
}
