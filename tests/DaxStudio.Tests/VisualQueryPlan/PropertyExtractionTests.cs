using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.VisualQueryPlan
{
    /// <summary>
    /// Unit tests for property extraction from DAX query plan operation strings.
    /// Tests RequiredCols, DependOnCols, JoinCols, SemijoinCols, BlankRow, TableId, DataType.
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

        [TestMethod]
        public void DependOnCols_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("AddColumns: IterPhyOp");

            // Act & Assert
            Assert.IsNull(node.DependOnCols);
            Assert.IsFalse(node.HasDependOnCols);
        }

        #endregion

        #region JoinCols Tests

        [TestMethod]
        public void JoinCols_WithColumns_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp JoinCols(0)('Customer'[CustomerKey]) +BlankRow");

            // Act & Assert
            Assert.IsNotNull(node.JoinCols);
            Assert.IsTrue(node.JoinCols.Contains("Customer"));
            Assert.IsTrue(node.JoinCols.Contains("CustomerKey"));
            Assert.AreEqual("0", node.JoinColsIndices);
            Assert.IsTrue(node.HasJoinCols);
        }

        [TestMethod]
        public void JoinCols_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("AddColumns: RelLogOp RequiredCols(0, 1)('T'[Col])");

            // Act & Assert
            Assert.IsNull(node.JoinCols);
            Assert.IsFalse(node.HasJoinCols);
        }

        #endregion

        #region SemijoinCols Tests

        [TestMethod]
        public void SemijoinCols_WithColumns_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("GroupSemiJoin: RelLogOp SemijoinCols(1)('Product'[ProductID])");

            // Act & Assert
            Assert.IsNotNull(node.SemijoinCols);
            Assert.IsTrue(node.SemijoinCols.Contains("Product"));
            Assert.IsTrue(node.SemijoinCols.Contains("ProductID"));
            Assert.AreEqual("1", node.SemijoinColsIndices);
            Assert.IsTrue(node.HasSemijoinCols);
        }

        [TestMethod]
        public void SemijoinCols_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("Filter: RelLogOp #Records=500");

            // Act & Assert
            Assert.IsNull(node.SemijoinCols);
            Assert.IsFalse(node.HasSemijoinCols);
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

        #region LookupCols Tests

        [TestMethod]
        public void LookupCols_WithColumns_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Spool: LookupPhyOp LookupCols(1)('Date'[Year])");

            // Act & Assert
            Assert.IsNotNull(node.LookupCols);
            Assert.IsTrue(node.LookupCols.Contains("Date"));
            Assert.IsTrue(node.LookupCols.Contains("Year"));
            Assert.AreEqual("1", node.LookupColsIndices);
            Assert.IsTrue(node.HasLookupCols);
        }

        [TestMethod]
        public void LookupCols_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("Filter: IterPhyOp");

            // Act & Assert
            Assert.IsNull(node.LookupCols);
            Assert.IsFalse(node.HasLookupCols);
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

        [TestMethod]
        public void BlankRowIndicator_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("AddColumns: IterPhyOp #Records=100");

            // Act & Assert
            Assert.IsNull(node.BlankRowIndicator);
            Assert.IsNull(node.IncludesBlankRow);
            Assert.IsFalse(node.HasBlankRowIndicator);
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

        [TestMethod]
        public void TableId_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("AddColumns: IterPhyOp");

            // Act & Assert
            Assert.IsNull(node.TableId);
            Assert.IsFalse(node.HasTableId);
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
        public void DataType_String_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("ColValue: ScaLogOp String 'Test'");

            // Act & Assert
            Assert.AreEqual("String", node.DataType);
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

        [TestMethod]
        public void DataType_DateTime_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("DatesBetween: IterPhyOp DateTime");

            // Act & Assert
            Assert.AreEqual("DateTime", node.DataType);
            Assert.IsTrue(node.HasDataType);
        }

        [TestMethod]
        public void DataType_Real_ExtractsCorrectly()
        {
            // Arrange
            var node = CreateNode("Sum_Vertipaq: ScaLogOp Real");

            // Act & Assert
            Assert.AreEqual("Real", node.DataType);
            Assert.IsTrue(node.HasDataType);
        }

        [TestMethod]
        public void DataType_NotPresent_ReturnsNull()
        {
            // Arrange
            var node = CreateNode("Scan_Vertipaq: RelLogOp #Records=1000");

            // Act & Assert
            Assert.IsNull(node.DataType);
            Assert.IsFalse(node.HasDataType);
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

        [TestMethod]
        public void PhysicalPlanFormat_ExtractsProperties()
        {
            // Arrange - Physical plan format
            var node = CreateNode("AddColumns: IterPhyOp LogOp=AddColumns IterCols(0)(''[Total Sales]) #Records=1000");

            // Act & Assert
            Assert.IsTrue(node.HasIterCols);
            Assert.IsTrue(node.IterCols.Contains("Total Sales"));
            Assert.AreEqual("0", node.IterColsIndices);
        }

        [TestMethod]
        public void LogicalPlanFormat_ExtractsProperties()
        {
            // Arrange - Logical plan format with column reference
            var node = CreateNode("'Internet Sales'[Sales Amount]: ScaLogOp DependOnCols(106)('Internet Sales'[Sales Amount]) Currency DominantValue=NONE");

            // Act & Assert
            Assert.IsTrue(node.HasDependOnCols);
            Assert.AreEqual("Currency", node.DataType);
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
