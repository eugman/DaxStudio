using System.Collections.Generic;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ExampleReimplementation.Phase01_DataModels
{
    /// <summary>
    /// Phase 1: Data Models & Basic Parsing
    ///
    /// These tests verify the core parsing logic for extracting operator names
    /// from operation strings. This is fundamental to all subsequent phases.
    ///
    /// Prerequisites: EnrichedPlanNode, PlanNodeViewModel (minimal)
    ///
    /// Reference: docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md - Phase 1
    /// </summary>
    [TestClass]
    public class OperatorNameExtractionTests
    {
        #region Physical Plan Format Tests

        [TestMethod]
        public void OperatorName_PhysicalFormat_ExtractsCorrectly()
        {
            // Arrange - Standard physical plan format: "Operator: Details"
            var node = CreateNodeWithOperation("AddColumns: RelLogOp DependOnCols()() 0-3 RequiredCols(0, 1)");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("AddColumns", operatorName, "Should extract 'AddColumns' from physical plan format");
        }

        [TestMethod]
        public void OperatorName_VertipaqScan_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNodeWithOperation("Scan_Vertipaq: RelLogOp DependOnCols()() #Records=1000");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("Scan_Vertipaq", operatorName);
        }

        [TestMethod]
        public void OperatorName_SpoolIterator_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNodeWithOperation("Spool_Iterator: IterPhyOp #Records=50000");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("Spool_Iterator", operatorName);
        }

        [TestMethod]
        public void OperatorName_SpaceBeforeColon_UsesSpaceAsDelimiter()
        {
            // Arrange - Space comes before colon
            var node = CreateNodeWithOperation("Scan_Vertipaq RelLogOp: DependOnCols()");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("Scan_Vertipaq", operatorName, "Should use space as delimiter when it comes before colon");
        }

        #endregion

        #region Logical Plan Format Tests (Column Reference)

        [TestMethod]
        public void OperatorName_LogicalColumnReferenceFormat_ExtractsCorrectly()
        {
            // Arrange - Logical plan with column reference: "'Table'[Column]: Operator Details"
            var node = CreateNodeWithOperation("'Internet Sales'[Sales Amount]: ScaLogOp DependOnCols(106)('Internet Sales'[Sales Amount]) Currency DominantValue=NONE");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("ScaLogOp", operatorName, "Should extract 'ScaLogOp' from logical plan with column reference");
        }

        [TestMethod]
        public void OperatorName_SimpleColumnReference_ExtractsCorrectly()
        {
            // Arrange - Simple column reference format
            var node = CreateNodeWithOperation("'Sales'[Amount]: Sum_Vertipaq");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("Sum_Vertipaq", operatorName, "Should extract 'Sum_Vertipaq' after column reference");
        }

        [TestMethod]
        public void OperatorName_NestedBrackets_HandlesCorrectly()
        {
            // Arrange - Column name with nested brackets/quotes
            var node = CreateNodeWithOperation("'Internet Sales'[Customer's Order]: GroupBy_Vertipaq Details");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("GroupBy_Vertipaq", operatorName, "Should handle nested quotes in table name");
        }

        #endregion

        #region Edge Cases

        [TestMethod]
        public void OperatorName_NoDelimiter_ReturnsFullString()
        {
            // Arrange - No space or colon
            var node = CreateNodeWithOperation("SomeOperator");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("SomeOperator", operatorName, "Should return full string when no delimiter");
        }

        [TestMethod]
        public void OperatorName_EmptyString_ReturnsEmpty()
        {
            // Arrange
            var node = CreateNodeWithOperation("");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("", operatorName, "Should return empty for empty input");
        }

        [TestMethod]
        public void OperatorName_NullOperation_ReturnsEmpty()
        {
            // Arrange
            var node = CreateNodeWithOperation(null);

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("", operatorName, "Should return empty for null input");
        }

        #endregion

        #region Helper Methods

        private PlanNodeViewModel CreateNodeWithOperation(string operation)
        {
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = operation,
                ResolvedOperation = operation
            };
            return new PlanNodeViewModel(enrichedNode);
        }

        #endregion
    }
}
