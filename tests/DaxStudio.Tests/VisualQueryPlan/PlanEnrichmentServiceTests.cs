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

        #region Timing Correlation Tests

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_WithTimingEvents_CorrelatesData()
        {
            // Arrange
            var rawPlan = CreatePlanWithScanNodes();
            var timingEvents = CreateSampleTimingEvents();

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rawPlan,
                timingEvents,
                columnResolver: null,
                activityId: "test");

            // Assert
            var scanNode = result.AllNodes.FirstOrDefault(n => n.Operation.Contains("Scan_Vertipaq"));
            Assert.IsNotNull(scanNode, "Should find Scan_Vertipaq node");
            Assert.AreEqual(100, scanNode.DurationMs, "Duration should be correlated from timing event");
            Assert.AreEqual(50, scanNode.CpuTimeMs, "CPU time should be correlated from timing event");
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_ReconcilesRowCounts_WhenPlanShowsZero()
        {
            // Arrange - plan with 0 records, includes table reference for matching
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var row = new PhysicalQueryPlanRow();
            row.PrepareQueryPlanRow("Scan_Vertipaq: RelLogOp RequiredCols(0)('Customer'[FirstName]) #Records=0", 1);
            rows.Add(row);

            // Timing event with estimated rows and ObjectName for matching
            var timingEvents = new List<TraceStorageEngineEvent>
            {
                new TraceStorageEngineEvent
                {
                    Duration = 100,
                    CpuTime = 50,
                    EstimatedRows = 3655,  // Server timing shows actual rows
                    ObjectName = "Customer",  // Must match table name in operation
                    Query = "SELECT..."
                }
            };

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rows,
                timingEvents,
                columnResolver: null,
                activityId: "test");

            // Assert
            var scanNode = result.AllNodes.First();
            Assert.AreEqual(3655, scanNode.Records, "Records should be reconciled from timing event EstimatedRows");
            Assert.AreEqual("ServerTiming", scanNode.RecordsSource, "RecordsSource should indicate data came from ServerTiming");
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_CalculatesParallelism()
        {
            // Arrange - includes table reference for matching
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var row = new PhysicalQueryPlanRow();
            row.PrepareQueryPlanRow("Scan_Vertipaq: RelLogOp RequiredCols(0)('Sales'[Amount]) #Records=1000", 1);
            rows.Add(row);

            // Timing event with parallelism data and ObjectName for matching
            var timingEvents = new List<TraceStorageEngineEvent>
            {
                new TraceStorageEngineEvent
                {
                    Duration = 160,           // Total duration
                    NetParallelDuration = 10, // Net duration after parallelism
                    CpuTime = 150,
                    ObjectName = "Sales",     // Must match table name in operation
                    Query = "SELECT..."
                }
            };

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rows,
                timingEvents,
                columnResolver: null,
                activityId: "test");

            // Assert
            var scanNode = result.AllNodes.First();
            Assert.AreEqual(160, scanNode.DurationMs);
            Assert.AreEqual(10, scanNode.NetParallelDurationMs);
            Assert.AreEqual(16, scanNode.Parallelism, "Parallelism should be Duration/NetParallelDuration = 160/10 = 16");
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_MatchesMultipleTablesByName()
        {
            // Arrange - Plan with multiple Scan_Vertipaq nodes for different tables
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var row1 = new PhysicalQueryPlanRow();
            row1.PrepareQueryPlanRow("AddColumns: RelLogOp", 1);
            rows.Add(row1);

            var row2 = new PhysicalQueryPlanRow();
            row2.PrepareQueryPlanRow("\tScan_Vertipaq: RelLogOp RequiredCols(0)('Customer'[FirstName]) #Records=0", 2);
            rows.Add(row2);

            var row3 = new PhysicalQueryPlanRow();
            row3.PrepareQueryPlanRow("\tScan_Vertipaq: RelLogOp RequiredCols(0)('Internet Sales'[Margin]) #Records=0", 3);
            rows.Add(row3);

            // Timing events matched by ObjectName
            var timingEvents = new List<TraceStorageEngineEvent>
            {
                new TraceStorageEngineEvent { Duration = 50, CpuTime = 25, EstimatedRows = 673, ObjectName = "Customer" },
                new TraceStorageEngineEvent { Duration = 30, CpuTime = 15, EstimatedRows = 1, ObjectName = "Internet Sales" }
            };

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rows,
                timingEvents,
                columnResolver: null,
                activityId: "test");

            // Assert
            var customerNode = result.AllNodes.FirstOrDefault(n => n.Operation.Contains("'Customer'"));
            var salesNode = result.AllNodes.FirstOrDefault(n => n.Operation.Contains("'Internet Sales'"));

            Assert.IsNotNull(customerNode, "Should find Customer scan node");
            Assert.AreEqual(50, customerNode.DurationMs, "Customer node should get timing data");
            Assert.AreEqual(673, customerNode.Records, "Customer node should get EstimatedRows");

            Assert.IsNotNull(salesNode, "Should find Internet Sales scan node");
            Assert.AreEqual(30, salesNode.DurationMs, "Internet Sales node should get timing data");
            Assert.AreEqual(1, salesNode.Records, "Internet Sales node should get EstimatedRows");
        }

        #endregion

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_WithMissingObjectName_NoCorrelation()
        {
            // Arrange - timing event WITHOUT ObjectName should not match
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var row = new PhysicalQueryPlanRow();
            row.PrepareQueryPlanRow("Scan_Vertipaq: RelLogOp RequiredCols(0)('Sales'[Amount]) #Records=0", 1);
            rows.Add(row);

            // Timing event missing ObjectName - cannot match
            var timingEvents = new List<TraceStorageEngineEvent>
            {
                new TraceStorageEngineEvent
                {
                    Duration = 100,
                    CpuTime = 50,
                    EstimatedRows = 5000,
                    ObjectName = null,  // Missing ObjectName!
                    Query = "SELECT..."
                }
            };

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rows,
                timingEvents,
                columnResolver: null,
                activityId: "test");

            // Assert - node should NOT have timing data because ObjectName was missing
            var scanNode = result.AllNodes.First();
            Assert.IsNull(scanNode.DurationMs, "Duration should not be populated when ObjectName is missing");
            Assert.IsNull(scanNode.CpuTimeMs, "CpuTime should not be populated when ObjectName is missing");
            Assert.AreEqual(0, scanNode.Records, "Records should remain 0 when no timing match");
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_WithMismatchedObjectName_NoCorrelation()
        {
            // Arrange - timing event with wrong ObjectName should not match
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var row = new PhysicalQueryPlanRow();
            row.PrepareQueryPlanRow("Scan_Vertipaq: RelLogOp RequiredCols(0)('Sales'[Amount]) #Records=0", 1);
            rows.Add(row);

            // Timing event with wrong ObjectName
            var timingEvents = new List<TraceStorageEngineEvent>
            {
                new TraceStorageEngineEvent
                {
                    Duration = 100,
                    CpuTime = 50,
                    EstimatedRows = 5000,
                    ObjectName = "Customer",  // Wrong table name!
                    Query = "SELECT..."
                }
            };

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rows,
                timingEvents,
                columnResolver: null,
                activityId: "test");

            // Assert - node should NOT have timing data because ObjectName didn't match
            var scanNode = result.AllNodes.First();
            Assert.IsNull(scanNode.DurationMs, "Duration should not be populated when ObjectName doesn't match");
        }

        [TestMethod]
        public async Task EnrichPhysicalPlanAsync_PopulatesXmSql()
        {
            // Arrange
            var rows = new BindableCollection<PhysicalQueryPlanRow>();
            var row = new PhysicalQueryPlanRow();
            row.PrepareQueryPlanRow("Scan_Vertipaq: RelLogOp RequiredCols(0)('Product'[Color]) #Records=0", 1);
            rows.Add(row);

            var timingEvents = new List<TraceStorageEngineEvent>
            {
                new TraceStorageEngineEvent
                {
                    Duration = 25,
                    ObjectName = "Product",
                    Query = "SELECT 'Product'[Color] FROM 'Product' WHERE..."
                }
            };

            // Act
            var result = await _service.EnrichPhysicalPlanAsync(
                rows,
                timingEvents,
                columnResolver: null,
                activityId: "test");

            // Assert
            var scanNode = result.AllNodes.First();
            Assert.IsNotNull(scanNode.XmSql, "XmSql should be populated from timing event");
            Assert.IsTrue(scanNode.XmSql.Contains("Product"), "XmSql should contain the query text");
        }

        #region Cross-Reference Tests

        [TestMethod]
        public void CrossReferenceLogicalWithPhysical_InfersEngineType()
        {
            // Arrange
            var logicalPlan = new EnrichedQueryPlan
            {
                PlanType = PlanType.Logical,
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Sum_Vertipaq: ScaLogOp",
                        EngineType = EngineType.Unknown
                    }
                }
            };

            var physicalPlan = new EnrichedQueryPlan
            {
                PlanType = PlanType.Physical,
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Sum_Vertipaq: PhyOp",
                        EngineType = EngineType.StorageEngine,
                        DurationMs = 100,
                        Records = 500
                    }
                }
            };

            // Act
            _service.CrossReferenceLogicalWithPhysical(logicalPlan, physicalPlan);

            // Assert
            var logicalNode = logicalPlan.AllNodes.First();
            Assert.AreEqual(EngineType.StorageEngine, logicalNode.EngineType, "Engine type should be inferred from physical plan");
        }

        [TestMethod]
        public void CrossReferenceLogicalWithPhysical_InheritsTiming()
        {
            // Arrange
            var logicalPlan = new EnrichedQueryPlan
            {
                PlanType = PlanType.Logical,
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Scan_Vertipaq: RelLogOp",
                        EngineType = EngineType.Unknown,
                        DurationMs = null
                    }
                }
            };

            var physicalPlan = new EnrichedQueryPlan
            {
                PlanType = PlanType.Physical,
                AllNodes = new List<EnrichedPlanNode>
                {
                    new EnrichedPlanNode
                    {
                        NodeId = 1,
                        Operation = "Scan_Vertipaq: PhyOp",
                        EngineType = EngineType.StorageEngine,
                        DurationMs = 150,
                        CpuTimeMs = 100,
                        Parallelism = 8,
                        XmSql = "SELECT FROM 'Sales'..."
                    }
                }
            };

            // Act
            _service.CrossReferenceLogicalWithPhysical(logicalPlan, physicalPlan);

            // Assert
            var logicalNode = logicalPlan.AllNodes.First();
            Assert.AreEqual(150, logicalNode.DurationMs, "Duration should be inherited from physical");
            Assert.AreEqual(100, logicalNode.CpuTimeMs, "CPU time should be inherited");
            Assert.AreEqual(8, logicalNode.Parallelism, "Parallelism should be inherited");
            Assert.AreEqual("SELECT FROM 'Sales'...", logicalNode.XmSql, "xmSQL should be inherited");
        }

        #endregion

        #region Helper Methods for New Tests

        private BindableCollection<PhysicalQueryPlanRow> CreatePlanWithScanNodes()
        {
            var rows = new BindableCollection<PhysicalQueryPlanRow>();

            var row1 = new PhysicalQueryPlanRow();
            row1.PrepareQueryPlanRow("AddColumns: RelLogOp", 1);
            rows.Add(row1);

            // Include table reference for table-name matching
            var row2 = new PhysicalQueryPlanRow();
            row2.PrepareQueryPlanRow("\tScan_Vertipaq: RelLogOp RequiredCols(0)('Sales'[Amount]) #Records=0", 2);
            rows.Add(row2);

            return rows;
        }

        private List<TraceStorageEngineEvent> CreateSampleTimingEvents()
        {
            return new List<TraceStorageEngineEvent>
            {
                new TraceStorageEngineEvent
                {
                    Duration = 100,
                    CpuTime = 50,
                    NetParallelDuration = 25,
                    EstimatedRows = 1000,
                    EstimatedKBytes = 100,
                    ObjectName = "Sales",  // Must match table name in operation
                    Query = "SELECT * FROM 'Sales'..."
                }
            };
        }

        #endregion
    }
}
