using DaxStudio.UI.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.ExampleReimplementation.Phase02_OperatorDictionary
{
    /// <summary>
    /// Phase 2: Operator Dictionary & Engine Classification
    ///
    /// These tests verify the operator dictionary that maps operator names to:
    /// - Display names (human-readable)
    /// - Categories (Iterator, Storage Engine, Logical, etc.)
    /// - Engine types (Storage Engine, Formula Engine, DirectQuery)
    ///
    /// Prerequisites: Phase 1 complete, DaxOperatorDictionary class
    ///
    /// Reference: docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md - Phase 2
    /// </summary>
    [TestClass]
    public class DaxOperatorDictionaryTests
    {
        #region Core Operator Lookup Tests

        [TestMethod]
        public void GetOperatorInfo_AddColumns_ReturnsFormulaEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("AddColumns");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Add Columns", info.DisplayName);
            Assert.AreEqual("Iterator", info.Category);
            Assert.AreEqual(EngineType.FormulaEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_ScanVertipaq_ReturnsStorageEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Scan_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Scan", info.DisplayName);
            Assert.AreEqual("Storage Engine", info.Category);
            Assert.AreEqual(EngineType.StorageEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_Calculate_ReturnsLogicalCategory()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Calculate");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Calculate", info.DisplayName);
            Assert.AreEqual("Logical", info.Category);
        }

        #endregion

        #region Case-Insensitive Match Tests

        [TestMethod]
        public void GetOperatorInfo_LowerCase_MatchesCaseInsensitive()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("addcolumns");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Add Columns", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_UpperCase_MatchesCaseInsensitive()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("SCAN_VERTIPAQ");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Scan", info.DisplayName);
        }

        #endregion

        #region Parameterized Operator Tests (Generic Matching)

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolSum_ReturnsSpecificInfo()
        {
            // Act - Should match AggregationSpool<Sum> specifically
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<Sum>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool (Sum)", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_AggregationSpoolUnknown_FallsBackToBase()
        {
            // Act - Unknown variant should fall back to base AggregationSpool
            var info = DaxOperatorDictionary.GetOperatorInfo("AggregationSpool<SomeNewType>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Aggregation Spool", info.DisplayName);
        }

        [TestMethod]
        public void GetOperatorInfo_SpoolIterator_ReturnsFormulaEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Spool_Iterator<SpoolIterator>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Spool Iterator", info.DisplayName);
            Assert.AreEqual(EngineType.FormulaEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_ProjectionSpoolNested_FallsBackToBase()
        {
            // Act - Nested template should match base
            var info = DaxOperatorDictionary.GetOperatorInfo("ProjectionSpool<ProjectFusion<Copy>>");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Projection Spool", info.DisplayName);
        }

        #endregion

        #region Storage Engine Aggregation Tests

        [TestMethod]
        public void GetOperatorInfo_SumVertipaq_ReturnsStorageEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Sum_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Sum", info.DisplayName);
            Assert.AreEqual(EngineType.StorageEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_CountVertipaq_ReturnsStorageEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Count_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Count", info.DisplayName);
            Assert.AreEqual(EngineType.StorageEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_DistinctCountVertipaq_ReturnsStorageEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("DistinctCount_Vertipaq");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("VertiPaq Distinct Count", info.DisplayName);
            Assert.AreEqual(EngineType.StorageEngine, info.Engine);
        }

        #endregion

        #region Formula Engine Iterator Tests

        [TestMethod]
        public void GetOperatorInfo_Filter_ReturnsFormulaEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Filter");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Filter", info.DisplayName);
            Assert.AreEqual(EngineType.FormulaEngine, info.Engine);
        }

        [TestMethod]
        public void GetOperatorInfo_CrossApply_ReturnsFormulaEngine()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("CrossApply");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual(EngineType.FormulaEngine, info.Engine);
        }

        #endregion

        #region Comparison and Arithmetic Operator Tests

        [TestMethod]
        public void GetOperatorInfo_GreaterThan_ReturnsComparison()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("GreaterThan");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Greater Than", info.DisplayName);
            Assert.AreEqual("Comparison", info.Category);
        }

        [TestMethod]
        public void GetOperatorInfo_Add_ReturnsArithmetic()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("Add");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual("Add", info.DisplayName);
            Assert.AreEqual("Arithmetic", info.Category);
        }

        #endregion

        #region DirectQuery Tests

        [TestMethod]
        public void GetOperatorInfo_DirectQueryResult_ReturnsDirectQuery()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("DirectQueryResult");

            // Assert
            Assert.IsNotNull(info);
            Assert.AreEqual(EngineType.DirectQuery, info.Engine);
        }

        #endregion

        #region Unknown Operator Tests

        [TestMethod]
        public void GetOperatorInfo_UnknownOperator_ReturnsDefaultInfo()
        {
            // Act
            var info = DaxOperatorDictionary.GetOperatorInfo("SomeCompletelyNewOperator");

            // Assert
            Assert.IsNotNull(info, "Should return a default info object, not null");
            Assert.AreEqual(EngineType.Unknown, info.Engine);
        }

        #endregion
    }
}
