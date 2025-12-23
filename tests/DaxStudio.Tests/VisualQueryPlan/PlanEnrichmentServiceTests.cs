using Caliburn.Micro;
using DaxStudio.UI.Model;
using DaxStudio.UI.Services;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DaxStudio.Tests.VisualQueryPlan
{
    [TestClass]
    public class PlanEnrichmentServiceTests
    {
        private PlanEnrichmentService _service;

        [TestInitialize]
        public void TestSetup()
        {
            _service = new PlanEnrichmentService();
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_WithValidData_ReturnsEnrichedPlan()
        {
            // Arrange
            var rawPlan = CreateSamplePhysicalPlan();

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rawPlan,
                timingEvents: new List<TraceStorageEngineEvent>(),
                columnResolver: null,
                activityId: "test-activity-id");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("test-activity-id", result.ActivityID);
            Assert.AreEqual(PlanType.Physical, result.PlanType);
            Assert.AreEqual(PlanState.Enriched, result.State);
            Assert.IsTrue(result.AllNodes.Count > 0);
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_WithNullPlan_ReturnsEmptyPlan()
        {
            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rawPlan: null,
                timingEvents: null,
                columnResolver: null,
                activityId: "test");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.NodeCount);
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_BuildsTreeStructure()
        {
            // Arrange
            var rawPlan = CreateSamplePhysicalPlan();

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rawPlan,
                timingEvents: new List<TraceStorageEngineEvent>(),
                columnResolver: null,
                activityId: "test");

            // Assert
            Assert.IsNotNull(result.RootNode);
            Assert.AreEqual(0, result.RootNode.Level);
            Assert.IsTrue(result.RootNode.Children.Count > 0);
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_DetectsIssues()
        {
            // Arrange
            var rawPlan = CreatePlanWithExcessiveMaterialization();

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rawPlan,
                timingEvents: new List<TraceStorageEngineEvent>(),
                columnResolver: null,
                activityId: "test");

            // Assert
            Assert.IsTrue(result.HasIssues);
            Assert.IsTrue(result.Issues.Any(i => i.IssueType == IssueType.ExcessiveMaterialization));
        }

        [TestMethod]
        public async Task EnrichLogicalPlanAsync_WithValidData_ReturnsEnrichedPlan()
        {
            // Arrange
            var rawPlan = CreateSampleLogicalPlan();

            // Act
            var result = await _service.EnrichLogicalPlanAsync(
                rawPlan,
                columnResolver: null,
                activityId: "test-logical");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("test-logical", result.ActivityID);
            Assert.AreEqual(PlanType.Logical, result.PlanType);
            Assert.AreEqual(PlanState.Enriched, result.State);
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_AssignsEngineTypes()
        {
            // Arrange
            var rawPlan = CreateSamplePhysicalPlan();

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rawPlan,
                timingEvents: new List<TraceStorageEngineEvent>(),
                columnResolver: null,
                activityId: "test");

            // Assert
            var vertipaqNode = result.AllNodes.FirstOrDefault(n => n.Operation.Contains("Scan_Vertipaq"));
            Assert.IsNotNull(vertipaqNode);
            Assert.AreEqual(EngineType.StorageEngine, vertipaqNode.EngineType);
        }

        private BindableCollection<PhysicalQueryPlanRow> CreateSamplePhysicalPlan()
        {
            var rows = new BindableCollection<PhysicalQueryPlanRow>();

            var row1 = new PhysicalQueryPlanRow();
            row1.PrepareQueryPlanRow("AddColumns: RelLogOp DependOnCols()() 0-3 RequiredCols(0, 1)", 1);
            rows.Add(row1);

            var row2 = new PhysicalQueryPlanRow();
            row2.PrepareQueryPlanRow("\tScan_Vertipaq: RelLogOp DependOnCols()() #Records=1000", 2);
            rows.Add(row2);

            var row3 = new PhysicalQueryPlanRow();
            row3.PrepareQueryPlanRow("\tSum_Vertipaq: ScaLogOp MeasureRef=[Total] #Records=100", 3);
            rows.Add(row3);

            return rows;
        }

        private BindableCollection<PhysicalQueryPlanRow> CreatePlanWithExcessiveMaterialization()
        {
            var rows = new BindableCollection<PhysicalQueryPlanRow>();

            var row1 = new PhysicalQueryPlanRow();
            row1.PrepareQueryPlanRow("GroupSemijoin: IterPhyOp", 1);
            rows.Add(row1);

            var row2 = new PhysicalQueryPlanRow();
            row2.PrepareQueryPlanRow("\tSpool_Iterator: IterPhyOp #Records=1500000", 2);
            rows.Add(row2);

            return rows;
        }

        private BindableCollection<LogicalQueryPlanRow> CreateSampleLogicalPlan()
        {
            var rows = new BindableCollection<LogicalQueryPlanRow>();

            var row1 = new LogicalQueryPlanRow();
            row1.PrepareQueryPlanRow("Order: RelLogOp DependOnCols()()", 1);
            rows.Add(row1);

            var row2 = new LogicalQueryPlanRow();
            row2.PrepareQueryPlanRow("\tFilter: RelLogOp DependOnCols()()", 2);
            rows.Add(row2);

            return rows;
        }
    }
}
