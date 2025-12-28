using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ExampleReimplementation.Phase01_DataModels
{
    /// <summary>
    /// Phase 1: Data Models & Basic Parsing
    ///
    /// These tests verify property extraction from DAX query plan operation strings.
    /// Tests RequiredCols, DependOnCols, JoinCols, SemijoinCols, BlankRow, TableId, DataType.
    ///
    /// Prerequisites: EnrichedPlanNode, PlanNodeViewModel with property extraction regex patterns
    ///
    /// Reference: docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md - Phase 1, Appendix: Regex Patterns
    /// </summary>
    [TestClass]
    public class PropertyExtractionTests
    {
        #region RequiredCols Tests

        [TestMethod]
        public void RequiredCols_WithColumns_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("AddColumns: RelLogOp RequiredCols(0, 1)('Sales'[Amount], 'Sales'[Qty])");

            // Act & Assert
            Assert.IsNotNull(node.RequiredCols);
            Assert.IsTrue(node.RequiredCols.Contains("Sales"));
            Assert.IsTrue(node.RequiredCols.Contains("Amount"));
            Assert.AreEqual("0, 1", node.RequiredColsIndices);
            Assert.IsTrue(node.HasRequiredCols);
        }

        [TestMethod]
        public void RequiredCols_Empty_ReturnsEmpty()
        {
            // Arrange
            var node = CreateNode("AddColumns: RelLogOp RequiredCols()()");

            // Act & Assert
            Assert.AreEqual("(empty)", node.RequiredCols);
            Assert.AreEqual("(empty)", node.RequiredColsIndices);
            Assert.IsTrue(node.HasRequiredCols);
        }

        [TestMethod]
        public void RequiredCols_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp #Records=1000");

            // Act & Assert
            Assert.IsNull(node.RequiredCols);
            Assert.IsFalse(node.HasRequiredCols);
        }

        #endregion

        #region DependOnCols Tests

        [TestMethod]
        public void DependOnCols_WithColumns_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Calculate: ScaLogOp DependOnCols(106)('Internet Sales'[Sales Amount]) Currency");

            // Act & Assert
            Assert.IsNotNull(node.DependOnCols);
            Assert.IsTrue(node.DependOnCols.Contains("Internet Sales"));
            Assert.IsTrue(node.DependOnCols.Contains("Sales Amount"));
            Assert.AreEqual("106", node.DependOnColsIndices);
            Assert.IsTrue(node.HasDependOnCols);
        }

        [TestMethod]
        public void DependOnCols_Empty_ReturnsEmpty()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp DependOnCols()() #Records=1000");

            // Act & Assert
            Assert.AreEqual("(empty)", node.DependOnCols);
            Assert.IsTrue(node.HasDependOnCols);
        }

        #endregion

        #region IterCols Tests

        [TestMethod]
        public void IterCols_WithColumns_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Spool_Iterator<SpoolIterator>: IterPhyOp IterCols(0)('Customer'[First Name]) #Records=670");

            // Act & Assert
            Assert.IsNotNull(node.IterCols);
            Assert.IsTrue(node.IterCols.Contains("Customer"));
            Assert.IsTrue(node.IterCols.Contains("First Name"));
            Assert.AreEqual("0", node.IterColsIndices);
            Assert.IsTrue(node.HasIterCols);
        }

        [TestMethod]
        public void IterCols_Empty_ReturnsEmpty()
        {
            // Arrange
            var node = CreateNode("AddColumns: IterPhyOp IterCols()()");

            // Act & Assert
            Assert.AreEqual("(empty)", node.IterCols);
            Assert.IsTrue(node.HasIterCols);
        }

        #endregion

        #region BlankRow Tests

        [TestMethod]
        public void BlankRowIndicator_PlusBlankRow_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp +BlankRow Table=0");

            // Act & Assert
            Assert.AreEqual("+BlankRow", node.BlankRowIndicator);
            Assert.IsTrue(node.IncludesBlankRow);
            Assert.IsTrue(node.HasBlankRowIndicator);
            Assert.AreEqual("Includes blank row", node.BlankRowDisplay);
        }

        [TestMethod]
        public void BlankRowIndicator_MinusBlankRow_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp -BlankRow Table=0");

            // Act & Assert
            Assert.AreEqual("-BlankRow", node.BlankRowIndicator);
            Assert.IsFalse(node.IncludesBlankRow);
            Assert.IsTrue(node.HasBlankRowIndicator);
            Assert.AreEqual("Excludes blank row", node.BlankRowDisplay);
        }

        #endregion

        #region TableId Tests

        [TestMethod]
        public void TableId_WithValue_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp Table=0 +BlankRow");

            // Act & Assert
            Assert.AreEqual(0, node.TableId);
            Assert.IsTrue(node.HasTableId);
        }

        [TestMethod]
        public void TableId_MultipleDigits_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp Table=123");

            // Act & Assert
            Assert.AreEqual(123, node.TableId);
            Assert.IsTrue(node.HasTableId);
        }

        #endregion

        #region DataType Tests

        [TestMethod]
        public void DataType_Currency_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Calculate: ScaLogOp Currency DominantValue=BLANK");

            // Act & Assert
            Assert.AreEqual("Currency", node.DataType);
            Assert.IsTrue(node.HasDataType);
        }

        [TestMethod]
        public void DataType_Integer_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Constant: ScaLogOp Integer 100");

            // Act & Assert
            Assert.AreEqual("Integer", node.DataType);
            Assert.IsTrue(node.HasDataType);
        }

        [TestMethod]
        public void DataType_Boolean_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("GreaterThan: ScaLogOp Boolean");

            // Act & Assert
            Assert.AreEqual("Boolean", node.DataType);
            Assert.IsTrue(node.HasDataType);
        }

        #endregion

        #region Constant Value Extraction Tests

        [TestMethod]
        public void Constant_WithDominantValue_DisplaysValue()
        {
            // Arrange - Constant with DominantValue
            var node = CreateNode("Constant: LookupPhyOp LogOp=Constant Currency DominantValue=123.45");

            // Act & Assert
            Assert.AreEqual("Constant", node.OperatorName);
            Assert.AreEqual("123.45", node.DominantValue);
        }

        [TestMethod]
        public void Constant_WithBooleanTrue_DisplaysTrue()
        {
            // Arrange - Boolean constant
            var node = CreateNode("Constant: ScaLogOp DominantValue=true");

            // Act & Assert
            Assert.AreEqual("TRUE()", node.DisplayDetail);
        }

        [TestMethod]
        public void Constant_WithBlankValue_DisplaysBlank()
        {
            // Arrange
            var node = CreateNode("Constant: ScaLogOp DominantValue=BLANK");

            // Act & Assert
            Assert.AreEqual("BLANK()", node.DisplayDetail);
        }

        #endregion

        #region Records Extraction Tests

        [TestMethod]
        public void Records_WithHashRecords_ExtractsCorrectly()
        {
            // Arrange - Standard #Records= format
            var node = CreateNode("Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=12345");

            // Act & Assert
            Assert.AreEqual(12345, node.Records);
            Assert.IsTrue(node.HasRecords);
        }

        [TestMethod]
        public void Records_WithHashRecs_ExtractsCorrectly()
        {
            // Arrange - Alternative #Recs= format (some plans use this)
            var node = CreateNode("SpoolLookup: LookupPhyOp #Recs=500000");

            // Act & Assert
            Assert.AreEqual(500000, node.Records);
            Assert.IsTrue(node.HasRecords);
        }

        [TestMethod]
        public void Records_LargeNumber_ExtractsCorrectly()
        {
            // Arrange - Large record count (millions)
            var node = CreateNode("Scan_Vertipaq: RelLogOp #Records=6255798");

            // Act & Assert
            Assert.AreEqual(6255798, node.Records);
            Assert.IsTrue(node.HasRecords);
        }

        [TestMethod]
        public void Records_NotPresent_ReturnsNull()
        {
            // Arrange - No records property
            var node = CreateNode("AddColumns: IterPhyOp LogOp=AddColumns IterCols(0)(''[Test])");

            // Act & Assert
            Assert.IsFalse(node.HasRecords);
            Assert.AreEqual(0, node.Records);
        }

        [TestMethod]
        public void Records_WithKeyCols_BothExtractCorrectly()
        {
            // Arrange - Records with other properties like #KeyCols, #ValueCols
            var node = CreateNode("SpoolLookup: LookupPhyOp #Records=5647 #KeyCols=247 #ValueCols=1");

            // Act & Assert
            Assert.AreEqual(5647, node.Records);
            Assert.IsTrue(node.HasRecords);
        }

        [TestMethod]
        public void Records_Zero_ExtractsCorrectly()
        {
            // Arrange - Edge case: zero records
            var node = CreateNode("Spool_Iterator: IterPhyOp #Records=0");

            // Act & Assert
            Assert.AreEqual(0, node.Records);
            Assert.IsTrue(node.HasRecords);
        }

        #endregion

        #region Complex Operation String Tests

        [TestMethod]
        public void MultipleProperties_AllExtractCorrectly()
        {
            // Arrange - Complex operation with multiple properties
            var node = CreateNode("Scan_Vertipaq: RelLogOp DependOnCols(0, 1)('Sales'[Date], 'Sales'[Amount]) RequiredCols(2)('Sales'[Qty]) +BlankRow Table=5");

            // Act & Assert
            Assert.IsTrue(node.HasDependOnCols);
            Assert.IsTrue(node.DependOnCols.Contains("Sales"));
            Assert.AreEqual("0, 1", node.DependOnColsIndices);

            Assert.IsTrue(node.HasRequiredCols);
            Assert.IsTrue(node.RequiredCols.Contains("Qty"));
            Assert.AreEqual("2", node.RequiredColsIndices);

            Assert.AreEqual("+BlankRow", node.BlankRowIndicator);
            Assert.AreEqual(5, node.TableId);
        }

        #endregion

        #region Helper Methods

        private PlanNodeViewModel CreateNode(string operation)
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
