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
    }
}
