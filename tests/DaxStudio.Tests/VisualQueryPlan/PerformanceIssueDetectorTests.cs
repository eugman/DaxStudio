using DaxStudio.UI.Model;
using DaxStudio.UI.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Linq;

namespace DaxStudio.Tests.VisualQueryPlan
{
    [TestClass]
    public class PerformanceIssueDetectorTests
    {
        private PerformanceIssueDetector _detector;

        [TestInitialize]
        public void TestSetup()
        {
            _detector = new PerformanceIssueDetector();
        }

        [TestMethod]
        public void DetectNodeIssues_WithExcessiveMaterialization_ReturnsWarning()
        {
            // Arrange
            var node = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=150000"
            };

            // Act
            var issues = _detector.DetectNodeIssues(node);

            // Assert
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueType.ExcessiveMaterialization, issues[0].IssueType);
            Assert.AreEqual(IssueSeverity.Warning, issues[0].Severity);
            Assert.AreEqual(150000, issues[0].MetricValue);
        }

        [TestMethod]
        public void DetectNodeIssues_WithExcessiveMaterializationError_ReturnsError()
        {
            // Arrange
            var node = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=1500000"
            };

            // Act
            var issues = _detector.DetectNodeIssues(node);

            // Assert
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueType.ExcessiveMaterialization, issues[0].IssueType);
            Assert.AreEqual(IssueSeverity.Error, issues[0].Severity);
            Assert.AreEqual(1500000, issues[0].MetricValue);
        }

        [TestMethod]
        public void DetectNodeIssues_WithSpoolBelowThreshold_ReturnsNoIssues()
        {
            // Arrange
            var node = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Sum_Vertipaq #Records=500"
            };

            // Act
            var issues = _detector.DetectNodeIssues(node);

            // Assert
            Assert.AreEqual(0, issues.Count);
        }

        [TestMethod]
        public void DetectNodeIssues_WithCallbackDataId_ReturnsWarning()
        {
            // Arrange
            var node = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "CallbackDataID: ScaLogOp DependOnCols(0, 1) Integer"
            };

            // Act
            var issues = _detector.DetectNodeIssues(node);

            // Assert
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueType.CallbackDataID, issues[0].IssueType);
            Assert.AreEqual(IssueSeverity.Warning, issues[0].Severity);
        }

        [TestMethod]
        public void DetectNodeIssues_WithNormalOperation_ReturnsNoIssues()
        {
            // Arrange
            var node = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Scan_Vertipaq: RelLogOp DependOnCols()() #Records=1000"
            };

            // Act
            var issues = _detector.DetectNodeIssues(node);

            // Assert
            Assert.AreEqual(0, issues.Count);
        }

        [TestMethod]
        public void DetectNodeIssues_WithNullNode_ReturnsEmptyList()
        {
            // Act
            var issues = _detector.DetectNodeIssues(null);

            // Assert
            Assert.AreEqual(0, issues.Count);
        }

        [TestMethod]
        public void DetectIssues_WithMultipleProblematicNodes_ReturnsAllIssues()
        {
            // Arrange
            var plan = new EnrichedQueryPlan
            {
                AllNodes = new System.Collections.Generic.List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Spool_Iterator #Records=200000"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 2,
                        Operation = "CallbackDataID: operation"
                    },
                    new EnrichedPlanNode
                    {
                        NodeId = 3,
                        Operation = "Scan_Vertipaq normal operation"
                    }
                }
            };

            // Act
            var issues = _detector.DetectIssues(plan);

            // Assert
            Assert.AreEqual(2, issues.Count);
            Assert.IsTrue(issues.Any(i => i.IssueType == IssueType.ExcessiveMaterialization));
            Assert.IsTrue(issues.Any(i => i.IssueType == IssueType.CallbackDataID));
        }

        [TestMethod]
        public void DetectNodeIssues_WithRecsFormat_DetectsMaterialization()
        {
            // Arrange - some plans use #Recs= instead of #Records=
            var node = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "SpoolLookup: IterPhyOp #Recs=500000"
            };

            // Act
            var issues = _detector.DetectNodeIssues(node);

            // Assert
            Assert.AreEqual(1, issues.Count);
            Assert.AreEqual(IssueType.ExcessiveMaterialization, issues[0].IssueType);
        }

        [TestMethod]
        public void Settings_DefaultValues_AreCorrect()
        {
            // Assert
            Assert.AreEqual(100000, _detector.Settings.ExcessiveMaterializationThreshold);
            Assert.AreEqual(1000000, _detector.Settings.ExcessiveMaterializationErrorThreshold);
            Assert.IsTrue(_detector.Settings.DetectCallbackDataId);
        }

        [TestMethod]
        public void DetectIssues_WithSameRowCountInPath_KeepsOnlyLeafMost()
        {
            // Arrange - ancestor and descendant both have 200,000 rows
            // The deduplication should keep only the descendant (leaf-most)
            var ancestor = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Spool_Iterator #Records=200000"
            };
            var descendant = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "Spool_Iterator #Records=200000",
                Parent = ancestor
            };

            var plan = new EnrichedQueryPlan
            {
                AllNodes = new System.Collections.Generic.List<EnrichedPlanNode> { ancestor, descendant }
            };

            // Act
            var issues = _detector.DetectIssues(plan);

            // Assert - should have only 1 issue (for the descendant)
            var materializationIssues = issues.Where(i => i.IssueType == IssueType.ExcessiveMaterialization).ToList();
            Assert.AreEqual(1, materializationIssues.Count, "Should keep only leaf-most issue per row count");
            Assert.AreEqual(2, materializationIssues[0].AffectedNodeId, "Should be the descendant node");
        }

        [TestMethod]
        public void DetectIssues_WithDifferentRowCounts_KeepsBothIssues()
        {
            // Arrange - ancestor has 200,000 rows, descendant has 300,000 rows
            // Different row counts should not be deduped
            var ancestor = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Spool_Iterator #Records=200000"
            };
            var descendant = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "Spool_Iterator #Records=300000",
                Parent = ancestor
            };

            var plan = new EnrichedQueryPlan
            {
                AllNodes = new System.Collections.Generic.List<EnrichedPlanNode> { ancestor, descendant }
            };

            // Act
            var issues = _detector.DetectIssues(plan);

            // Assert - should have 2 issues (different row counts)
            var materializationIssues = issues.Where(i => i.IssueType == IssueType.ExcessiveMaterialization).ToList();
            Assert.AreEqual(2, materializationIssues.Count, "Different row counts should not be deduped");
        }

        [TestMethod]
        public void DetectIssues_WithSameRowCountInSiblings_AggregatesIntoOne()
        {
            // Arrange - two siblings have the same row count (no ancestor relationship)
            var parent = new EnrichedPlanNode
            {
                NodeId = 1,
                Operation = "Sum_Vertipaq"
            };
            var sibling1 = new EnrichedPlanNode
            {
                NodeId = 2,
                Operation = "Spool_Iterator #Records=200000",
                Parent = parent
            };
            var sibling2 = new EnrichedPlanNode
            {
                NodeId = 3,
                Operation = "Spool_Iterator #Records=200000",
                Parent = parent
            };

            var plan = new EnrichedQueryPlan
            {
                AllNodes = new System.Collections.Generic.List<EnrichedPlanNode> { parent, sibling1, sibling2 }
            };

            // Act
            var issues = _detector.DetectIssues(plan);

            // Assert - should have 1 aggregated issue (multiple siblings with same row count)
            var materializationIssues = issues.Where(i => i.IssueType == IssueType.ExcessiveMaterialization).ToList();
            Assert.AreEqual(1, materializationIssues.Count, "Siblings with same row count should be aggregated into one issue");
            Assert.IsTrue(materializationIssues[0].Description.Contains("2 Spool"), "Should indicate count in description");
        }
    }
}
