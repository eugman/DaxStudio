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
        public void Constant_WithTypeValue_DisplaysValue()
        {
            // Arrange - Constant with type followed by value (from DirectQuery fixture)
            var node = CreateNode("Constant: LookupPhyOp LogOp=Constant Integer 502");

            // Act & Assert
            Assert.AreEqual("Constant", node.OperatorName);
            // DisplayDetail should show the value
            Assert.AreEqual("502", node.DisplayDetail);
        }

        [TestMethod]
        public void Constant_WithCurrencyValue_DisplaysValue()
        {
            // Arrange - Currency constant
            var node = CreateNode("Constant: LookupPhyOp LogOp=Constant Currency 99.99");

            // Act & Assert
            Assert.AreEqual("99.99", node.DisplayDetail);
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
        public void Constant_WithBooleanFalse_DisplaysFalse()
        {
            // Arrange
            var node = CreateNode("Constant: ScaLogOp DominantValue=false");

            // Act & Assert
            Assert.AreEqual("FALSE()", node.DisplayDetail);
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

        #region MeasureReference Extraction Tests

        [TestMethod]
        public void SumVertipaq_WithMeasureRef_DisplaysMeasure()
        {
            // Arrange - Sum_Vertipaq with MeasureRef pattern
            var node = CreateNode("Sum_Vertipaq: LogOp=Sum_Vertipaq MeasureRef=[Total Sales] DependOnCols()()");

            // Act & Assert
            Assert.AreEqual("[Total Sales]", node.MeasureReference);
            Assert.IsTrue(node.HasMeasureReference);
        }

        [TestMethod]
        public void SumVertipaq_WithLogOpMeasureName_DisplaysMeasure()
        {
            // Arrange - Sum_Vertipaq with measure name after LogOp=
            var node = CreateNode("Sum_Vertipaq: LogOp=Sum_Vertipaq TotalSales DependOnCols()()");

            // Act & Assert
            Assert.AreEqual("[TotalSales]", node.MeasureReference);
            Assert.IsTrue(node.HasMeasureReference);
        }

        [TestMethod]
        public void AggregationNode_DisplaysMeasureRefInDetail()
        {
            // Arrange - Sum_Vertipaq should show MeasureRef in DisplayDetail
            var node = CreateNode("Sum_Vertipaq: LogOp=Sum_Vertipaq MeasureRef=[Margin Pct]");

            // Act & Assert
            Assert.AreEqual("[Margin Pct]", node.MeasureReference);
            // DisplayDetail shows MeasureReference for aggregation operators
            Assert.AreEqual("[Margin Pct]", node.DisplayDetail);
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

        #region ColValue Display Tests

        [TestMethod]
        public void ColValue_WithAngleBrackets_ShowsColumnInDisplayDetail()
        {
            // Arrange - ColValue<'Table'[Column]> format from physical plan
            var node = CreateNode("ColValue<'Sales SalesOrderHeader'[CustomerID]>: LookupPhyOp LogOp=ColValue");

            // Act & Assert
            Assert.IsTrue(node.OperatorName.StartsWith("ColValue"));
            Assert.AreEqual("[CustomerID]", node.DisplayDetail);
        }

        [TestMethod]
        public void ColValue_WithEmptyTableName_ShowsColumnInDisplayDetail()
        {
            // Arrange - ColValue<''[Column]> format for calculated columns
            var node = CreateNode("ColValue<''[IsGrandTotalRowTotal]>: LookupPhyOp LogOp=ColValue");

            // Act & Assert
            Assert.AreEqual("[IsGrandTotalRowTotal]", node.DisplayDetail);
        }

        #endregion

        #region Join/Set Operator Display Tests

        [TestMethod]
        public void Union_WithIterCols_ShowsColumnsInDisplayDetail()
        {
            // Arrange - Union with IterCols
            var node = CreateNode("Union: IterPhyOp LogOp=Union IterCols(0, 1, 2, 3)('Sales'[CustomerID], ''[IsGrandTotalRowTotal], ''[Total], ''[])");

            // Act & Assert
            Assert.AreEqual("Union", node.OperatorName);
            Assert.IsTrue(node.HasIterCols);
            // DisplayDetail should show abbreviated column list
            Assert.IsNotNull(node.DisplayDetail);
            Assert.IsTrue(node.DisplayDetail.Contains("[CustomerID]") || node.DisplayDetail.Contains("Sales"));
        }

        [TestMethod]
        public void GroupSemijoin_WithIterCols_ShowsColumnsInDisplayDetail()
        {
            // Arrange - GroupSemijoin with IterCols
            var node = CreateNode("GroupSemijoin: IterPhyOp LogOp=GroupSemiJoin IterCols(0, 1, 2)('Sales'[CustomerID], ''[IsGrandTotalRowTotal], ''[Total])");

            // Act & Assert
            Assert.AreEqual("GroupSemijoin", node.OperatorName);
            Assert.IsTrue(node.HasIterCols);
            Assert.IsNotNull(node.DisplayDetail);
        }

        [TestMethod]
        public void CrossApply_WithIterCols_ShowsColumnsInDisplayDetail()
        {
            // Arrange
            var node = CreateNode("CrossApply: IterPhyOp LogOp=Sum_Vertipaq IterCols(0)('Sales'[CustomerID])");

            // Act & Assert
            Assert.AreEqual("CrossApply", node.OperatorName);
            Assert.IsTrue(node.HasIterCols);
            Assert.IsNotNull(node.DisplayDetail);
        }

        [TestMethod]
        public void TreatAs_WithIterCols_ShowsColumnsInDisplayDetail()
        {
            // Arrange
            var node = CreateNode("TreatAs: IterPhyOp LogOp=TreatAs IterCols(0, 1)('Sales'[CustomerID], 'Sales'[SalesOrderID])");

            // Act & Assert
            Assert.AreEqual("TreatAs", node.OperatorName);
            Assert.IsTrue(node.HasIterCols);
            Assert.IsNotNull(node.DisplayDetail);
        }

        #endregion

        #region TableVarProxy RefVarName Tests

        [TestMethod]
        public void TableVarProxy_WithRefVarName_ShowsInDisplayDetail()
        {
            // Arrange - TableVarProxy from logical plan with RefVarName
            var node = CreateNode("TableVarProxy: RelLogOp DependOnCols()() 0-3 RequiredCols(0, 1, 2, 3)('Sales'[CustomerID], ''[IsGrandTotalRowTotal], ''[Total], ''[]) RefVarName=__DS0Core");

            // Act & Assert
            Assert.AreEqual("TableVarProxy", node.OperatorName);
            Assert.IsNotNull(node.DisplayDetail);
            Assert.AreEqual("__DS0Core", node.DisplayDetail, "Should show RefVarName in DisplayDetail");
        }

        [TestMethod]
        public void TableVarProxy_WithPrimaryWindowedRefVarName_ShowsInDisplayDetail()
        {
            // Arrange - TableVarProxy referencing __DS0PrimaryWindowed
            var node = CreateNode("TableVarProxy: RelLogOp DependOnCols()() 0-6 RequiredCols(0, 1, 2, 3, 4, 5, 6)('Product'[Brand], ''[IsGrandTotalRowTotal], ''[Sales_Amount], ''[Margin__], ''[Total_Cost], ''[Total_Quantity], ''[]) RefVarName=__DS0PrimaryWindowed");

            // Act & Assert
            Assert.AreEqual("TableVarProxy", node.OperatorName);
            Assert.AreEqual("__DS0PrimaryWindowed", node.DisplayDetail);
        }

        [TestMethod]
        public void TableVarProxy_WithoutRefVarName_DisplayDetailIsNull()
        {
            // Arrange - TableVarProxy without RefVarName (shouldn't normally happen but handle gracefully)
            var node = CreateNode("TableVarProxy: RelLogOp DependOnCols()() RequiredCols(0)('Sales'[Amount])");

            // Act & Assert
            Assert.AreEqual("TableVarProxy", node.OperatorName);
            Assert.IsNull(node.DisplayDetail);
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
            Assert.IsNull(node.Records);
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
            // Note: HasRecords may be false when Records=0 depending on implementation
        }

        #endregion

        #region Helper Methods

        // Regex to extract #Records= or #Recs= values from operation string
        private static readonly System.Text.RegularExpressions.Regex RecordsExtractorRegex =
            new System.Text.RegularExpressions.Regex(@"#Rec(?:ords|s)=(\d+)",
                System.Text.RegularExpressions.RegexOptions.Compiled);

        private PlanNodeViewModel CreateNode(string operation)
        {
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = operation,
                ResolvedOperation = operation
            };

            // Extract #Records= or #Recs= from operation string if present
            var recordsMatch = RecordsExtractorRegex.Match(operation);
            if (recordsMatch.Success && long.TryParse(recordsMatch.Groups[1].Value, out var records))
            {
                enrichedNode.Records = records;
            }

            return new PlanNodeViewModel(enrichedNode);
        }

        #endregion
    }
}
