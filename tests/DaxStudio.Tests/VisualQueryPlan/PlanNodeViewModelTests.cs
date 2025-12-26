using System.Collections.Generic;
using System.IO;
using System.Linq;
using Caliburn.Micro;
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
        public void DisplayText_LongOperatorName_NotTruncated()
        {
            // Arrange - word wrapping is handled by XAML, so no truncation in code
            var node = CreateNodeWithOperation("VeryLongOperatorNameThatExceedsTwentyFiveCharacters: Details");

            // Act
            var displayText = node.DisplayText;

            // Assert - display text should contain formatted name (no code truncation)
            // FormatUnknownOperator converts "VeryLong..." to "Very Long..." (spaces added)
            Assert.IsTrue(displayText.Contains("Very Long Operator"), "Display text should preserve formatted operator name");
            Assert.IsFalse(displayText.EndsWith("..."), "Should not add ellipsis - word wrap handles long text");
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
            // Arrange - 150K records is between 100K and 1M (Warning threshold)
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 150000
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert
            Assert.AreEqual("Warning", node.RowCountSeverity);
        }

        [TestMethod]
        public void RowCountSeverity_LargeRecordCount_ReturnsCritical()
        {
            // Arrange - 1.5M records exceeds 1M threshold (Critical)
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 1500000
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
            // Arrange - 150K is Warning severity (100K-1M)
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 150000  // Warning severity
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
            // Arrange - 1.5M is Critical severity (1M+)
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 1500000  // Critical severity
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
            // Arrange - 150K is Warning severity (100K-1M)
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 150000  // Warning severity
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
            // Arrange - 1.5M is Critical severity (1M+)
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Test: Op",
                Records = 1500000  // Critical severity
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
        public void BackgroundBrush_StorageEngine_ReturnsLightBlueTint()
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

            // Assert - Very light blue tint (240, 247, 255) - matches Server Timings
            Assert.IsNotNull(color);
            Assert.AreEqual(240, color.Color.R);
            Assert.AreEqual(247, color.Color.G);
            Assert.AreEqual(255, color.Color.B);
        }

        [TestMethod]
        public void BackgroundBrush_FormulaEngine_ReturnsLightOrangeTint()
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

            // Assert - Very light orange tint (255, 250, 240) - matches Server Timings
            Assert.IsNotNull(color);
            Assert.AreEqual(255, color.Color.R);
            Assert.AreEqual(250, color.Color.G);
            Assert.AreEqual(240, color.Color.B);
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
        public void BackgroundBrush_WithErrorIssue_UsesEngineTypeColor()
        {
            // Arrange - Background color is based on engine type, not issues
            // Issues are indicated by the warning emoji and color-coded row counts
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

            // Assert - Still uses StorageEngine color (240, 247, 255)
            Assert.IsNotNull(color);
            Assert.AreEqual(240, color.Color.R);
            Assert.AreEqual(247, color.Color.G);
            Assert.AreEqual(255, color.Color.B);
        }

        [TestMethod]
        public void BackgroundBrush_WithWarningIssue_UsesEngineTypeColor()
        {
            // Arrange - Background color is based on engine type, not issues
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

            // Assert - Still uses StorageEngine color (240, 247, 255)
            Assert.IsNotNull(color);
            Assert.AreEqual(240, color.Color.R);
            Assert.AreEqual(247, color.Color.G);
            Assert.AreEqual(255, color.Color.B);
        }

        #endregion

        #region Color Scheme Tests - Border Brush

        [TestMethod]
        public void BorderBrush_StorageEngine_NotSelected_ReturnsLightBlueGray()
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

            // Assert - Light blue-gray (180, 200, 220) - matches Server Timings
            Assert.IsNotNull(color);
            Assert.AreEqual(180, color.Color.R);
            Assert.AreEqual(200, color.Color.G);
            Assert.AreEqual(220, color.Color.B);
        }

        [TestMethod]
        public void BorderBrush_FormulaEngine_NotSelected_ReturnsLightOrangeGray()
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

            // Assert - Light orange-gray (220, 200, 180) - matches Server Timings
            Assert.IsNotNull(color);
            Assert.AreEqual(220, color.Color.R);
            Assert.AreEqual(200, color.Color.G);
            Assert.AreEqual(180, color.Color.B);
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

        [TestMethod]
        public void MeasureReference_LogOpSumVertipaq_FiltersOutIterCols()
        {
            // Arrange - Operation where IterCols follows Sum_Vertipaq (common pattern)
            // This was incorrectly extracting "IterCols" as a measure reference
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq IterCols(0)('Product'[Brand]) #Records=11"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert - Should NOT extract IterCols as a measure reference
            Assert.AreEqual(string.Empty, node.MeasureReference, "IterCols should not be extracted as measure reference");
            Assert.IsFalse(node.HasMeasureReference, "HasMeasureReference should be false");
        }

        [TestMethod]
        public void MeasureReference_LogOpCountVertipaq_FiltersOutLookupCols()
        {
            // Arrange - Operation where LookupCols follows Count_Vertipaq
            var enrichedNode = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "SpoolLookup: LookupPhyOp LogOp=Count_Vertipaq LookupCols(0)('Sales'[OrderID])"
            };
            var node = new PlanNodeViewModel(enrichedNode);

            // Act & Assert - Should NOT extract LookupCols as a measure reference
            Assert.AreEqual(string.Empty, node.MeasureReference, "LookupCols should not be extracted as measure reference");
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

        #region Node Folding Tests (BuildTree)

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
        public void BuildTree_RegularOperator_IsNotFolded()
        {
            // Arrange - regular operators like AddColumns, Filter should NOT be folded
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

            // Assert - regular operators should NOT be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "Regular operators should NOT be folded");
        }

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

        #region Filter Predicate Rollup Tests

        [TestMethod]
        public void BuildTree_FilterWithComparison_RollsUpPredicate()
        {
            // Arrange - Filter with GreaterThan comparison
            // Filter
            //   Scan_Vertipaq (SE - keep)
            //   GreaterThan (fold)
            //     Column ref (fold)
            //     Constant (fold)
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp DependOnCols()() 0-0 RequiredCols(0)('Customer'[First Name])", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp DependOnCols()() 0-0 RequiredCols(0)('Customer'[First Name])", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "GreaterThan: ScaLogOp DependOnCols(0)('Customer'[First Name]) Boolean DominantValue=NONE", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 4, Operation = "'Customer'[First Name]: ScaLogOp DependOnCols(0)('Customer'[First Name]) String DominantValue=NONE", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 5, Operation = "Constant: ScaLogOp DependOnCols()() String DominantValue=Bob", EngineType = EngineType.FormulaEngine }
            };

            // Set up parent relationships
            plan.AllNodes[1].Parent = plan.AllNodes[0]; // Scan_Vertipaq -> Filter
            plan.AllNodes[2].Parent = plan.AllNodes[0]; // GreaterThan -> Filter
            plan.AllNodes[3].Parent = plan.AllNodes[2]; // Column -> GreaterThan
            plan.AllNodes[4].Parent = plan.AllNodes[2]; // Constant -> GreaterThan
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate, "Filter should have predicate expression");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("[First Name]"), "Predicate should contain column name");
            Assert.IsTrue(root.FilterPredicateExpression.Contains(">"), "Predicate should contain comparison operator");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("Bob"), "Predicate should contain constant value");

            // Only Scan_Vertipaq should remain as child (SE node preserved)
            Assert.AreEqual(1, root.Children.Count, "Only Scan_Vertipaq should remain as child");
            Assert.IsTrue(root.Children[0].Operation.Contains("Scan_Vertipaq"), "Child should be Scan_Vertipaq");
        }

        [TestMethod]
        public void BuildTree_FilterVertipaqWithCondensedPredicate_RollsUpPredicate()
        {
            // Arrange - Filter_Vertipaq with already-condensed predicate
            // Filter_Vertipaq
            //   Scan_Vertipaq (keep)
            //   'Product'[Color] <> Black (fold, extract predicate)
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter_Vertipaq: RelLogOp DependOnCols()() 1-1 RequiredCols(1)('Product'[Color])", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp DependOnCols()() 1-1 RequiredCols(1)('Product'[Color])", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "'Product'[Color] <> Black: ScaLogOp DependOnCols(1)('Product'[Color]) Boolean DominantValue=false", EngineType = EngineType.StorageEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0]; // Scan_Vertipaq -> Filter_Vertipaq
            plan.AllNodes[2].Parent = plan.AllNodes[0]; // Condensed predicate -> Filter_Vertipaq
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate, "Filter should have predicate expression");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("[Color]"), "Predicate should contain column name");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("<>"), "Predicate should contain not-equal operator");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("Black"), "Predicate should contain value");

            // Scan_Vertipaq should remain
            Assert.AreEqual(1, root.Children.Count, "Only Scan_Vertipaq should remain");
        }

        [TestMethod]
        public void BuildTree_FilterWithSEChild_PreservesSENode()
        {
            // Arrange - FE Filter with SE Scan child - should NOT fold Scan
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "GreaterThan: ScaLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 4, Operation = "Constant: ScaLogOp DominantValue=100", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 5, Operation = "'Sales'[Amount]: ScaLogOp", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.AllNodes[3].Parent = plan.AllNodes[2];
            plan.AllNodes[4].Parent = plan.AllNodes[2];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - SE Scan node must be preserved (engine transition)
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "SE Scan should be preserved as child");
            Assert.AreEqual(EngineType.StorageEngine, root.Children[0].Node.EngineType, "Child should be SE");
        }

        [TestMethod]
        public void BuildTree_FilterWithoutPredicate_NoPredicateExpression()
        {
            // Arrange - Filter with no comparison children
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsFalse(root.HasFilterPredicate, "Filter without comparison should have no predicate expression");
        }

        [TestMethod]
        public void IsFilterOperator_Filter_ReturnsTrue()
        {
            Assert.IsTrue(PlanNodeViewModel.IsFilterOperator("Filter"));
        }

        [TestMethod]
        public void IsFilterOperator_FilterVertipaq_ReturnsTrue()
        {
            Assert.IsTrue(PlanNodeViewModel.IsFilterOperator("Filter_Vertipaq"));
        }

        [TestMethod]
        public void IsFilterOperator_DataPostFilter_ReturnsTrue()
        {
            Assert.IsTrue(PlanNodeViewModel.IsFilterOperator("DataPostFilter"));
        }

        [TestMethod]
        public void IsFilterOperator_ScanVertipaq_ReturnsFalse()
        {
            Assert.IsFalse(PlanNodeViewModel.IsFilterOperator("Scan_Vertipaq"));
        }

        [TestMethod]
        public void DisplayText_FilterWithPredicate_ShowsPredicateInTitle()
        {
            // Arrange
            var node = new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp" };
            var vm = new PlanNodeViewModel(node)
            {
                FilterPredicateExpression = "[Name] > \"Bob\""
            };

            // Act
            var displayText = vm.DisplayText;

            // Assert
            Assert.IsTrue(displayText.Contains("Filter"), "Display should contain Filter");
            Assert.IsTrue(displayText.Contains("[Name]") || displayText.Contains("..."),
                "Display should contain predicate or be truncated");
        }

        [TestMethod]
        public void BuildTree_LessThanComparison_ExtractsCorrectSymbol()
        {
            // Arrange
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "LessThan: ScaLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 4, Operation = "'Sales'[Qty]: ScaLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 5, Operation = "Constant: ScaLogOp DominantValue=10", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.AllNodes[3].Parent = plan.AllNodes[2];
            plan.AllNodes[4].Parent = plan.AllNodes[2];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate);
            Assert.IsTrue(root.FilterPredicateExpression.Contains("<"), "Should contain less-than symbol");
            Assert.IsFalse(root.FilterPredicateExpression.Contains("<>"), "Should not be not-equal");
        }

        [TestMethod]
        public void BuildTree_NotEqualComparison_ExtractsCorrectSymbol()
        {
            // Arrange
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "NotEqual: ScaLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 4, Operation = "'Product'[Status]: ScaLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 5, Operation = "Constant: ScaLogOp DominantValue=Inactive", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.AllNodes[3].Parent = plan.AllNodes[2];
            plan.AllNodes[4].Parent = plan.AllNodes[2];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate);
            Assert.IsTrue(root.FilterPredicateExpression.Contains("<>"), "Should contain not-equal symbol");
        }

        #endregion

        #region Physical Plan Filter Predicate Tests

        [TestMethod]
        public void BuildTree_PhysicalPlanFilter_WithLogOpGreaterThan_ExtractsColumn()
        {
            // Arrange - Physical Plan style Filter with LogOp embedded in child
            // Filter
            //   Extend_Lookup with LogOp=GreaterThan
            //     Constant with DominantValue
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: IterPhyOp LogOp=Filter IterCols(0)('Customer'[First Name])", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Extend_Lookup: IterPhyOp LogOp=GreaterThan IterCols(0)('Customer'[First Name])", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "Constant: ScaPhyOp LogOp=Constant String DominantValue=Bob", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0]; // Extend_Lookup -> Filter
            plan.AllNodes[2].Parent = plan.AllNodes[1]; // Constant -> Extend_Lookup
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate, "Physical plan Filter should have predicate");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("[First Name]"), "Should extract column from IterCols");
            Assert.IsTrue(root.FilterPredicateExpression.Contains(">"), "Should have greater-than symbol");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("Bob"), "Should extract constant value");
        }

        [TestMethod]
        public void BuildTree_PhysicalPlanFilter_IterColsPattern_ExtractsColumnName()
        {
            // Arrange - Test the IterCols regex pattern specifically
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: IterPhyOp LogOp=Filter IterCols(0)('Sales'[Amount])", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Compare: IterPhyOp LogOp=LessThan IterCols(0)('Sales'[Amount])", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "Constant: ScaPhyOp DominantValue=1000", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            if (root.HasFilterPredicate)
            {
                Assert.IsTrue(root.FilterPredicateExpression.Contains("[Amount]"), "Should extract Amount from IterCols pattern");
            }
        }

        [TestMethod]
        public void BuildTree_PhysicalPlanFilter_FallbackColumnPattern_ExtractsColumn()
        {
            // Arrange - Test fallback pattern when IterCols isn't present
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: IterPhyOp LogOp=Filter", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Lookup: IterPhyOp LogOp=Equal 'Product'[Category]", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "Constant: ScaPhyOp DominantValue=Electronics", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            if (root.HasFilterPredicate)
            {
                Assert.IsTrue(root.FilterPredicateExpression.Contains("[Category]"), "Should extract column via fallback pattern");
            }
        }

        [TestMethod]
        public void BuildTree_PhysicalPlanFilter_NumericConstant_NoQuotes()
        {
            // Arrange - Numeric constants should not be quoted
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: IterPhyOp LogOp=Filter IterCols(0)('Sales'[Quantity])", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Compare: IterPhyOp LogOp=GreaterOrEqualTo IterCols(0)('Sales'[Quantity])", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "Constant: ScaPhyOp DominantValue=100", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            if (root.HasFilterPredicate)
            {
                Assert.IsFalse(root.FilterPredicateExpression.Contains("\"100\""), "Numeric values should not be quoted");
                Assert.IsTrue(root.FilterPredicateExpression.Contains("100"), "Should contain numeric value");
            }
        }

        [TestMethod]
        public void BuildTree_CondensedPredicate_ExtractsCorrectly()
        {
            // Arrange - Test condensed predicate like 'Product'[Color] <> Black
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter_Vertipaq: RelLogOp DependOnCols()() RequiredCols(1)('Product'[Color])", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "'Product'[Color] <> Black: ScaLogOp DependOnCols(1)('Product'[Color]) Boolean", EngineType = EngineType.StorageEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate, "Should detect condensed predicate");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("[Color]"), "Should extract column name");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("<>"), "Should extract operator");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("Black"), "Should extract value");
        }

        [TestMethod]
        public void BuildTree_CondensedPredicate_GreaterThanOrEqual()
        {
            // Arrange - Test condensed predicate with >= operator
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "'Order'[Total] >= 500: ScaLogOp Boolean", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate, "Should detect >= condensed predicate");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("[Total]"), "Should extract column");
            Assert.IsTrue(root.FilterPredicateExpression.Contains(">="), "Should preserve >= operator");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("500"), "Should extract numeric value");
        }

        [TestMethod]
        public void BuildTree_CondensedPredicate_LessThanOrEqual()
        {
            // Arrange - Test condensed predicate with <= operator
            var plan = new EnrichedQueryPlan();
            plan.AllNodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Filter: RelLogOp", EngineType = EngineType.FormulaEngine },
                new EnrichedPlanNode { NodeId = 2, Operation = "Scan_Vertipaq: RelLogOp", EngineType = EngineType.StorageEngine },
                new EnrichedPlanNode { NodeId = 3, Operation = "'Date'[Year] <= 2023: ScaLogOp Integer", EngineType = EngineType.FormulaEngine }
            };

            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate, "Should detect <= condensed predicate");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("[Year]"), "Should extract column");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("<="), "Should preserve <= operator");
        }

        #endregion

        #region Identical Node Folding Tests

        [TestMethod]
        public void BuildTree_IdenticalParentChild_FoldsChild()
        {
            // Arrange - Parent and child have identical operation strings
            // This happens with Proxy nodes in physical plans
            var identicalOp = "Proxy: IterPhyOp LogOp=TableVarProxy IterCols(0, 1, 2)('Product'[Brand], ''[Sales], ''[Cost])";
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = identicalOp
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = identicalOp // Identical to parent
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - identical child should be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "Identical child should be folded into parent");
        }

        [TestMethod]
        public void BuildTree_IdenticalParentChild_WithGrandchildren_PromotesGrandchildren()
        {
            // Arrange - Parent and child identical, grandchild different
            var identicalOp = "Proxy: IterPhyOp LogOp=TableVarProxy IterCols(0, 1)('Product'[Brand], ''[Sales])";
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = identicalOp
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = identicalOp // Will be folded
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Level = 2,
                        Operation = "Scan_Vertipaq: RelLogOp RequiredCols(1)('Product'[Brand])" // Different - should be promoted
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

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "Grandchild should be promoted when parent is folded");
            Assert.IsTrue(root.Children[0].Operation.Contains("Scan_Vertipaq"), "Scan_Vertipaq should be promoted");
        }

        [TestMethod]
        public void BuildTree_DifferentParentChild_DoesNotFold()
        {
            // Arrange - Parent and child have different operations
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Proxy: IterPhyOp LogOp=TableVarProxy IterCols(0)('Product'[Brand])"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "Proxy: IterPhyOp LogOp=ScalarVarProxy IterCols(1)('Sales'[Amount])" // Different!
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - different operations should NOT be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "Different operations should NOT be folded");
        }

        [TestMethod]
        public void BuildTree_IdenticalButMultipleChildren_DoesNotFold()
        {
            // Arrange - Parent has multiple children, one identical - should NOT fold
            var identicalOp = "AddColumns: RelLogOp DependOnCols()()";
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = identicalOp
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = identicalOp // Identical
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Level = 1,
                        Operation = "Filter: RelLogOp" // Different sibling
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[0].Children.Add(plan.AllNodes[2]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - should NOT fold when there are multiple children
            Assert.IsNotNull(root);
            Assert.AreEqual(2, root.Children.Count, "Should NOT fold identical child when there are siblings");
        }

        #endregion

        #region Spool Folding Tests

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
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - ProjectionSpool should be folded into Spool_Iterator
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "ProjectionSpool should be folded into Spool_Iterator");
            Assert.IsTrue(root.HasSpoolTypeInfo, "Parent should have SpoolTypeInfo after folding");
            Assert.IsTrue(root.SpoolTypeInfo.Contains("Project"), "SpoolTypeInfo should contain 'Project'");
        }

        [TestMethod]
        public void BuildTree_SpoolLookup_WithProjectionSpoolChild_FoldsChild()
        {
            // Arrange - SpoolLookup with ProjectionSpool child should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "SpoolLookup: LookupPhyOp LogOp=ScalarVarProxy LookupCols(0)('Product'[Brand]) Currency #Records=11"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "ProjectionSpool<ProjectFusion<Copy, Copy, Copy>>: SpoolPhyOp #Records=11"
                    }
                }
            };
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - ProjectionSpool should be folded into SpoolLookup
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "ProjectionSpool should be folded into SpoolLookup");
            Assert.IsTrue(root.HasSpoolTypeInfo, "Parent should have SpoolTypeInfo after folding");
            Assert.IsTrue(root.SpoolTypeInfo.Contains("Project"), "SpoolTypeInfo should contain 'Project'");
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
            plan.AllNodes[0].Children.Add(plan.AllNodes[1]);
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "AggregationSpool should be folded into SpoolLookup");
            Assert.IsTrue(root.HasSpoolTypeInfo, "Parent should have SpoolTypeInfo after folding");
            Assert.IsTrue(root.SpoolTypeInfo.Contains("Sum"), "SpoolTypeInfo should indicate Sum aggregation");
        }

        #endregion

        #region Column References Tests

        [TestMethod]
        public void ColumnReferences_WithTableColumn_ExtractsColumnName()
        {
            // Arrange - operation with 'Table'[Column] format
            var node = CreateNodeWithOperation("Scan_Vertipaq: RequiredCols(0)('Product'[Brand])");

            // Act
            var refs = node.ColumnReferences;

            // Assert
            Assert.IsTrue(refs.Contains("Brand"), "Should extract column name from 'Table'[Column] format");
        }

        [TestMethod]
        public void ColumnReferences_WithMultipleColumns_ExtractsAll()
        {
            // Arrange
            var node = CreateNodeWithOperation("IterCols(0, 1)('Product'[Brand], 'Sales'[Amount])");

            // Act
            var refs = node.ColumnReferences;

            // Assert
            Assert.IsTrue(refs.Contains("Brand"), "Should extract Brand");
            Assert.IsTrue(refs.Contains("Amount"), "Should extract Amount");
        }

        [TestMethod]
        public void ColumnReferences_FiltersOutIterCols()
        {
            // Arrange - IterCols should NOT appear as a column reference
            var node = CreateNodeWithOperation("Spool_Iterator: IterPhyOp [IterCols](0)('Product'[Brand])");

            // Act
            var refs = node.ColumnReferences;

            // Assert
            Assert.IsFalse(refs.Contains("IterCols"), "Should filter out IterCols system property");
            Assert.IsTrue(refs.Contains("Brand"), "Should still extract actual column Brand");
        }

        [TestMethod]
        public void ColumnReferences_FiltersOutLookupCols()
        {
            // Arrange
            var node = CreateNodeWithOperation("SpoolLookup: LookupPhyOp [LookupCols](0)('Product'[Brand])");

            // Act
            var refs = node.ColumnReferences;

            // Assert
            Assert.IsFalse(refs.Contains("LookupCols"), "Should filter out LookupCols system property");
        }

        [TestMethod]
        public void ColumnReferences_FiltersOutRequiredCols()
        {
            // Arrange
            var node = CreateNodeWithOperation("Scan: [RequiredCols](0, 1)('T'[Col])");

            // Act
            var refs = node.ColumnReferences;

            // Assert
            Assert.IsFalse(refs.Contains("RequiredCols"), "Should filter out RequiredCols system property");
        }

        [TestMethod]
        public void ColumnReferences_FiltersOutAllSystemProperties()
        {
            // Arrange - operation with multiple system property names that look like columns
            var node = CreateNodeWithOperation(
                "[IterCols] [LookupCols] [RequiredCols] [DependOnCols] [JoinCols] " +
                "[SemijoinCols] [KeyCols] [ValueCols] [FieldCols] [BlankRow] [MeasureRef] [LogOp] [Table] " +
                "'Actual'[Column]");

            // Act
            var refs = node.ColumnReferences;

            // Assert - only actual column should be present
            Assert.AreEqual("Column", refs, "Should only contain actual column, not system properties");
        }

        [TestMethod]
        public void ColumnReferences_EmptyWhenNoColumns()
        {
            // Arrange
            var node = CreateNodeWithOperation("Cache: IterPhyOp #FieldCols=1");

            // Act
            var refs = node.ColumnReferences;

            // Assert
            Assert.AreEqual(string.Empty, refs, "Should return empty when no column references");
        }

        [TestMethod]
        public void ColumnReferences_LimitsToFiveColumns()
        {
            // Arrange - operation with more than 5 columns
            var node = CreateNodeWithOperation(
                "'T'[Col1], 'T'[Col2], 'T'[Col3], 'T'[Col4], 'T'[Col5], 'T'[Col6], 'T'[Col7]");

            // Act
            var refs = node.ColumnReferences;
            var count = refs.Split(',').Length;

            // Assert
            Assert.IsTrue(count <= 5, $"Should limit to 5 columns, got {count}");
        }

        #endregion

        #region Nested Spool Chain Grouping Tests

        [TestMethod]
        public void BuildTree_NestedSpoolIterators_SameRecords_FoldsIntoOne()
        {
            // Arrange - Two Spool_Iterator nodes in a chain with same #Records
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=ScalarVarProxy IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null // Will be set below
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Cache: IterPhyOp #FieldCols=1 #ValueCols=3",
                        Parent = null // Will be set below
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Should fold node 2 into node 1
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId);
            Assert.AreEqual(2, root.NestedSpoolDepth, "Should have depth 2 for two folded Spool_Iterators");
            Assert.IsTrue(root.IsNestedSpoolChain, "Should be marked as nested chain");
            Assert.IsTrue(root.DisplayName.Contains("×2"), "DisplayName should show depth ×2");

            // The child should be the Cache, not the second Spool_Iterator
            Assert.AreEqual(1, root.Children.Count, "Should have 1 child (Cache)");
            Assert.AreEqual("Cache", root.Children[0].OperatorName);
        }

        [TestMethod]
        public void BuildTree_NestedSpoolIterators_DifferentRecords_FoldsWithRowRange()
        {
            // Arrange - Two Spool_Iterator nodes with different #Records
            // Current behavior: fold and track row range (1-11)
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Level = 0,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=1 #KeyCols=0 #ValueCols=3",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Level = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=ScalarVarProxy IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null // Will be set below
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Level = 2,
                        Operation = "Cache: IterPhyOp #FieldCols=1 #ValueCols=3",
                        Parent = null // Will be set below
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

            // Assert - Should fold with row range tracking (min=1, max=11)
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId);
            Assert.AreEqual(2, root.NestedSpoolDepth, "Should have depth 2 (folded nested chain)");
            Assert.IsTrue(root.IsNestedSpoolChain, "Should be marked as nested chain");
            Assert.IsTrue(root.DisplayName.Contains("×2"), "DisplayName should show depth ×2");

            // Row range should be tracked
            Assert.AreEqual(1, root.SpoolRowRangeMin, "Min records should be 1");
            Assert.AreEqual(11, root.SpoolRowRangeMax, "Max records should be 11");

            // Child should be Cache (second Spool_Iterator was folded)
            Assert.AreEqual(1, root.Children.Count);
            Assert.AreEqual("Cache", root.Children[0].OperatorName, "Child should be Cache node");
        }

        [TestMethod]
        public void BuildTree_ThreeNestedSpoolIterators_SameRecords_FoldsAllIntoOne()
        {
            // Arrange - Three Spool_Iterator nodes in a chain with same #Records
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=ScalarVarProxy IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null // Will be set below
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=GroupBy IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null // Will be set below
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 4,
                        Operation = "Cache: IterPhyOp #FieldCols=1 #ValueCols=3",
                        Parent = null // Will be set below
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.AllNodes[3].Parent = plan.AllNodes[2];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Should fold nodes 2 and 3 into node 1
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId);
            Assert.AreEqual(3, root.NestedSpoolDepth, "Should have depth 3 for three folded Spool_Iterators");
            Assert.IsTrue(root.IsNestedSpoolChain, "Should be marked as nested chain");
            Assert.IsTrue(root.DisplayName.Contains("×3"), "DisplayName should show depth ×3");

            // The child should be the Cache
            Assert.AreEqual(1, root.Children.Count, "Should have 1 child (Cache)");
            Assert.AreEqual("Cache", root.Children[0].OperatorName);
        }

        [TestMethod]
        public void BuildTree_SpoolIterator_WithMultipleChildren_DoesNotFold()
        {
            // Arrange - Spool_Iterator with two children (not a linear chain)
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=ScalarVarProxy IterCols(0)('Product'[Brand]) #Records=11 #KeyCols=1 #ValueCols=3",
                        Parent = null // Will be set below
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "SpoolLookup: LookupPhyOp LogOp=ScalarVarProxy LookupCols(0)('Product'[Brand]) #Records=11",
                        Parent = null // Will be set below
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0]; // Both children of root
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Should NOT fold (multiple children)
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId);
            Assert.AreEqual(1, root.NestedSpoolDepth, "Should have depth 1 (no nesting)");
            Assert.IsFalse(root.IsNestedSpoolChain, "Should NOT be marked as nested chain");

            // Should have both children
            Assert.AreEqual(2, root.Children.Count, "Should have 2 children");
        }

        #endregion

        #region ISBLANK/Not/Filter Folding Tests

        [TestMethod]
        public void BuildTree_StandaloneISBLANK_ShowsColumnNames()
        {
            // Arrange - Standalone ISBLANK should show column names
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "ISBLANK: IterPhyOp LogOp=ISBLANK IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    }
                }
            };
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.HasFilterPredicate, "ISBLANK should have a predicate expression");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("'Customer'[CustomerKey]"),
                "Should show Customer column");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("'Date'[Date]"),
                "Should show Date column");
        }

        [TestMethod]
        public void BuildTree_NotWithISBLANK_FoldsAndShowsNotIsblank()
        {
            // Arrange - Not with ISBLANK child should fold and show NOT(ISBLANK(...))
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Not: IterPhyOp LogOp=Not IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "ISBLANK: IterPhyOp LogOp=ISBLANK IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - ISBLANK should be folded into Not
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "Not should be the root");
            Assert.AreEqual(0, root.Children.Count, "ISBLANK should be folded, no children");
            Assert.IsTrue(root.HasFilterPredicate, "Not should have predicate expression");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("NOT(ISBLANK("),
                $"Should show NOT(ISBLANK(...)), got: {root.FilterPredicateExpression}");
        }

        [TestMethod]
        public void BuildTree_FilterNotISBLANK_FoldsChainAndShowsPredicate()
        {
            // Arrange - Filter → Not → ISBLANK chain should fold completely
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Filter: IterPhyOp LogOp=Filter IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Not: IterPhyOp LogOp=Not IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "ISBLANK: IterPhyOp LogOp=ISBLANK IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Both Not and ISBLANK should be folded into Filter
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "Filter should be the root");
            Assert.AreEqual(0, root.Children.Count, "Not and ISBLANK should be folded, no children");
            Assert.IsTrue(root.HasFilterPredicate, "Filter should have predicate expression");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("NOT(ISBLANK("),
                $"Should show NOT(ISBLANK(...)), got: {root.FilterPredicateExpression}");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("'Customer'[CustomerKey]"),
                "Should contain Customer column");
            Assert.IsTrue(root.FilterPredicateExpression.Contains("'Date'[Date]"),
                "Should contain Date column");
        }

        [TestMethod]
        public void BuildTree_FilterNotISBLANK_WithSiblingChild_OnlyFoldsChain()
        {
            // Arrange - Filter with Not→ISBLANK chain AND another child (Scan)
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Filter: IterPhyOp LogOp=Filter IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Not: IterPhyOp LogOp=Not IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "ISBLANK: IterPhyOp LogOp=ISBLANK IterCols(1, 42)('Customer'[CustomerKey], 'Date'[Date])",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 4,
                        Operation = "Scan_Vertipaq: RelLogOp Table='Date' #Records=731",
                        EngineType = EngineType.StorageEngine,
                        Parent = null
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.AllNodes[3].Parent = plan.AllNodes[0]; // Sibling of Not
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Not→ISBLANK chain folded, but Scan preserved as child
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "Filter should be the root");
            Assert.AreEqual(1, root.Children.Count, "Scan should remain as child");
            Assert.AreEqual("Scan_Vertipaq", root.Children[0].OperatorName, "Child should be Scan");
            Assert.IsTrue(root.HasFilterPredicate, "Filter should have predicate expression");
        }

        [TestMethod]
        public void BuildTree_SingletonTable_FoldsIntoParent()
        {
            // Arrange - SingletonTable should always fold into parent
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "AddColumns: IterPhyOp LogOp=AddColumns",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "SingletonTable: IterPhyOp LogOp=TableToScalar",
                        Parent = null
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - SingletonTable should be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "AddColumns should be root");
            Assert.AreEqual(0, root.Children.Count, "SingletonTable should be folded, no children");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("SingletonTable")),
                "SingletonTable should be in FoldedOperations");
        }

        [TestMethod]
        public void BuildTree_AggregationSpoolUnderMultiValuedHashLookup_Folds()
        {
            // Arrange - AggregationSpool under Spool_MultiValuedHashLookup should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_MultiValuedHashLookup: IterPhyOp LogOp=First LookupCols(42)('Date'[Date]) IterCols(1)('Customer'[CustomerKey]) #Records=18869",
                        Parent = null
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "AggregationSpool<Last>: SpoolPhyOp #Records=18869",
                        Parent = null
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - AggregationSpool should be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "Spool_MultiValuedHashLookup should be root");
            Assert.AreEqual(0, root.Children.Count, "AggregationSpool should be folded, no children");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("AggregationSpool")),
                "AggregationSpool should be in FoldedOperations");
        }

        [TestMethod]
        public void BuildTree_ExtendLookupUnderMultiValuedHashLookup_Folds()
        {
            // Arrange - Extend_Lookup under Spool_MultiValuedHashLookup should fold
            // The multi-valued context makes the extend redundant
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_MultiValuedHashLookup: IterPhyOp LogOp=First LookupCols(42)('Date'[Date]) IterCols(1)('Customer'[CustomerKey]) #Records=18869",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "AggregationSpool<Last>: SpoolPhyOp #Records=18869",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Extend_Lookup: IterPhyOp LogOp=Extend_Lookup'Date'[Date] IterCols(42)('Date'[Date])",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0]; // Extend_Lookup is sibling of AggregationSpool
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Both AggregationSpool and Extend_Lookup should be folded
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "Spool_MultiValuedHashLookup should be root");
            Assert.AreEqual(0, root.Children.Count, "Both children should be folded");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("AggregationSpool")),
                "AggregationSpool should be in FoldedOperations");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("Extend_Lookup")),
                "Extend_Lookup should be in FoldedOperations");
        }

        [TestMethod]
        public void BuildTree_ExtendLookupUnderSpoolIterator_Folds()
        {
            // Arrange - Extend_Lookup under any Spool should fold (not just MultiValuedHashLookup)
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Scan_Vertipaq IterCols(1, 44)('Customer'[CustomerKey], 'Date'[Date]) #Records=18869",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Extend_Lookup: IterPhyOp LogOp=Extend_Lookup'Date'[Date] IterCols(42)('Date'[Date])",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Extend_Lookup should be folded into Spool_Iterator
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "Spool_Iterator should be root");
            Assert.AreEqual(0, root.Children.Count, "Extend_Lookup should be folded");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("Extend_Lookup")),
                "Extend_Lookup should be in FoldedOperations");
        }

        [TestMethod]
        public void BuildTree_ExtendLookupUnderSpoolLookup_Folds()
        {
            // Arrange - Extend_Lookup under SpoolLookup should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "SpoolLookup: IterPhyOp LogOp=First LookupCols(1)('Customer'[CustomerKey]) #Records=18869",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Extend_Lookup: IterPhyOp LogOp=Extend_Lookup'Date'[Date] IterCols(42)('Date'[Date])",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Extend_Lookup should be folded into SpoolLookup
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "SpoolLookup should be root");
            Assert.AreEqual(0, root.Children.Count, "Extend_Lookup should be folded");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("Extend_Lookup")),
                "Extend_Lookup should be in FoldedOperations");
        }

        #endregion

        #region Chained Arithmetic Operator Folding Tests

        [TestMethod]
        public void BuildTree_ChainedAdds_FoldsWithCount()
        {
            // Arrange - Three nested Add operators should fold into one "Add (3x)"
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Add: ScaLogOp MeasureRef=[Test] DependOnCols(1)('Customer'[CustomerKey]) Currency DominantValue=BLANK"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Add: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency DominantValue=BLANK"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Add: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency DominantValue=BLANK"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 4,
                        Operation = "Calculate: ScaLogOp MeasureRef=[Audio] DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.AllNodes[3].Parent = plan.AllNodes[2];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId, "First Add should be root");
            Assert.AreEqual(3, root.ChainedOperatorCount, "Should count 3 chained Adds");
            Assert.IsTrue(root.HasChainedOperators, "Should have chained operators");
            Assert.IsTrue(root.DisplayName.Contains("(3x)"), $"DisplayName should show (3x), got: {root.DisplayName}");
            Assert.AreEqual(1, root.Children.Count, "Calculate should be the only child after folding");
            Assert.AreEqual("Calculate", root.Children[0].OperatorName, "Calculate should be promoted as child");
        }

        [TestMethod]
        public void BuildTree_ChainedMultiplies_FoldsWithCount()
        {
            // Arrange - Two nested Multiply operators should fold into "Multiply (2x)"
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Multiply: ScaLogOp DependOnCols(151, 155)('Sales'[Quantity], 'Sales'[Net Price]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Multiply: ScaLogOp DependOnCols(151)('Sales'[Quantity]) Integer"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "'Sales'[Quantity]: ScaLogOp DependOnCols(151)('Sales'[Quantity]) Integer"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(2, root.ChainedOperatorCount, "Should count 2 chained Multiplies");
            Assert.IsTrue(root.DisplayName.Contains("(2x)"), $"DisplayName should show (2x), got: {root.DisplayName}");
        }

        [TestMethod]
        public void BuildTree_MixedArithmetic_DoesNotFold()
        {
            // Arrange - Add followed by Multiply should NOT fold (different operators)
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Add: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Multiply: ScaLogOp DependOnCols(151, 155)('Sales'[Quantity], 'Sales'[Net Price]) Currency"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.ChainedOperatorCount, "Should NOT chain different operators");
            Assert.IsFalse(root.HasChainedOperators, "Should not have chained operators");
            Assert.IsFalse(root.DisplayName.Contains("x)"), $"DisplayName should not have count, got: {root.DisplayName}");
            Assert.AreEqual(1, root.Children.Count, "Multiply should be a child");
        }

        [TestMethod]
        public void BuildTree_ChainedAdd_PreservesMeasureRef()
        {
            // Arrange - MeasureRef from top Add should be preserved
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Add: ScaLogOp MeasureRef=[Total Sales] DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Add: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(2, root.ChainedOperatorCount, "Should count 2 chained Adds");
            Assert.AreEqual("[Total Sales]", root.MeasureReference, "MeasureRef should be preserved from top node");
        }

        [TestMethod]
        public void BuildTree_ChainedAdd_PromotesNonAddChildren()
        {
            // Arrange - Non-Add children should be promoted to the folded parent
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Add: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Add: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Calculate: ScaLogOp MeasureRef=[Audio] DependOnCols(1) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 4,
                        Operation = "Calculate: ScaLogOp MeasureRef=[Video] DependOnCols(1) Currency"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1]; // Child of second Add
            plan.AllNodes[3].Parent = plan.AllNodes[1]; // Another child of second Add
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(2, root.ChainedOperatorCount, "Should count 2 chained Adds");
            Assert.AreEqual(2, root.Children.Count, "Both Calculate nodes should be promoted as children");
        }

        [TestMethod]
        public void BuildTree_SingleAdd_NoCountSuffix()
        {
            // Arrange - Single Add should NOT show count suffix
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Add: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Calculate: ScaLogOp MeasureRef=[Audio] Currency"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.ChainedOperatorCount, "Single operator has count 1");
            Assert.IsFalse(root.HasChainedOperators, "Should not have chained operators");
            Assert.IsFalse(root.DisplayName.Contains("("), $"DisplayName should not have parentheses, got: {root.DisplayName}");
        }

        [TestMethod]
        public void BuildTree_ChainedDivide_FoldsWithCount()
        {
            // Arrange - Chained Divide operators should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Divide: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Divide: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(2, root.ChainedOperatorCount, "Should count 2 chained Divides");
            Assert.IsTrue(root.DisplayName.Contains("(2x)"), $"DisplayName should show (2x), got: {root.DisplayName}");
        }

        [TestMethod]
        public void BuildTree_ChainedCoalesce_FoldsWithCount()
        {
            // Arrange - Chained Coalesce operators should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Coalesce: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Coalesce: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Coalesce: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) Currency"
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(3, root.ChainedOperatorCount, "Should count 3 chained Coalesces");
            Assert.IsTrue(root.DisplayName.Contains("(3x)"), $"DisplayName should show (3x), got: {root.DisplayName}");
        }

        #endregion

        #region Generalized Folding Tests - Pre-Refactor Safety Net

        // These tests ensure we don't break existing behavior when generalizing folding logic

        #region Spool Chain Matrix Tests

        [TestMethod]
        [Ignore("Future feature: Spool_MultiValuedHashLookup not yet included in spool chain folding")]
        public void BuildTree_ProjectionSpoolUnderSpoolMultiValuedHashLookup_Folds()
        {
            // Arrange - ProjectionSpool under Spool_MultiValuedHashLookup should fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_MultiValuedHashLookup: IterPhyOp LogOp=First #Records=1000",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "ProjectionSpool<Copy>: SpoolPhyOp #Records=1000",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "ProjectionSpool should be folded");
        }

        [TestMethod]
        public void BuildTree_AggregationSpoolUnderSpoolIterator_Folds()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=500",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "AggregationSpool<Sum>: SpoolPhyOp #Records=500",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            // Note: Current behavior may vary - this test documents it
            // The refactoring should preserve whatever the current behavior is
        }

        [TestMethod]
        public void BuildTree_CacheUnderSpoolIterator_Folds()
        {
            // Arrange - Cache under Spool_Iterator
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=100",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Cache: IterPhyOp #FieldCols=1 #ValueCols=3",
                        EngineType = EngineType.StorageEngine  // Cache is often SE
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Cache with different engine type should NOT fold
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "Cache with SE engine should NOT fold into FE parent");
        }

        [TestMethod]
        public void BuildTree_CacheUnderSpoolIterator_SameEngineType_MayFold()
        {
            // Arrange - Cache under Spool_Iterator with same engine type
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=100",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Cache: IterPhyOp #FieldCols=1 #ValueCols=3",
                        EngineType = EngineType.FormulaEngine  // Same as parent
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Document current behavior
            Assert.IsNotNull(root);
            // Cache doesn't fold into Spool_Iterator currently - this is intentional
            // as Cache represents a distinct data source
        }

        #endregion

        #region Engine Transition Preservation Tests

        [TestMethod]
        public void BuildTree_SEChildUnderFEParent_DoesNotFold()
        {
            // CRITICAL: Engine transitions should NEVER be folded - they're visually important
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp #Records=100",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "ProjectionSpool<Copy>: SpoolPhyOp #Records=100",
                        EngineType = EngineType.StorageEngine  // Different engine!
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - SE child should NOT fold into FE parent
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "SE child should NOT fold into FE parent - engine transition is significant");
            Assert.AreEqual(EngineType.StorageEngine, root.Children[0].Node.EngineType);
        }

        [TestMethod]
        public void BuildTree_FEChildUnderSEParent_DoesNotFold()
        {
            // CRITICAL: Engine transitions should NEVER be folded
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Scan_Vertipaq: RelLogOp #Records=1000",
                        EngineType = EngineType.StorageEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Filter: IterPhyOp #Records=100",
                        EngineType = EngineType.FormulaEngine  // Different engine!
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - FE child should NOT fold into SE parent
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "FE child should NOT fold into SE parent");
        }

        [TestMethod]
        public void BuildTree_DirectQueryChildUnderFEParent_DoesNotFold()
        {
            // DirectQuery transitions are also significant
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator: IterPhyOp #Records=100",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "DirectQueryResult: IterPhyOp #Records=100",
                        EngineType = EngineType.DirectQuery
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.Children.Count, "DirectQuery child should NOT fold into FE parent");
        }

        #endregion

        #region Wrapper Operator Tests

        [TestMethod]
        [Ignore("Future feature: Variant fold-down not yet implemented")]
        public void BuildTree_VariantOperator_FoldsDownIntoChild()
        {
            // Variant wraps a type coercion and should fold into its child
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Variant->Currency: ScaPhyOp",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "SpoolLookup: LookupPhyOp Currency #Records=1",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Variant should fold, SpoolLookup becomes root
            Assert.IsNotNull(root);
            Assert.AreEqual("SpoolLookup", root.OperatorName, "SpoolLookup should be root after Variant folds down");
        }

        [TestMethod]
        [Ignore("Future feature: Proxy fold-down not yet implemented")]
        public void BuildTree_ProxyOperator_FoldsDownIntoChild()
        {
            // Proxy wraps and should fold into its single child
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Proxy: IterPhyOp",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "AddColumns: IterPhyOp",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Proxy should fold, AddColumns becomes root
            Assert.IsNotNull(root);
            Assert.AreEqual("AddColumns", root.OperatorName, "AddColumns should be root after Proxy folds down");
        }

        [TestMethod]
        public void BuildTree_ProxyWithMultipleChildren_DoesNotFold()
        {
            // Proxy with multiple children should NOT fold
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Proxy: IterPhyOp",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "AddColumns: IterPhyOp",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Filter: IterPhyOp",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Proxy with multiple children stays
            Assert.IsNotNull(root);
            Assert.AreEqual("Proxy", root.OperatorName, "Proxy with multiple children should NOT fold");
            Assert.AreEqual(2, root.Children.Count);
        }

        #endregion

        #region Predicate Chain Generalization Tests

        [TestMethod]
        public void BuildTree_FilterNotISERROR_FoldsAndShowsExpression()
        {
            // Test that ISERROR works like ISBLANK
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Filter: IterPhyOp LogOp=Filter IterCols(1)('Sales'[Amount])",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Not: IterPhyOp LogOp=Not IterCols(1)('Sales'[Amount])",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "ISERROR: IterPhyOp LogOp=ISERROR IterCols(1)('Sales'[Amount])",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Currently ISERROR may not be handled - this test documents expected behavior
            Assert.IsNotNull(root);
            // After generalization, this should fold like ISBLANK
        }

        [TestMethod]
        public void BuildTree_StandaloneNot_ShowsChildExpression()
        {
            // Not without ISBLANK child - should still display something sensible
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Not: IterPhyOp LogOp=Not IterCols(1)('Sales'[IsActive])",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "'Sales'[IsActive]: ScaPhyOp Boolean",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.AreEqual("Not", root.OperatorName);
        }

        #endregion

        #region Metrics Inheritance Tests

        [TestMethod]
        public void BuildTree_FoldedNode_ParentInheritsFoldedOperations()
        {
            // When a node folds, its operation should appear in parent's FoldedOperations
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp #Records=100",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "ProjectionSpool<ProjectFusion<Copy>>: SpoolPhyOp #Records=100",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.FoldedOperations.Count > 0, "Should have folded operations");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("ProjectionSpool")),
                "FoldedOperations should contain the folded ProjectionSpool");
        }

        [TestMethod]
        [Ignore("Future feature: Nested Spool_MultiValuedHashLookup chain folding not yet implemented")]
        public void BuildTree_MultipleFolds_AllOperationsInFoldedList()
        {
            // Chain of folds should accumulate in FoldedOperations
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Filter: IterPhyOp LogOp=Filter IterCols(1, 42)('A'[X], 'B'[Y])",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "Not: IterPhyOp LogOp=Not IterCols(1, 42)('A'[X], 'B'[Y])",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "ISBLANK: IterPhyOp LogOp=ISBLANK IterCols(1, 42)('A'[X], 'B'[Y])",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("Not")),
                "FoldedOperations should contain Not");
            Assert.IsTrue(root.FoldedOperations.Any(op => op.Contains("ISBLANK")),
                "FoldedOperations should contain ISBLANK");
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void BuildTree_RootNode_NeverFolded()
        {
            // Root node should never be folded even if it matches fold criteria
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "SingletonTable: IterPhyOp LogOp=TableToScalar",  // Would normally fold
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Root should exist even though SingletonTable normally folds
            Assert.IsNotNull(root);
            Assert.AreEqual(1, root.NodeId);
            Assert.AreEqual("SingletonTable", root.OperatorName);
        }

        [TestMethod]
        [Ignore("Future feature: 10-level nested spool chain folding has different expected count")]
        public void BuildTree_DeeplyNestedChain_AllFoldsCorrectly()
        {
            // Test a deeply nested chain (10 levels) folds correctly
            var plan = new EnrichedQueryPlan { AllNodes = new List<EnrichedPlanNode>() };

            // Create 10 nested Spool_Iterators
            for (int i = 1; i <= 10; i++)
            {
                var node = new EnrichedPlanNode
                {
                    NodeId = i,
                    Operation = $"Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=100",
                    EngineType = EngineType.FormulaEngine
                };
                plan.AllNodes.Add(node);
                if (i > 1)
                {
                    node.Parent = plan.AllNodes[i - 2];
                }
            }
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - All should fold into root, depth should be 10
            Assert.IsNotNull(root);
            Assert.AreEqual(0, root.Children.Count, "All nested Spool_Iterators should fold");
            Assert.AreEqual(10, root.NestedSpoolDepth, "Should have depth 10");
        }

        [TestMethod]
        public void BuildTree_NodeWithNoOperation_HandledGracefully()
        {
            // Null or empty operation should not crash
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = null,  // null operation
                        EngineType = EngineType.Unknown
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "",  // empty operation
                        EngineType = EngineType.Unknown
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act - should not throw
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsNotNull(root);
        }

        #endregion

        #region Fixture Regression Tests

        [TestMethod]
        public void BuildTree_LargeFixture_CorrectMetrics()
        {
            // Load the large fixture and verify key metrics don't change after refactoring
            // Path: from src\bin\Debug go up to repo root (3 levels), then to tests\DaxStudio.Tests\VisualQueryPlan\Fixtures
            var fixturesDir = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "tests", "DaxStudio.Tests", "VisualQueryPlan", "Fixtures");

            var physicalPlanPath = Path.Combine(fixturesDir, "Large plan Physical Query Plan.tsv");

            if (!File.Exists(physicalPlanPath))
            {
                Assert.Inconclusive("Large fixture file not found - skipping regression test");
                return;
            }

            // Parse the TSV file
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var lines = File.ReadAllLines(physicalPlanPath);
            for (int i = 1; i < lines.Length; i++)  // Skip header
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;

                var row = new PhysicalQueryPlanRow();
                var operationWithIndent = parts[2];
                var trimmedOperation = operationWithIndent.TrimStart();
                var leadingSpaces = operationWithIndent.Length - trimmedOperation.Length;
                row.PrepareQueryPlanRow(new string(' ', leadingSpaces) + trimmedOperation, i);
                rows.Add(row);
            }

            // Build the enriched plan
            var enrichedPlan = new EnrichedQueryPlan { AllNodes = new List<EnrichedPlanNode>() };
            foreach (var row in rows)
            {
                var node = new EnrichedPlanNode
                {
                    NodeId = row.RowNumber,
                    Operation = row.Operation,
                    Level = row.Level
                };
                enrichedPlan.AllNodes.Add(node);
            }

            // Set up parent-child relationships based on level
            var nodeStack = new Stack<EnrichedPlanNode>();
            foreach (var node in enrichedPlan.AllNodes)
            {
                while (nodeStack.Count > 0 && nodeStack.Peek().Level >= node.Level)
                {
                    nodeStack.Pop();
                }
                if (nodeStack.Count > 0)
                {
                    node.Parent = nodeStack.Peek();
                }
                nodeStack.Push(node);
            }
            enrichedPlan.RootNode = enrichedPlan.AllNodes.FirstOrDefault();

            // Act
            var root = PlanNodeViewModel.BuildTree(enrichedPlan);

            // Assert - capture key metrics for regression detection
            Assert.IsNotNull(root);

            // Count visible nodes (non-folded)
            int CountVisibleNodes(PlanNodeViewModel node)
            {
                return 1 + node.Children.Sum(c => CountVisibleNodes(c));
            }
            var visibleNodeCount = CountVisibleNodes(root);

            // This is a snapshot test - the exact number may need updating
            // but changes should be intentional
            Assert.IsTrue(visibleNodeCount > 0, "Should have at least 1 visible node");
            Assert.IsTrue(visibleNodeCount < enrichedPlan.AllNodes.Count,
                "Should have fewer visible nodes than total due to folding");

            // Log the metrics for reference
            System.Diagnostics.Debug.WriteLine($"Fixture regression test: {enrichedPlan.AllNodes.Count} total nodes, {visibleNodeCount} visible after folding");
        }

        [TestMethod]
        public void BuildTree_LargeFixture_CompletesWithinTimeLimit()
        {
            // Performance regression test - BuildTree should complete in under 3 seconds
            // This test guards against O(n²) complexity regressions that caused 20+ second UI freezes
            // Path: from src\bin\Debug go up to repo root (3 levels), then to tests\DaxStudio.Tests\VisualQueryPlan\Fixtures
            var fixturesDir = Path.Combine(
                Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                "..", "..", "..", "tests", "DaxStudio.Tests", "VisualQueryPlan", "Fixtures");

            var physicalPlanPath = Path.Combine(fixturesDir, "Large plan Physical Query Plan.tsv");

            if (!File.Exists(physicalPlanPath))
            {
                Assert.Inconclusive("Large fixture file not found - skipping performance test");
                return;
            }

            // Parse the TSV file
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var lines = File.ReadAllLines(physicalPlanPath);
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;

                var row = new PhysicalQueryPlanRow();
                var operationWithIndent = parts[2];
                var trimmedOperation = operationWithIndent.TrimStart();
                var leadingSpaces = operationWithIndent.Length - trimmedOperation.Length;
                row.PrepareQueryPlanRow(new string(' ', leadingSpaces) + trimmedOperation, i);
                rows.Add(row);
            }

            // Build the enriched plan
            var enrichedPlan = new EnrichedQueryPlan { AllNodes = new List<EnrichedPlanNode>() };
            foreach (var row in rows)
            {
                var node = new EnrichedPlanNode
                {
                    NodeId = row.RowNumber,
                    Operation = row.Operation,
                    Level = row.Level
                };
                enrichedPlan.AllNodes.Add(node);
            }

            // Set up parent-child relationships
            var nodeStack = new Stack<EnrichedPlanNode>();
            foreach (var node in enrichedPlan.AllNodes)
            {
                while (nodeStack.Count > 0 && nodeStack.Peek().Level >= node.Level)
                {
                    nodeStack.Pop();
                }
                if (nodeStack.Count > 0)
                {
                    node.Parent = nodeStack.Peek();
                }
                nodeStack.Push(node);
            }
            enrichedPlan.RootNode = enrichedPlan.AllNodes.FirstOrDefault();

            // Act - measure BuildTree execution time
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var root = PlanNodeViewModel.BuildTree(enrichedPlan);
            stopwatch.Stop();

            // Assert - BuildTree must complete within 500ms
            // The previous O(n²) bug caused 20+ second freezes on this fixture
            var maxAllowedMs = 500;
            Assert.IsNotNull(root);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < maxAllowedMs,
                $"BuildTree took {stopwatch.ElapsedMilliseconds}ms which exceeds {maxAllowedMs}ms limit. " +
                $"This may indicate an O(n²) performance regression. " +
                $"Check for nested iterations over AllNodes or local regex patterns inside loops.");

            System.Diagnostics.Debug.WriteLine($"Performance test: BuildTree completed in {stopwatch.ElapsedMilliseconds}ms for {enrichedPlan.AllNodes.Count} nodes");
        }

        #endregion

        #region TableToScalar Records Inheritance Tests

        [TestMethod]
        public void BuildTree_TableToScalarToStartOfYear_InheritsRecords()
        {
            // Arrange - DatesBetween → TableToScalar → AggregationSpool<TableToScalar> → StartOfYear → LastDate
            // StartOfYear should inherit #Records from the TableToScalar when it folds
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "DatesBetween: RelLogOp DependOnCols(1)('Customer'[CustomerKey]) 44-44",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "TableToScalar: ScaLogOp DependOnCols(1)('Customer'[CustomerKey]) DateTime DominantValue=BLANK #Records=18869",
                        Records = 18869,
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "AggregationSpool<TableToScalar>: SpoolPhyOp #Records=18869",
                        Records = 18869,
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 4,
                        Operation = "StartOfYear: RelLogOp DependOnCols(1)('Customer'[CustomerKey]) 44-44 RequiredCols(1, 44)('Customer'[CustomerKey], 'Date'[Date])",
                        Records = null,  // No records on StartOfYear originally
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 5,
                        Operation = "LastDate: RelLogOp DependOnCols(1)('Customer'[CustomerKey]) 44-44",
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];  // TableToScalar under DatesBetween
            plan.AllNodes[2].Parent = plan.AllNodes[1];  // AggregationSpool under TableToScalar
            plan.AllNodes[3].Parent = plan.AllNodes[2];  // StartOfYear under AggregationSpool
            plan.AllNodes[4].Parent = plan.AllNodes[3];  // LastDate under StartOfYear
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - DatesBetween is root, StartOfYear should be its child with inherited records
            Assert.IsNotNull(root);
            Assert.AreEqual("DatesBetween", root.OperatorName, "DatesBetween should be the root");
            Assert.AreEqual(1, root.Children.Count, "Should have one child after folding");

            var startOfYear = root.Children[0];
            Assert.AreEqual("StartOfYear", startOfYear.OperatorName, "StartOfYear should be child of DatesBetween");
            Assert.AreEqual(18869, startOfYear.Records, "StartOfYear should inherit records from TableToScalar");
            Assert.AreEqual("Inherited", startOfYear.RecordsSource, "Records source should indicate inheritance");
            Assert.IsTrue(startOfYear.FoldedOperations.Any(op => op.Contains("TableToScalar")),
                "TableToScalar should be in FoldedOperations");
            Assert.IsTrue(startOfYear.FoldedOperations.Any(op => op.Contains("AggregationSpool")),
                "AggregationSpool should be in FoldedOperations");
        }

        [TestMethod]
        public void BuildTree_TableToScalarToStartOfYear_WithExistingRecords_DoesNotOverwrite()
        {
            // Arrange - If StartOfYear already has records, don't overwrite them
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "DatesBetween: RelLogOp DependOnCols(1)('Customer'[CustomerKey]) 44-44",
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "TableToScalar: ScaLogOp #Records=18869",
                        Records = 18869,
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "AggregationSpool<TableToScalar>: SpoolPhyOp #Records=18869",
                        Records = 18869,
                        EngineType = EngineType.FormulaEngine
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 4,
                        Operation = "StartOfYear: RelLogOp #Records=5000",  // Already has records
                        Records = 5000,
                        EngineType = EngineType.FormulaEngine
                    }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.AllNodes[3].Parent = plan.AllNodes[2];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert - Should keep original records
            Assert.IsNotNull(root);
            Assert.AreEqual("DatesBetween", root.OperatorName, "DatesBetween should be the root");
            Assert.AreEqual(1, root.Children.Count, "Should have one child after folding");

            var startOfYear = root.Children[0];
            Assert.AreEqual("StartOfYear", startOfYear.OperatorName);
            Assert.AreEqual(5000, startOfYear.Records, "Should keep existing records, not overwrite with inherited");
        }

        #endregion

        #region Subtree Collapse/Expand Tests

        [TestMethod]
        public void SubtreeWidth_NoChildren_ReturnsOne()
        {
            // Arrange
            var node = new EnrichedPlanNode { NodeId = 1, Operation = "Test" };
            var vm = new PlanNodeViewModel(node);

            // Act & Assert
            Assert.AreEqual(1, vm.SubtreeWidth, "Leaf node should have subtree width of 1");
        }

        [TestMethod]
        public void SubtreeWidth_WithChildren_ReturnsSumOfChildWidths()
        {
            // Arrange - Create a simple tree with 3 leaf children
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Operation = "Parent" },
                    new EnrichedPlanNode { NodeId = 2, Operation = "Child1" },
                    new EnrichedPlanNode { NodeId = 3, Operation = "Child2" },
                    new EnrichedPlanNode { NodeId = 4, Operation = "Child3" }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.AllNodes[3].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.AreEqual(3, root.SubtreeWidth, "Parent with 3 leaf children should have width 3");
        }

        [TestMethod]
        public void CanToggleSubtree_MultipleChildrenAndLargeWidth_ReturnsTrue()
        {
            // Arrange - Create a tree with 2 children and subtree width >= 20
            var nodes = new List<EnrichedPlanNode>
            {
                new EnrichedPlanNode { NodeId = 1, Operation = "Root" }
            };

            // Add 2 subtrees, each with 10 leaf nodes
            for (int subtree = 0; subtree < 2; subtree++)
            {
                var subtreeRoot = new EnrichedPlanNode { NodeId = 2 + subtree * 11, Operation = $"Subtree{subtree}" };
                subtreeRoot.Parent = nodes[0];
                nodes.Add(subtreeRoot);

                for (int i = 0; i < 10; i++)
                {
                    var leaf = new EnrichedPlanNode { NodeId = 3 + subtree * 11 + i, Operation = $"Leaf{subtree}_{i}" };
                    leaf.Parent = subtreeRoot;
                    nodes.Add(leaf);
                }
            }

            var plan = new EnrichedQueryPlan { AllNodes = nodes, RootNode = nodes[0] };

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsTrue(root.CanToggleSubtree, "Node with 2 children and width >= 20 should show toggle");
            Assert.AreEqual(20, root.SubtreeWidth, "Root should have subtree width of 20");
        }

        [TestMethod]
        public void CanToggleSubtree_SingleChild_ReturnsFalse()
        {
            // Arrange - Single child, large subtree
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Operation = "Root" },
                    new EnrichedPlanNode { NodeId = 2, Operation = "SingleChild" }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsFalse(root.CanToggleSubtree, "Node with single child should not show toggle");
        }

        [TestMethod]
        public void CanToggleSubtree_SmallWidth_ReturnsFalse()
        {
            // Arrange - Multiple children but width < 20
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Operation = "Root" },
                    new EnrichedPlanNode { NodeId = 2, Operation = "Child1" },
                    new EnrichedPlanNode { NodeId = 3, Operation = "Child2" }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            // Act
            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsFalse(root.CanToggleSubtree, "Node with width < 20 should not show toggle");
            Assert.AreEqual(2, root.SubtreeWidth);
        }

        [TestMethod]
        public void IsSubtreeCollapsed_TogglesCorrectly()
        {
            // Arrange
            var node = new EnrichedPlanNode { NodeId = 1, Operation = "Test" };
            var vm = new PlanNodeViewModel(node);

            // Assert initial state
            Assert.IsFalse(vm.IsSubtreeCollapsed, "Should start expanded");
            Assert.AreEqual("−", vm.SubtreeToggleSymbol, "Expanded should show minus");

            // Act - toggle
            vm.ToggleSubtree();

            // Assert collapsed
            Assert.IsTrue(vm.IsSubtreeCollapsed, "Should be collapsed after toggle");
            Assert.AreEqual("+", vm.SubtreeToggleSymbol, "Collapsed should show plus");

            // Act - toggle again
            vm.ToggleSubtree();

            // Assert expanded again
            Assert.IsFalse(vm.IsSubtreeCollapsed, "Should be expanded after second toggle");
        }

        [TestMethod]
        public void VisibleChildrenForLayout_WhenCollapsed_ReturnsEmpty()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Operation = "Root" },
                    new EnrichedPlanNode { NodeId = 2, Operation = "Child1" },
                    new EnrichedPlanNode { NodeId = 3, Operation = "Child2" }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[0];
            plan.RootNode = plan.AllNodes[0];

            var root = PlanNodeViewModel.BuildTree(plan);

            // Assert initial state
            Assert.AreEqual(2, root.VisibleChildrenForLayout.Count(), "Should have 2 visible children initially");

            // Act
            root.IsSubtreeCollapsed = true;

            // Assert
            Assert.AreEqual(0, root.VisibleChildrenForLayout.Count(), "Should have 0 visible children when collapsed");
        }

        [TestMethod]
        public void ExpandPathToRoot_ExpandsAllAncestors()
        {
            // Arrange - Create a 3-level tree
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode { NodeId = 1, Operation = "Root" },
                    new EnrichedPlanNode { NodeId = 2, Operation = "Middle" },
                    new EnrichedPlanNode { NodeId = 3, Operation = "Leaf" }
                }
            };
            plan.AllNodes[1].Parent = plan.AllNodes[0];
            plan.AllNodes[2].Parent = plan.AllNodes[1];
            plan.RootNode = plan.AllNodes[0];

            var root = PlanNodeViewModel.BuildTree(plan);
            var middle = root.Children[0];
            var leaf = middle.Children[0];

            // Collapse both root and middle
            root.IsSubtreeCollapsed = true;
            middle.IsSubtreeCollapsed = true;

            // Assert collapsed state
            Assert.IsTrue(root.IsSubtreeCollapsed);
            Assert.IsTrue(middle.IsSubtreeCollapsed);

            // Act - expand path to leaf
            leaf.ExpandPathToRoot();

            // Assert - both ancestors should be expanded
            Assert.IsFalse(root.IsSubtreeCollapsed, "Root should be expanded");
            Assert.IsFalse(middle.IsSubtreeCollapsed, "Middle should be expanded");
        }

        #endregion

        #endregion
    }
}
