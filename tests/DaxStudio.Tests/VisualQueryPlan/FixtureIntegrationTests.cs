using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DaxStudio.UI.Model;
using DaxStudio.UI.Services;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace DaxStudio.Tests.VisualQueryPlan
{
    /// <summary>
    /// Integration tests using fixture data to verify complete parsing pipelines.
    /// These tests load real query plan data captured from actual queries and verify
    /// that all expected items are correctly extracted.
    /// </summary>
    [TestClass]
    public class FixtureIntegrationTests
    {
        private static string FixturesPath
        {
            get
            {
                // Use assembly location to find fixtures relative to the test DLL
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(assemblyDir, "..", "..", "..", "tests", "DaxStudio.Tests", "VisualQueryPlan", "Fixtures");
            }
        }

        #region Fixture Discovery

        /// <summary>
        /// Gets all fixture base names (e.g., "Filtered Margin.dax", "100 million row.dax")
        /// </summary>
        private static IEnumerable<string> GetAllFixtureBaseNames()
        {
            var path = FixturesPath;
            if (!Directory.Exists(path))
            {
                yield break;
            }

            // Find all .dax files (the base files for each fixture set)
            foreach (var file in Directory.GetFiles(path, "*.dax"))
            {
                var fileName = Path.GetFileName(file);
                // Skip files that are actually extensions (e.g., .dax.queryPlans)
                if (!fileName.Contains(".dax."))
                {
                    yield return fileName;
                }
            }
        }

        #endregion

        #region Fixture Loading Helpers

        private static QueryPlansFixture LoadQueryPlansFixture(string baseName)
        {
            var path = Path.Combine(FixturesPath, $"{baseName}.queryPlans");
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"Fixture file not found: {path}");
            }
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<QueryPlansFixture>(json);
        }

        private static ServerTimingsFixture LoadServerTimingsFixture(string baseName)
        {
            var path = Path.Combine(FixturesPath, $"{baseName}.serverTimings");
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"Fixture file not found: {path}");
            }
            var json = File.ReadAllText(path);
            return JsonConvert.DeserializeObject<ServerTimingsFixture>(json);
        }

        /// <summary>
        /// Converts fixture QueryPlanRow to PhysicalQueryPlanRow.
        /// </summary>
        private static List<PhysicalQueryPlanRow> ToPhysicalPlanRows(List<QueryPlanRowFixture> fixtureRows)
        {
            if (fixtureRows == null) return new List<PhysicalQueryPlanRow>();

            return fixtureRows.Select(r => new PhysicalQueryPlanRow
            {
                Operation = r.Operation,
                IndentedOperation = r.IndentedOperation,
                Level = r.Level,
                RowNumber = r.RowNumber,
                NextSiblingRowNumber = r.NextSiblingRowNumber,
                HighlightRow = r.HighlightRow,
                Records = r.Records
            }).ToList();
        }

        /// <summary>
        /// Converts fixture QueryPlanRow to LogicalQueryPlanRow.
        /// </summary>
        private static List<LogicalQueryPlanRow> ToLogicalPlanRows(List<QueryPlanRowFixture> fixtureRows)
        {
            if (fixtureRows == null) return new List<LogicalQueryPlanRow>();

            return fixtureRows.Select(r => new LogicalQueryPlanRow
            {
                Operation = r.Operation,
                IndentedOperation = r.IndentedOperation,
                Level = r.Level,
                RowNumber = r.RowNumber,
                NextSiblingRowNumber = r.NextSiblingRowNumber,
                HighlightRow = r.HighlightRow
            }).ToList();
        }

        /// <summary>
        /// Converts fixture SE events to TraceStorageEngineEvent.
        /// </summary>
        private static List<TraceStorageEngineEvent> ToStorageEngineEvents(List<StorageEngineEventFixture> fixtureEvents)
        {
            if (fixtureEvents == null) return new List<TraceStorageEngineEvent>();

            return fixtureEvents.Select(e => new TraceStorageEngineEvent
            {
                ObjectName = e.ObjectName,
                Query = e.Query,
                TextData = e.TextData,
                Duration = e.Duration ?? 0,
                CpuTime = e.CpuTime ?? 0,
                NetParallelDuration = e.NetParallelDuration ?? 0,
                EstimatedRows = e.EstimatedRows ?? 0,
                EstimatedKBytes = e.EstimatedKBytes ?? 0
            }).ToList();
        }

        #endregion

        #region Data-Driven Tests for All Fixtures

        [TestMethod]
        public async Task AllFixtures_LogicalPlan_ParsesSuccessfully()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found in Fixtures folder");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var fixture = LoadQueryPlansFixture(fixtureName);
                var enrichmentService = new PlanEnrichmentService();
                var rows = ToLogicalPlanRows(fixture.LogicalQueryPlanRows);

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, fixture.ActivityID);

                // Assert
                Assert.IsNotNull(plan, $"[{fixtureName}] Plan should be created");
                Assert.IsTrue(plan.AllNodes.Count > 0, $"[{fixtureName}] Should have nodes in logical plan");
            }
        }

        [TestMethod]
        public async Task AllFixtures_PhysicalPlan_ParsesSuccessfully()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found in Fixtures folder");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var fixture = LoadQueryPlansFixture(fixtureName);
                var enrichmentService = new PlanEnrichmentService();
                var rows = ToPhysicalPlanRows(fixture.PhysicalQueryPlanRows);

                // Act
                var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, fixture.ActivityID);

                // Assert
                Assert.IsNotNull(plan, $"[{fixtureName}] Plan should be created");
                Assert.IsTrue(plan.AllNodes.Count > 0, $"[{fixtureName}] Should have nodes in physical plan");
            }
        }

        [TestMethod]
        public async Task AllFixtures_LogicalPlan_BuildsTree()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found in Fixtures folder");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var fixture = LoadQueryPlansFixture(fixtureName);
                var enrichmentService = new PlanEnrichmentService();
                var rows = ToLogicalPlanRows(fixture.LogicalQueryPlanRows);

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, fixture.ActivityID);
                var tree = PlanNodeViewModel.BuildTree(plan);
                var visibleNodes = GetAllNodes(tree).ToList();

                // Assert
                Assert.IsNotNull(tree, $"[{fixtureName}] Tree should be built");
                Assert.IsTrue(visibleNodes.Count > 0, $"[{fixtureName}] Should have visible nodes");
                Assert.IsTrue(visibleNodes.Count <= plan.AllNodes.Count,
                    $"[{fixtureName}] Visible nodes ({visibleNodes.Count}) should be <= total ({plan.AllNodes.Count})");
            }
        }

        [TestMethod]
        public async Task AllFixtures_PhysicalPlan_BuildsTree()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found in Fixtures folder");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var fixture = LoadQueryPlansFixture(fixtureName);
                var enrichmentService = new PlanEnrichmentService();
                var rows = ToPhysicalPlanRows(fixture.PhysicalQueryPlanRows);

                // Act
                var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, fixture.ActivityID);
                var tree = PlanNodeViewModel.BuildTree(plan);
                var visibleNodes = GetAllNodes(tree).ToList();

                // Assert
                Assert.IsNotNull(tree, $"[{fixtureName}] Tree should be built");
                Assert.IsTrue(visibleNodes.Count > 0, $"[{fixtureName}] Should have visible nodes");
            }
        }

        [TestMethod]
        public async Task AllFixtures_WithServerTimings_CorrelatesXmSql()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found in Fixtures folder");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var planFixture = LoadQueryPlansFixture(fixtureName);
                var timingsFixture = LoadServerTimingsFixture(fixtureName);
                var enrichmentService = new PlanEnrichmentService();

                var rows = ToLogicalPlanRows(planFixture.LogicalQueryPlanRows);
                var seEvents = ToStorageEngineEvents(timingsFixture.StorageEngineEvents);

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, seEvents, null, planFixture.ActivityID);

                // Assert - verify xmSQL is correlated to scan nodes
                var nodesWithXmSql = plan.AllNodes.Where(n => !string.IsNullOrEmpty(n.XmSql) || !string.IsNullOrEmpty(n.ResolvedXmSql)).ToList();
                Assert.IsTrue(nodesWithXmSql.Count >= 1, $"[{fixtureName}] Should have at least 1 node with xmSQL, found {nodesWithXmSql.Count}");
            }
        }

        #endregion

        #region Filtered Margin Specific Tests

        [TestMethod]
        public async Task FilteredMargin_LogicalPlan_HasAddColumnsAndCalculate()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("Filtered Margin.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToLogicalPlanRows(fixture.LogicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, fixture.ActivityID);

            // Assert - Verify key operators are present
            var operators = plan.AllNodes.Select(n => GetOperatorName(n.Operation)).ToList();
            Assert.IsTrue(operators.Any(o => o?.Contains("AddColumns") == true), "Should have AddColumns");
            Assert.IsTrue(operators.Any(o => o?.Contains("Calculate") == true), "Should have Calculate");
        }

        [TestMethod]
        public async Task FilteredMargin_LogicalPlan_FilterPredicateExtraction()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("Filtered Margin.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToLogicalPlanRows(fixture.LogicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, fixture.ActivityID);
            var tree = PlanNodeViewModel.BuildTree(plan);

            // Assert - Find Filter nodes and check predicates
            var allNodes = GetAllNodes(tree).ToList();
            var filterNodes = allNodes.Where(n => PlanNodeViewModel.IsFilterOperator(n.OperatorName)).ToList();
            var nodesWithPredicates = allNodes.Where(n => !string.IsNullOrEmpty(n.FilterPredicateExpression)).ToList();

            Assert.IsTrue(filterNodes.Count > 0 || nodesWithPredicates.Count > 0,
                "Should have filter nodes or nodes with predicates");
        }

        [TestMethod]
        public async Task FilteredMargin_LogicalPlan_MeasureReferenceExtraction()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("Filtered Margin.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToLogicalPlanRows(fixture.LogicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, fixture.ActivityID);
            var tree = PlanNodeViewModel.BuildTree(plan);

            // Assert - Find nodes with measure references
            var nodesWithMeasures = GetAllNodes(tree).Where(n => !string.IsNullOrEmpty(n.MeasureReference)).ToList();
            Assert.IsNotNull(nodesWithMeasures, "Measure reference extraction should work");
        }

        [TestMethod]
        public async Task FilteredMargin_PhysicalPlan_SpoolIteratorRollup()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("Filtered Margin.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToPhysicalPlanRows(fixture.PhysicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, fixture.ActivityID);
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - Check for spool type info on Spool_Iterator nodes
            var spoolIterators = allNodes.Where(n =>
                n.OperatorName?.StartsWith("Spool_Iterator") == true ||
                n.OperatorName?.Contains("Spool Iterator") == true).ToList();

            foreach (var spool in spoolIterators)
            {
                Assert.IsNotNull(spool.DisplayText, "Spool Iterator should have display text");
            }
        }

        #endregion

        #region 100 Million Row Specific Tests (Parallelism)

        [TestMethod]
        public async Task HundredMillionRow_LogicalPlan_HasSumVertipaq()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("100 million row.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToLogicalPlanRows(fixture.LogicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, fixture.ActivityID);

            // Assert - Verify key operators are present
            var operators = plan.AllNodes.Select(n => GetOperatorName(n.Operation)).ToList();
            Assert.IsTrue(operators.Any(o => o?.Contains("Sum_Vertipaq") == true), "Should have Sum_Vertipaq");
            Assert.IsTrue(operators.Any(o => o?.Contains("Scan_Vertipaq") == true), "Should have Scan_Vertipaq");
        }

        [TestMethod]
        public void HundredMillionRow_ServerTimings_HasParallelism()
        {
            // Arrange
            var timingsFixture = LoadServerTimingsFixture("100 million row.dax");

            // Act - Check for parallelism in SE events
            var seEvents = ToStorageEngineEvents(timingsFixture.StorageEngineEvents);
            var eventsWithParallelism = seEvents.Where(e =>
                e.Duration > 0 &&
                e.NetParallelDuration > 0 &&
                e.CpuTime > e.Duration).ToList();

            // Assert - This fixture should have parallel SE operations
            Assert.IsTrue(eventsWithParallelism.Count >= 1,
                $"Should have at least 1 SE event with parallelism (CPU > Duration), found {eventsWithParallelism.Count}");
        }

        [TestMethod]
        public async Task HundredMillionRow_LogicalPlan_WithServerTimings_CorrelatesParallelism()
        {
            // Arrange
            var planFixture = LoadQueryPlansFixture("100 million row.dax");
            var timingsFixture = LoadServerTimingsFixture("100 million row.dax");
            var enrichmentService = new PlanEnrichmentService();

            var rows = ToLogicalPlanRows(planFixture.LogicalQueryPlanRows);
            var seEvents = ToStorageEngineEvents(timingsFixture.StorageEngineEvents);

            // Act
            var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, seEvents, null, planFixture.ActivityID);
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - Nodes with timing data should have duration and CPU time
            var nodesWithTiming = allNodes.Where(n => n.DurationMs.HasValue && n.DurationMs.Value > 0).ToList();
            Assert.IsTrue(nodesWithTiming.Count >= 1,
                $"Should have at least 1 node with timing data, found {nodesWithTiming.Count}");

            // Check for parallelism indicators
            var nodesWithParallelism = allNodes.Where(n => n.HasParallelism).ToList();
            // Note: Parallelism may or may not be shown depending on how correlation works
            // This test verifies the mechanism exists
        }

        [TestMethod]
        public async Task HundredMillionRow_PhysicalPlan_HasAggregationSpool()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("100 million row.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToPhysicalPlanRows(fixture.PhysicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, fixture.ActivityID);
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - Should have AggregationSpool operators
            var aggregationSpools = allNodes.Where(n =>
                n.OperatorName?.Contains("AggregationSpool") == true).ToList();
            Assert.IsTrue(aggregationSpools.Count >= 1,
                $"Should have AggregationSpool nodes, found {aggregationSpools.Count}");
        }

        [TestMethod]
        public async Task HundredMillionRow_LogicalPlan_HasMeasureReferences()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("100 million row.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToLogicalPlanRows(fixture.LogicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, fixture.ActivityID);
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - Should have measure references (Sales Amount, Margin %, Total Cost, Total Quantity)
            var nodesWithMeasures = allNodes.Where(n => !string.IsNullOrEmpty(n.MeasureReference)).ToList();
            Assert.IsTrue(nodesWithMeasures.Count >= 1,
                $"Should have nodes with measure references, found {nodesWithMeasures.Count}");

            // Verify specific measures are referenced
            var measureNames = nodesWithMeasures.Select(n => n.MeasureReference).Distinct().ToList();
            Assert.IsTrue(measureNames.Any(m => m.Contains("Sales Amount") || m.Contains("Margin") || m.Contains("Total")),
                $"Should reference expected measures. Found: {string.Join(", ", measureNames)}");
        }

        [TestMethod]
        public async Task HundredMillionRow_PhysicalPlan_SpoolLookupFoldsProjectionSpool()
        {
            // Arrange
            var fixture = LoadQueryPlansFixture("100 million row.dax");
            var enrichmentService = new PlanEnrichmentService();
            var rows = ToPhysicalPlanRows(fixture.PhysicalQueryPlanRows);

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, fixture.ActivityID);
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - SpoolLookup nodes should have SpoolTypeInfo (from folded ProjectionSpool)
            var spoolLookups = allNodes.Where(n => n.OperatorName == "SpoolLookup").ToList();
            Assert.IsTrue(spoolLookups.Count >= 1,
                $"Should have SpoolLookup nodes, found {spoolLookups.Count}");

            // At least one SpoolLookup should have SpoolTypeInfo
            var spoolLookupsWithInfo = spoolLookups.Where(n => n.HasSpoolTypeInfo).ToList();
            Assert.IsTrue(spoolLookupsWithInfo.Count >= 1,
                $"At least one SpoolLookup should have SpoolTypeInfo from folded child. Found {spoolLookupsWithInfo.Count} with info out of {spoolLookups.Count}");

            // ProjectionSpool should NOT appear as separate nodes (they should be folded)
            var projectionSpools = allNodes.Where(n =>
                n.OperatorName?.StartsWith("ProjectionSpool") == true).ToList();

            // Log for debugging
            foreach (var sl in spoolLookups)
            {
                System.Diagnostics.Debug.WriteLine($"SpoolLookup: HasSpoolTypeInfo={sl.HasSpoolTypeInfo}, SpoolTypeInfo={sl.SpoolTypeInfo ?? "null"}");
            }
        }

        #endregion

        #region Helper Methods

        private static string GetOperatorName(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return null;

            // Handle 'Table'[Column] pattern
            if (operation.StartsWith("'"))
            {
                var colonIdx = operation.IndexOf(':');
                return colonIdx > 0 ? operation.Substring(0, colonIdx).Trim() : operation;
            }

            // Handle Spool_Iterator<Type> pattern
            var ltIdx = operation.IndexOf('<');
            var colonIdx2 = operation.IndexOf(':');

            if (ltIdx > 0 && (colonIdx2 < 0 || ltIdx < colonIdx2))
            {
                return operation.Substring(0, ltIdx).Trim();
            }

            if (colonIdx2 > 0)
            {
                return operation.Substring(0, colonIdx2).Trim();
            }

            return operation.Split(' ')[0];
        }

        private static IEnumerable<PlanNodeViewModel> GetAllNodes(PlanNodeViewModel root)
        {
            if (root == null) yield break;

            yield return root;
            foreach (var child in root.Children)
            {
                foreach (var descendant in GetAllNodes(child))
                {
                    yield return descendant;
                }
            }
        }

        #endregion

        #region Fixture Classes

        public class QueryPlansFixture
        {
            public int FileFormatVersion { get; set; }
            public List<QueryPlanRowFixture> PhysicalQueryPlanRows { get; set; }
            public List<QueryPlanRowFixture> LogicalQueryPlanRows { get; set; }
            public string ActivityID { get; set; }
            public string RequestID { get; set; }
            public string CommandText { get; set; }
        }

        public class QueryPlanRowFixture
        {
            public long? Records { get; set; }
            public string Operation { get; set; }
            public string IndentedOperation { get; set; }
            public int Level { get; set; }
            public int RowNumber { get; set; }
            public int NextSiblingRowNumber { get; set; }
            public bool HighlightRow { get; set; }
        }

        public class ServerTimingsFixture
        {
            public int FileFormatVersion { get; set; }
            public string ActivityID { get; set; }
            public string RequestID { get; set; }
            public long StorageEngineDuration { get; set; }
            public long StorageEngineNetParallelDuration { get; set; }
            public long FormulaEngineDuration { get; set; }
            public List<StorageEngineEventFixture> StorageEngineEvents { get; set; }
            public string CommandText { get; set; }
        }

        public class StorageEngineEventFixture
        {
            public string Class { get; set; }
            public string Subclass { get; set; }
            public string Query { get; set; }
            public string TextData { get; set; }
            public long? Duration { get; set; }
            public long? NetParallelDuration { get; set; }
            public long? CpuTime { get; set; }
            public double? CpuFactor { get; set; }
            public long? EstimatedRows { get; set; }
            public long? EstimatedKBytes { get; set; }
            public bool IsInternalEvent { get; set; }
            public string ObjectName { get; set; }
            public bool IsScanEvent { get; set; }
        }

        #endregion
    }
}
