using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace DaxStudio.Tests.VisualQueryPlan
{
    /// <summary>
    /// Unit tests for XmSqlParser callback detection and query analysis.
    /// </summary>
    [TestClass]
    public class XmSqlParserTests
    {
        #region CallbackDataID Detection Tests

        [TestMethod]
        public void DetectCallbackType_CallbackDataID_ReturnsCorrectType()
        {
            // Arrange
            var xmSql = @"SELECT
                'Sales'[Amount],
                CallbackDataID ( DAXEVAL ( [Measure] ) )
            FROM 'Sales'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.CallbackDataID, result);
        }

        [TestMethod]
        public void DetectCallbackType_CallbackDataIDWithPFDATAID_ReturnsCorrectType()
        {
            // Arrange
            var xmSql = @"SELECT
                CallbackDataID ( PFDATAID ( 'Sales'[Amount] ) )
            FROM 'Sales'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.CallbackDataID, result);
        }

        [TestMethod]
        public void Parse_CallbackDataID_HasPerformanceWarning()
        {
            // Arrange
            var xmSql = @"SELECT CallbackDataID ( [Measure] ) FROM 'Sales'";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.HasPerformanceWarning);
            Assert.IsNotNull(info.PerformanceWarning);
            Assert.IsTrue(info.PerformanceWarning.Contains("NOT cached"));
        }

        #endregion

        #region EncodeCallback Detection Tests

        [TestMethod]
        public void DetectCallbackType_EncodeCallback_ReturnsCorrectType()
        {
            // Arrange
            var xmSql = @"SELECT
                EncodeCallback ( 'Sales'[CalcColumn] )
            FROM 'Sales'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.EncodeCallback, result);
        }

        #endregion

        #region LogAbsValueCallback Detection Tests

        [TestMethod]
        public void DetectCallbackType_LogAbsValueCallback_ReturnsCorrectType()
        {
            // Arrange - PRODUCT optimization pattern
            var xmSql = @"SELECT
                LogAbsValueCallback ( 'Sales'[Multiplier] )
            FROM 'Sales'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.LogAbsValueCallback, result);
        }

        #endregion

        #region RoundValueCallback Detection Tests

        [TestMethod]
        public void DetectCallbackType_RoundValueCallback_ReturnsCorrectType()
        {
            // Arrange
            var xmSql = @"SELECT
                RoundValueCallback ( 'Sales'[Price] )
            FROM 'Sales'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.RoundValueCallback, result);
        }

        #endregion

        #region MinMaxColumnPositionCallback Detection Tests

        [TestMethod]
        public void DetectCallbackType_MinMaxColumnPositionCallback_ReturnsCorrectType()
        {
            // Arrange
            var xmSql = @"SELECT
                MinMaxColumnPositionCallback ( 'Date'[Date] )
            FROM 'Date'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.MinMaxColumnPositionCallback, result);
        }

        #endregion

        #region Cond Callback Detection Tests

        [TestMethod]
        public void DetectCallbackType_Cond_ReturnsCorrectType()
        {
            // Arrange - Blank row conditional logic
            var xmSql = @"SELECT
                Cond ( 'Sales'[Amount], 0 )
            FROM 'Sales'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.Cond, result);
        }

        #endregion

        #region No Callback Tests

        [TestMethod]
        public void DetectCallbackType_NoCallback_ReturnsNone()
        {
            // Arrange - Simple query without callbacks
            var xmSql = @"SELECT
                'Sales'[Amount],
                SUM ( 'Sales'[Quantity] )
            FROM 'Sales'";

            // Act
            var result = XmSqlParser.DetectCallbackType(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.None, result);
        }

        [TestMethod]
        public void DetectCallbackType_NullInput_ReturnsNone()
        {
            // Act
            var result = XmSqlParser.DetectCallbackType(null);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.None, result);
        }

        [TestMethod]
        public void DetectCallbackType_EmptyInput_ReturnsNone()
        {
            // Act
            var result = XmSqlParser.DetectCallbackType("");

            // Assert
            Assert.AreEqual(XmSqlCallbackType.None, result);
        }

        #endregion

        #region Multiple Callbacks Tests

        [TestMethod]
        public void Parse_MultipleCallbacks_DetectsAll()
        {
            // Arrange - Query with multiple callback types
            var xmSql = @"SELECT
                CallbackDataID ( [Measure1] ),
                EncodeCallback ( 'Sales'[CalcCol] ),
                Cond ( 'Sales'[Amount], 0 )
            FROM 'Sales'";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.AreEqual(3, info.Callbacks.Count);
            Assert.IsTrue(info.Callbacks.Contains(XmSqlCallbackType.CallbackDataID));
            Assert.IsTrue(info.Callbacks.Contains(XmSqlCallbackType.EncodeCallback));
            Assert.IsTrue(info.Callbacks.Contains(XmSqlCallbackType.Cond));
            // Primary should be CallbackDataID (most significant)
            Assert.AreEqual(XmSqlCallbackType.CallbackDataID, info.PrimaryCallback);
        }

        #endregion

        #region Join Type Detection Tests

        [TestMethod]
        public void Parse_LeftOuterJoin_DetectsCorrectly()
        {
            // Arrange
            var xmSql = @"SELECT
                'Sales'[Amount]
            FROM 'Sales'
            LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey] = 'Product'[ProductKey]";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.JoinTypes.Contains(XmSqlJoinType.LeftOuterJoin));
        }

        [TestMethod]
        public void Parse_InnerJoinReducedBy_DetectsCorrectly()
        {
            // Arrange
            var xmSql = @"SELECT
                'Sales'[Amount]
            FROM 'Sales'
            INNER JOIN __DS0 REDUCED BY 'Product'[Category]";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.JoinTypes.Contains(XmSqlJoinType.InnerJoinReducedBy));
        }

        [TestMethod]
        public void Parse_ReverseHashJoin_DetectsCorrectly()
        {
            // Arrange
            var xmSql = @"SELECT
                'Sales'[Amount]
            FROM 'Sales'
            REVERSE HASH JOIN 'Customer' ON 'Sales'[CustomerKey] = 'Customer'[CustomerKey]";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.JoinTypes.Contains(XmSqlJoinType.ReverseHashJoin));
        }

        [TestMethod]
        public void Parse_ReverseBitmapJoin_DetectsCorrectly()
        {
            // Arrange
            var xmSql = @"SELECT
                'Sales'[Amount]
            FROM 'Sales'
            REVERSE BITMAP JOIN 'Date' ON 'Sales'[DateKey] = 'Date'[DateKey]";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.JoinTypes.Contains(XmSqlJoinType.ReverseBitmapJoin));
        }

        #endregion

        #region Feature Detection Tests

        [TestMethod]
        public void Parse_DefineTable_DetectsBatch()
        {
            // Arrange
            var xmSql = @"DEFINE TABLE __DS0 :=
                SELECTCOLUMNS ( 'Product', 'Product'[Category] )
            SELECT 'Sales'[Amount] FROM 'Sales'";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.UsesBatch);
        }

        [TestMethod]
        public void Parse_SimpleIndexN_DetectsBitmapIndex()
        {
            // Arrange
            var xmSql = @"SELECT 'Sales'[Amount]
            FROM 'Sales'
            WHERE SIMPLEINDEXN ( 'Product'[Category], 1, 2, 3 )";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.UsesBitmapIndex);
        }

        [TestMethod]
        public void Parse_InIndex_DetectsBitmapIndex()
        {
            // Arrange
            var xmSql = @"SELECT 'Sales'[Amount]
            FROM 'Sales'
            WHERE ININDEX ( 'Sales'[ProductKey], __IDX0 )";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.UsesBitmapIndex);
        }

        [TestMethod]
        public void Parse_DatacacheKindDense_ExtractsCorrectly()
        {
            // Arrange
            var xmSql = @"SET DC_KIND=""DENSE""
            SELECT 'Sales'[Amount] FROM 'Sales'";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.AreEqual("DENSE", info.DatacacheKind);
        }

        [TestMethod]
        public void Parse_DatacacheKindAuto_ExtractsCorrectly()
        {
            // Arrange
            var xmSql = @"SET DC_KIND=""AUTO""
            SELECT 'Sales'[Amount] FROM 'Sales'";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.AreEqual("AUTO", info.DatacacheKind);
        }

        #endregion

        #region Reference Extraction Tests

        [TestMethod]
        public void Parse_TableReferences_ExtractsCorrectly()
        {
            // Arrange
            var xmSql = @"SELECT
                'Sales'[Amount],
                'Product'[Name],
                'Customer'[City]
            FROM 'Sales'
            LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey] = 'Product'[ProductKey]";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.ReferencedTables.Contains("Sales"));
            Assert.IsTrue(info.ReferencedTables.Contains("Product"));
            Assert.IsTrue(info.ReferencedTables.Contains("Customer"));
        }

        [TestMethod]
        public void Parse_ColumnReferences_ExtractsCorrectly()
        {
            // Arrange
            var xmSql = @"SELECT
                'Sales'[Amount],
                'Sales'[Quantity]
            FROM 'Sales'";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.IsTrue(info.ReferencedColumns.Any(c => c.Contains("Amount")));
            Assert.IsTrue(info.ReferencedColumns.Any(c => c.Contains("Quantity")));
        }

        #endregion

        #region Description Tests

        [TestMethod]
        public void GetCallbackDescription_CallbackDataID_ReturnsDescription()
        {
            // Act
            var description = XmSqlParser.GetCallbackDescription(XmSqlCallbackType.CallbackDataID);

            // Assert
            Assert.IsNotNull(description);
            Assert.IsTrue(description.Contains("NOT cached"));
        }

        [TestMethod]
        public void GetCallbackDescription_EncodeCallback_ReturnsDescription()
        {
            // Act
            var description = XmSqlParser.GetCallbackDescription(XmSqlCallbackType.EncodeCallback);

            // Assert
            Assert.IsNotNull(description);
            Assert.IsTrue(description.Contains("DEFINE COLUMN"));
        }

        [TestMethod]
        public void GetCallbackDescription_None_ReturnsNull()
        {
            // Act
            var description = XmSqlParser.GetCallbackDescription(XmSqlCallbackType.None);

            // Assert
            Assert.IsNull(description);
        }

        [TestMethod]
        public void GetJoinDescription_LeftOuterJoin_ReturnsDescription()
        {
            // Act
            var description = XmSqlParser.GetJoinDescription(XmSqlJoinType.LeftOuterJoin);

            // Assert
            Assert.IsNotNull(description);
            Assert.IsTrue(description.Contains("Many-to-one"));
        }

        [TestMethod]
        public void GetJoinDescription_ReverseHashJoin_ReturnsDescription()
        {
            // Act
            var description = XmSqlParser.GetJoinDescription(XmSqlJoinType.ReverseHashJoin);

            // Assert
            Assert.IsNotNull(description);
            Assert.IsTrue(description.Contains("many-side"));
        }

        #endregion

        #region Severity Tests

        [TestMethod]
        public void GetCallbackSeverity_CallbackDataID_ReturnsWarning()
        {
            // Act
            var severity = XmSqlParser.GetCallbackSeverity(XmSqlCallbackType.CallbackDataID);

            // Assert
            Assert.AreEqual(IssueSeverity.Warning, severity);
        }

        [TestMethod]
        public void GetCallbackSeverity_EncodeCallback_ReturnsInfo()
        {
            // Act
            var severity = XmSqlParser.GetCallbackSeverity(XmSqlCallbackType.EncodeCallback);

            // Assert
            Assert.AreEqual(IssueSeverity.Info, severity);
        }

        [TestMethod]
        public void GetCallbackSeverity_None_ReturnsInfo()
        {
            // Act
            var severity = XmSqlParser.GetCallbackSeverity(XmSqlCallbackType.None);

            // Assert
            Assert.AreEqual(IssueSeverity.Info, severity);
        }

        #endregion

        #region Complex Query Tests

        [TestMethod]
        public void Parse_ComplexQuery_ExtractsAllInfo()
        {
            // Arrange - Complex xmSQL with multiple features
            var xmSql = @"SET DC_KIND=""DENSE""
            DEFINE TABLE __DS0 :=
                SELECTCOLUMNS ( 'Product', 'Product'[Category] )
            SELECT
                'Sales'[Amount],
                CallbackDataID ( PFDATAID ( 'Sales'[Measure] ) ),
                SUM ( 'Sales'[Quantity] )
            FROM 'Sales'
            LEFT OUTER JOIN 'Product' ON 'Sales'[ProductKey] = 'Product'[ProductKey]
            WHERE ININDEX ( 'Sales'[DateKey], __IDX0 )";

            // Act
            var info = XmSqlParser.Parse(xmSql);

            // Assert
            Assert.AreEqual(XmSqlCallbackType.CallbackDataID, info.PrimaryCallback);
            Assert.IsTrue(info.HasPerformanceWarning);
            Assert.IsTrue(info.UsesBatch);
            Assert.IsTrue(info.UsesBitmapIndex);
            Assert.AreEqual("DENSE", info.DatacacheKind);
            Assert.IsTrue(info.JoinTypes.Contains(XmSqlJoinType.LeftOuterJoin));
            Assert.IsTrue(info.ReferencedTables.Contains("Sales"));
            Assert.IsTrue(info.ReferencedTables.Contains("Product"));
        }

        #endregion
    }
}
