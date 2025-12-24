using System.Linq;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.VisualQueryPlan
{
    /// <summary>
    /// Unit tests for PlanNodeViewModel, particularly operator name extraction.
    /// </summary>
    [TestClass]
    public class PlanNodeViewModelTests
    {
        #region GetOperationName Tests - Various Operation String Formats

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
        public void OperatorName_MeasureReference_ExtractsCorrectly()
        {
            // Arrange - Measure reference format from logical plan
            var node = CreateNodeWithOperation("[Internet Sales Amount]: ScaLogOp MeasureRef='Internet Sales Amount'");

            // Act
            var operatorName = node.OperatorName;

            // Assert
            Assert.AreEqual("[Internet", operatorName,
                "Note: This test documents current behavior - measure references starting with [ need special handling");
        }

        #endregion

        #region Display Text Tests

        [TestMethod]
        public void DisplayText_LongOperatorName_Truncates()
        {
            // Arrange
            var node = CreateNodeWithOperation("VeryLongOperatorNameThatExceedsTwentyFiveCharacters: Details");

            // Act
            var displayText = node.DisplayText;

            // Assert
            Assert.IsTrue(displayText.Length <= 25, "Display text should be truncated to 25 chars");
            Assert.IsTrue(displayText.EndsWith("..."), "Should end with ellipsis when truncated");
        }

        [TestMethod]
        public void DisplayText_ShortOperatorName_NotTruncated()
        {
            // Arrange
            var node = CreateNodeWithOperation("AddColumns: Details");

            // Act
            var displayText = node.DisplayText;

            // Assert
            Assert.IsFalse(displayText.EndsWith("..."), "Should not truncate short names");
        }

        #endregion

        #region Engine Badge Tests

        [TestMethod]
        public void EngineBadge_StorageEngine_ReturnsSE()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Scan_Vertipaq: Test",
                EngineType = EngineType.StorageEngine
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("SE", node.EngineBadge);
            Assert.IsTrue(node.HasEngineBadge);
        }

        [TestMethod]
        public void EngineBadge_FormulaEngine_ReturnsFE()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "AddColumns: Test",
                EngineType = EngineType.FormulaEngine
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("FE", node.EngineBadge);
            Assert.IsTrue(node.HasEngineBadge);
        }

        [TestMethod]
        public void EngineBadge_Unknown_ReturnsEmpty()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "SomeOp: Test",
                EngineType = EngineType.Unknown
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("", node.EngineBadge);
            Assert.IsFalse(node.HasEngineBadge);
        }

        #endregion

        #region Timing Display Tests

        [TestMethod]
        public void DurationDisplay_WithValue_FormatsCorrectly()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                DurationMs = 1234
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("1,234 ms", node.DurationDisplay);
            Assert.IsTrue(node.HasDuration);
        }

        [TestMethod]
        public void DurationDisplay_WithoutValue_ReturnsEmpty()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                DurationMs = null
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("", node.DurationDisplay);
            Assert.IsFalse(node.HasDuration);
        }

        [TestMethod]
        public void ParallelismDisplay_WithValue_FormatsCorrectly()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Parallelism = 16
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("x16 parallelism", node.ParallelismDisplay);
            Assert.IsTrue(node.HasParallelism);
        }

        [TestMethod]
        public void RecordsWithSourceDisplay_FromServerTiming_ShowsSource()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 3655,
                RecordsSource = "ServerTiming"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("3,655 (from ServerTiming)", node.RecordsWithSourceDisplay);
        }

        [TestMethod]
        public void RecordsWithSourceDisplay_FromPlan_NoAnnotation()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 1000,
                RecordsSource = "Plan"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("1,000", node.RecordsWithSourceDisplay);
        }

        #endregion

        #region Color Scheme Tests - Row Count Severity

        [TestMethod]
        public void RowCountSeverity_NoRecords_ReturnsNone()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = null
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("None", node.RowCountSeverity);
        }

        [TestMethod]
        public void RowCountSeverity_ZeroRecords_ReturnsNone()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 0
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("None", node.RowCountSeverity);
        }

        [TestMethod]
        public void RowCountSeverity_SmallRecordCount_ReturnsFine()
        {
            // Arrange - 1000 records is under 10K threshold
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 1000
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("Fine", node.RowCountSeverity);
        }

        [TestMethod]
        public void RowCountSeverity_MediumRecordCount_ReturnsWarning()
        {
            // Arrange - 50K records is between 10K and 100K
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 50000
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("Warning", node.RowCountSeverity);
        }

        [TestMethod]
        public void RowCountSeverity_LargeRecordCount_ReturnsCritical()
        {
            // Arrange - 500K records exceeds 100K threshold
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 500000
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("Critical", node.RowCountSeverity);
        }

        #endregion

        #region Color Scheme Tests - Row Count Color

        [TestMethod]
        public void RowCountColor_DefaultSeverity_ReturnsDarkGray()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 100  // Fine severity
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.RowCountColor as System.Windows.Media.SolidColorBrush;

            // Assert - Dark gray (80, 80, 80)
            Assert.IsNotNull(color);
            Assert.AreEqual(80, color.Color.R);
            Assert.AreEqual(80, color.Color.G);
            Assert.AreEqual(80, color.Color.B);
        }

        [TestMethod]
        public void RowCountColor_WarningSeverity_ReturnsMutedOrange()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 50000  // Warning severity
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.RowCountColor as System.Windows.Media.SolidColorBrush;

            // Assert - Muted orange (200, 120, 0)
            Assert.IsNotNull(color);
            Assert.AreEqual(200, color.Color.R);
            Assert.AreEqual(120, color.Color.G);
            Assert.AreEqual(0, color.Color.B);
        }

        [TestMethod]
        public void RowCountColor_CriticalSeverity_ReturnsMutedRed()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 500000  // Critical severity
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.RowCountColor as System.Windows.Media.SolidColorBrush;

            // Assert - Muted red (180, 40, 40)
            Assert.IsNotNull(color);
            Assert.AreEqual(180, color.Color.R);
            Assert.AreEqual(40, color.Color.G);
            Assert.AreEqual(40, color.Color.B);
        }

        #endregion

        #region Color Scheme Tests - Edge Color

        [TestMethod]
        public void EdgeColor_DefaultSeverity_ReturnsNeutralGray()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 100  // Fine severity
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.EdgeColor as System.Windows.Media.SolidColorBrush;

            // Assert - Neutral gray (140, 140, 140)
            Assert.IsNotNull(color);
            Assert.AreEqual(140, color.Color.R);
            Assert.AreEqual(140, color.Color.G);
            Assert.AreEqual(140, color.Color.B);
        }

        [TestMethod]
        public void EdgeColor_WarningSeverity_ReturnsMutedOrange()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 50000  // Warning severity
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.EdgeColor as System.Windows.Media.SolidColorBrush;

            // Assert - Muted orange (200, 140, 60)
            Assert.IsNotNull(color);
            Assert.AreEqual(200, color.Color.R);
            Assert.AreEqual(140, color.Color.G);
            Assert.AreEqual(60, color.Color.B);
        }

        [TestMethod]
        public void EdgeColor_CriticalSeverity_ReturnsMutedRed()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 500000  // Critical severity
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.EdgeColor as System.Windows.Media.SolidColorBrush;

            // Assert - Muted red (180, 80, 80)
            Assert.IsNotNull(color);
            Assert.AreEqual(180, color.Color.R);
            Assert.AreEqual(80, color.Color.G);
            Assert.AreEqual(80, color.Color.B);
        }

        #endregion

        #region Color Scheme Tests - Background Brush

        [TestMethod]
        public void BackgroundBrush_StorageEngine_ReturnsLightGreenTint()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Scan_Vertipaq: Test",
                EngineType = EngineType.StorageEngine
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.BackgroundBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Very light green tint (245, 250, 245)
            Assert.IsNotNull(color);
            Assert.AreEqual(245, color.Color.R);
            Assert.AreEqual(250, color.Color.G);
            Assert.AreEqual(245, color.Color.B);
        }

        [TestMethod]
        public void BackgroundBrush_FormulaEngine_ReturnsLightPurpleTint()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "AddColumns: Test",
                EngineType = EngineType.FormulaEngine
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.BackgroundBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Very light purple tint (250, 248, 252)
            Assert.IsNotNull(color);
            Assert.AreEqual(250, color.Color.R);
            Assert.AreEqual(248, color.Color.G);
            Assert.AreEqual(252, color.Color.B);
        }

        [TestMethod]
        public void BackgroundBrush_UnknownEngine_ReturnsNearWhite()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "SomeOp: Test",
                EngineType = EngineType.Unknown
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.BackgroundBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Near white (248, 248, 248)
            Assert.IsNotNull(color);
            Assert.AreEqual(248, color.Color.R);
            Assert.AreEqual(248, color.Color.G);
            Assert.AreEqual(248, color.Color.B);
        }

        [TestMethod]
        public void BackgroundBrush_WithErrorIssue_ReturnsLightRed()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                EngineType = EngineType.StorageEngine
            };
            enrichedNode.Issues.Add(new PerformanceIssue
            {
                IssueType = IssueType.ExcessiveMaterialization,
                Severity = IssueSeverity.Error,
                AffectedNodeId = 1,
                Description = "Test error"
            });
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.BackgroundBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Very light red (255, 235, 235)
            Assert.IsNotNull(color);
            Assert.AreEqual(255, color.Color.R);
            Assert.AreEqual(235, color.Color.G);
            Assert.AreEqual(235, color.Color.B);
        }

        [TestMethod]
        public void BackgroundBrush_WithWarningIssue_ReturnsLightOrange()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                EngineType = EngineType.StorageEngine
            };
            enrichedNode.Issues.Add(new PerformanceIssue
            {
                IssueType = IssueType.ExcessiveMaterialization,
                Severity = IssueSeverity.Warning,
                AffectedNodeId = 1,
                Description = "Test warning"
            });
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            var color = node.BackgroundBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Very light orange (255, 245, 230)
            Assert.IsNotNull(color);
            Assert.AreEqual(255, color.Color.R);
            Assert.AreEqual(245, color.Color.G);
            Assert.AreEqual(230, color.Color.B);
        }

        #endregion

        #region Color Scheme Tests - Border Brush

        [TestMethod]
        public void BorderBrush_StorageEngine_NotSelected_ReturnsLightGreenGray()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Scan_Vertipaq: Test",
                EngineType = EngineType.StorageEngine
            };
            var node = new PlanNodeViewModel(enrichedNode);
            node.IsSelected = false;

            // Act
            var color = node.BorderBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Light green-gray (180, 200, 180)
            Assert.IsNotNull(color);
            Assert.AreEqual(180, color.Color.R);
            Assert.AreEqual(200, color.Color.G);
            Assert.AreEqual(180, color.Color.B);
        }

        [TestMethod]
        public void BorderBrush_FormulaEngine_NotSelected_ReturnsLightPurpleGray()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "AddColumns: Test",
                EngineType = EngineType.FormulaEngine
            };
            var node = new PlanNodeViewModel(enrichedNode);
            node.IsSelected = false;

            // Act
            var color = node.BorderBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Light purple-gray (200, 180, 200)
            Assert.IsNotNull(color);
            Assert.AreEqual(200, color.Color.R);
            Assert.AreEqual(180, color.Color.G);
            Assert.AreEqual(200, color.Color.B);
        }

        [TestMethod]
        public void BorderBrush_WhenSelected_ReturnsBlue()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                EngineType = EngineType.StorageEngine
            };
            var node = new PlanNodeViewModel(enrichedNode);
            node.IsSelected = true;

            // Act
            var color = node.BorderBrush as System.Windows.Media.SolidColorBrush;

            // Assert - Blue selection (0, 120, 215)
            Assert.IsNotNull(color);
            Assert.AreEqual(0, color.Color.R);
            Assert.AreEqual(120, color.Color.G);
            Assert.AreEqual(215, color.Color.B);
        }

        #endregion

        #region Measure Formula Tests

        [TestMethod]
        public void MeasureFormula_SetValue_UpdatesProperty()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "[Total Sales]: ScaLogOp MeasureRef"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            node.MeasureFormula = "SUMX(Sales, Sales[Qty] * Sales[Price])";

            // Assert
            Assert.AreEqual("SUMX(Sales, Sales[Qty] * Sales[Price])", node.MeasureFormula);
            Assert.IsTrue(node.HasMeasureFormula);
        }

        [TestMethod]
        public void MeasureFormula_NullValue_HasMeasureFormulaReturnsFalse()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "[Total Sales]: ScaLogOp MeasureRef"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.IsNull(node.MeasureFormula);
            Assert.IsFalse(node.HasMeasureFormula);
        }

        [TestMethod]
        public void MeasureFormula_EmptyValue_HasMeasureFormulaReturnsFalse()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "[Total Sales]: ScaLogOp MeasureRef"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act
            node.MeasureFormula = "";

            // Assert
            Assert.IsFalse(node.HasMeasureFormula);
        }

        [TestMethod]
        public void MeasureReference_WithLogOpPattern_ExtractsMeasureName()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Sum_Vertipaq<ScaLogOp>: LogOp=Sum_Vertipaq Currency"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[Currency]", node.MeasureReference);
            Assert.IsTrue(node.HasMeasureReference);
        }

        [TestMethod]
        public void MeasureReference_WithBracketedName_ExtractsMeasureName()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "[Internet Sales Amount]: ScaLogOp MeasureRef='Internet Sales Amount'"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[Internet Sales Amount]", node.MeasureReference);
            Assert.IsTrue(node.HasMeasureReference);
        }

        [TestMethod]
        public void MeasureReference_NoMeasure_ReturnsEmpty()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Scan_Vertipaq: RelLogOp"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual(string.Empty, node.MeasureReference);
            Assert.IsFalse(node.HasMeasureReference);
        }

        [TestMethod]
        public void MeasureReference_WithMeasureRefEquals_ExtractsMeasureName()
        {
            // Arrange - Calculate operator with MeasureRef= pattern
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Calculate: ScaLogOp MeasureRef=[Filtered Margin] DependOnCols()() Currency DominantValue=BLANK"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[Filtered Margin]", node.MeasureReference);
            Assert.IsTrue(node.HasMeasureReference);
        }

        [TestMethod]
        public void MeasureReference_WithDoubleQuoteBrackets_ExtractsMeasureName()
        {
            // Arrange - Physical plan format (''[MeasureName])
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "AddColumns: IterPhyOp LogOp=AddColumns IterCols(0)(''[Filtered Margin])"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[Filtered Margin]", node.MeasureReference);
            Assert.IsTrue(node.HasMeasureReference);
        }

        [TestMethod]
        public void MeasureReference_ColumnReference_NotExtracted()
        {
            // Arrange - Column reference with table name should not match
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Scan_Vertipaq: 'Sales'[Amount] #Records=1000"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert - Should NOT extract [Amount] as a measure (it's a column)
            // The pattern should exclude 'Table'[Column] format
            Assert.AreEqual(string.Empty, node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_WithSpacesInName_ExtractsFullName()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Calculate: ScaLogOp MeasureRef=[Total Sales Amount YTD]"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[Total Sales Amount YTD]", node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_WithVertipaqAggregation_ExtractsMeasure()
        {
            // Arrange - Vertipaq aggregation pattern
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Sum_Vertipaq<ScaLogOp>: LogOp=Sum_Vertipaq TotalSales DependOnCols()"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[TotalSales]", node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_ScanVertipaq_WithIterCols_NotExtracted()
        {
            // Arrange - Scan_Vertipaq with IterCols should NOT extract IterCols as measure
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Scan_Vertipaq IterCols(0)('Customer'[First Name]) #Records=670"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert - Should NOT match IterCols... as a measure name
            Assert.AreEqual(string.Empty, node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_ScanVertipaq_WithColumnOnly_NotExtracted()
        {
            // Arrange - Scan with column reference only
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Scan_Vertipaq: RelLogOp 'Product'[Color] #Records=500"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual(string.Empty, node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_LogOpSumVertipaq_ExtractsSimpleName()
        {
            // Arrange - Sum aggregation with simple measure name
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Sum_Vertipaq: LogOp=Sum_Vertipaq Amount #Records=100"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[Amount]", node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_LogOpCountVertipaq_ExtractsMeasure()
        {
            // Arrange - Count aggregation
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Count_Vertipaq: LogOp=Count_Vertipaq OrderCount DependOnCols()"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[OrderCount]", node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_PhysicalPlanWithQuotedMeasure_ExtractsMeasure()
        {
            // Arrange - Physical plan format with double-single-quote prefix
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "AddColumns: IterPhyOp LogOp=AddColumns IterCols(0)(''[Total Revenue])"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("[Total Revenue]", node.MeasureReference);
        }

        [TestMethod]
        public void MeasureReference_ComplexOperation_NoFalsePositive()
        {
            // Arrange - Complex operation that might have false positives
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Filter: RelLogOp DependOnCols(0, 1)('Date'[Year], 'Date'[Month]) RequiredCols(2)"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert - Should not extract DependOnCols or column refs as measures
            Assert.AreEqual(string.Empty, node.MeasureReference);
        }

        #endregion

        #region Node Collapsing Tests

        [TestMethod]
        public void IsComparisonOperator_GreaterThan_ReturnsTrue()
        {
            // Arrange
            var node = CreateNodeWithOperation("GreaterThan: ScaLogOp Boolean");

            // Act & Assert
            Assert.IsTrue(node.IsComparisonOperator);
        }

        [TestMethod]
        public void IsComparisonOperator_LessThan_ReturnsTrue()
        {
            // Arrange
            var node = CreateNodeWithOperation("LessThan: ScaLogOp Boolean");

            // Act & Assert
            Assert.IsTrue(node.IsComparisonOperator);
        }

        [TestMethod]
        public void IsComparisonOperator_Equal_ReturnsTrue()
        {
            // Arrange
            var node = CreateNodeWithOperation("Equal: ScaLogOp Boolean");

            // Act & Assert
            Assert.IsTrue(node.IsComparisonOperator);
        }

        [TestMethod]
        public void IsComparisonOperator_ScanVertipaq_ReturnsFalse()
        {
            // Arrange
            var node = CreateNodeWithOperation("Scan_Vertipaq: RelLogOp");

            // Act & Assert
            Assert.IsFalse(node.IsComparisonOperator);
        }

        [TestMethod]
        public void IsValueOperator_Constant_ReturnsTrue()
        {
            // Arrange
            var node = CreateNodeWithOperation("Constant: ScaLogOp Integer 100");

            // Act & Assert
            Assert.IsTrue(node.IsValueOperator);
        }

        [TestMethod]
        public void IsValueOperator_ColValue_ReturnsTrue()
        {
            // Arrange
            var node = CreateNodeWithOperation("ColValue: ScaLogOp 'Sales'[Amount]");

            // Act & Assert
            Assert.IsTrue(node.IsValueOperator);
        }

        [TestMethod]
        public void ComparisonSymbol_GreaterThan_ReturnsCorrectSymbol()
        {
            // Arrange
            var node = CreateNodeWithOperation("GreaterThan: ScaLogOp Boolean");

            // Act & Assert
            Assert.AreEqual(">", node.ComparisonSymbol);
        }

        [TestMethod]
        public void ComparisonSymbol_LessOrEqualTo_ReturnsCorrectSymbol()
        {
            // Arrange
            var node = CreateNodeWithOperation("LessOrEqualTo: ScaLogOp Boolean");

            // Act & Assert
            Assert.AreEqual("<=", node.ComparisonSymbol);
        }

        [TestMethod]
        public void ComparisonSymbol_NotEqual_ReturnsCorrectSymbol()
        {
            // Arrange
            var node = CreateNodeWithOperation("NotEqual: ScaLogOp Boolean");

            // Act & Assert
            Assert.AreEqual("<>", node.ComparisonSymbol);
        }

        [TestMethod]
        public void CanCollapseComparison_WithTwoValueChildren_ReturnsTrue()
        {
            // Arrange
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "GreaterThan: ScaLogOp Boolean"
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            var child1Enriched = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "ColValue: ScaLogOp 'Sales'[Amount]"
            };
            var child1 = new PlanNodeViewModel(child1Enriched);

            var child2Enriched = new EnrichedPlanNode
            {
                NodeId = 3,
                Operation = "Constant: ScaLogOp Integer 100"
            };
            var child2 = new PlanNodeViewModel(child2Enriched);

            parentNode.Children.Add(child1);
            parentNode.Children.Add(child2);

            // Act & Assert
            Assert.IsTrue(parentNode.CanCollapseComparison);
        }

        [TestMethod]
        public void CanCollapseComparison_WithThreeChildren_ReturnsFalse()
        {
            // Arrange
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "GreaterThan: ScaLogOp Boolean"
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            // Add three children
            for (int i = 0; i < 3; i++)
            {
                var childEnriched = new EnrichedPlanNode
                {
                    NodeId = i + 2,
                    Operation = "Constant: ScaLogOp Integer"
                };
                parentNode.Children.Add(new PlanNodeViewModel(childEnriched));
            }

            // Act & Assert
            Assert.IsFalse(parentNode.CanCollapseComparison);
        }

        [TestMethod]
        public void CanCollapseComparison_NonComparison_ReturnsFalse()
        {
            // Arrange - AddColumns is not a comparison operator
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "AddColumns: IterPhyOp"
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            // Add two children
            for (int i = 0; i < 2; i++)
            {
                var childEnriched = new EnrichedPlanNode
                {
                    NodeId = i + 2,
                    Operation = "Constant: ScaLogOp Integer"
                };
                parentNode.Children.Add(new PlanNodeViewModel(childEnriched));
            }

            // Act & Assert
            Assert.IsFalse(parentNode.CanCollapseComparison);
        }

        [TestMethod]
        public void CollapseIfPossible_WithValidComparison_CollapsesNode()
        {
            // Arrange
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "GreaterThan: ScaLogOp Boolean"
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            var child1Enriched = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "ColValue: ScaLogOp 'Sales'[Amount]"
            };
            var child1 = new PlanNodeViewModel(child1Enriched);

            var child2Enriched = new EnrichedPlanNode
            {
                NodeId = 3,
                Operation = "Constant: ScaLogOp Integer 100"
            };
            var child2 = new PlanNodeViewModel(child2Enriched);

            parentNode.Children.Add(child1);
            parentNode.Children.Add(child2);

            // Act
            parentNode.CollapseIfPossible();

            // Assert
            Assert.IsTrue(parentNode.IsCollapsed);
            Assert.AreEqual(0, parentNode.Children.Count, "Visible children should be empty");
            Assert.AreEqual(2, parentNode.CollapsedChildren.Count, "Collapsed children should be stored");
            Assert.IsTrue(parentNode.HasCollapsedChildren);
        }

        [TestMethod]
        public void VisibleChildren_WhenCollapsed_ReturnsEmpty()
        {
            // Arrange
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "GreaterThan: ScaLogOp Boolean"
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            var child1Enriched = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "ColValue: ScaLogOp 'Sales'[Amount]"
            };
            var child2Enriched = new EnrichedPlanNode
            {
                NodeId = 3,
                Operation = "Constant: ScaLogOp Integer 100"
            };

            parentNode.Children.Add(new PlanNodeViewModel(child1Enriched));
            parentNode.Children.Add(new PlanNodeViewModel(child2Enriched));

            // Act
            parentNode.CollapseIfPossible();

            // Assert
            Assert.AreEqual(0, parentNode.VisibleChildren.Count());
        }

        [TestMethod]
        public void VisibleChildren_WhenNotCollapsed_ReturnsAllChildren()
        {
            // Arrange
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "AddColumns: IterPhyOp"  // Not a comparison - won't collapse
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            var child1Enriched = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "SomeOp: ScaLogOp"
            };

            parentNode.Children.Add(new PlanNodeViewModel(child1Enriched));

            // Act & Assert
            Assert.AreEqual(1, parentNode.VisibleChildren.Count());
            Assert.IsFalse(parentNode.IsCollapsed);
        }

        [TestMethod]
        public void CollapsedDisplayText_ShowsSimplifiedComparison()
        {
            // Arrange
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "GreaterThan: ScaLogOp Boolean"
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            var child1Enriched = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "ColValue: ScaLogOp 'Sales'[Amount]"
            };
            var child2Enriched = new EnrichedPlanNode
            {
                NodeId = 3,
                Operation = "Constant: ScaLogOp Integer 100"
            };

            parentNode.Children.Add(new PlanNodeViewModel(child1Enriched));
            parentNode.Children.Add(new PlanNodeViewModel(child2Enriched));

            // Act
            parentNode.CollapseIfPossible();

            // Assert - Should show "[Amount] > 100" or similar
            Assert.IsTrue(parentNode.CollapsedDisplayText.Contains(">"), "Should contain comparison operator");
            Assert.IsTrue(parentNode.CollapsedDisplayText.Contains("Amount") || parentNode.CollapsedDisplayText.Contains("["),
                "Should contain column reference");
        }

        [TestMethod]
        public void DisplayText_WhenCollapsed_ShowsCollapsedText()
        {
            // Arrange
            var parentEnriched = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "GreaterThan: ScaLogOp Boolean"
            };
            var parentNode = new PlanNodeViewModel(parentEnriched);

            var child1Enriched = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "ColValue: ScaLogOp 'Sales'[Amount]"
            };
            var child2Enriched = new EnrichedPlanNode
            {
                NodeId = 3,
                Operation = "Constant: ScaLogOp Integer 100"
            };

            parentNode.Children.Add(new PlanNodeViewModel(child1Enriched));
            parentNode.Children.Add(new PlanNodeViewModel(child2Enriched));

            // Act
            parentNode.CollapseIfPossible();
            var displayText = parentNode.DisplayText;

            // Assert - DisplayText should reflect the collapsed state
            Assert.IsTrue(displayText.Contains(">") || displayText.Contains("Amount"),
                $"DisplayText should show collapsed comparison, got: {displayText}");
        }

        #endregion

        #region Edge Thickness Tests

        [TestMethod]
        public void EdgeThickness_NoRecords_ReturnsMinimum()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = null
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual(1.0, node.EdgeThickness);
        }

        [TestMethod]
        public void EdgeThickness_ZeroRecords_ReturnsMinimum()
        {
            // Arrange
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 0
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual(1.0, node.EdgeThickness);
        }

        [TestMethod]
        public void EdgeThickness_SmallRecordCount_ReturnsSmallValue()
        {
            // Arrange - 10 rows should give log10(11) * 2 ≈ 2.08
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 10
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert - should be between 2 and 3
            Assert.IsTrue(node.EdgeThickness > 2.0);
            Assert.IsTrue(node.EdgeThickness < 3.0);
        }

        [TestMethod]
        public void EdgeThickness_LargeRecordCount_IsCappedAt10()
        {
            // Arrange - 10 million rows
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 10000000
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert - should be capped at 10
            Assert.AreEqual(10.0, node.EdgeThickness);
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
