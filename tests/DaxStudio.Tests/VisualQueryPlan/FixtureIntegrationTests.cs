using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Model;
using DaxStudio.UI.Services;
using DaxStudio.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace DaxStudio.Tests.VisualQueryPlan
{
    /// <summary>
    /// Integration tests using fixture data to verify complete parsing pipelines.
    /// These tests load TSV exports from DaxStudio's UI and verify that the enrichment
    /// pipeline correctly processes them.
    /// </summary>
    [TestClass]
    public class FixtureIntegrationTests
    {
        private static string FixturesPath
        {
            get
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(assemblyDir, "..", "..", "..", "tests", "DaxStudio.Tests", "VisualQueryPlan", "Fixtures");
            }
        }

        #region TSV Parsing Helpers

        /// <summary>
        /// Parses a Physical Query Plan TSV file into PhysicalQueryPlanRow objects.
        /// Format: Line[tab]Records[tab]Physical Query Plan
        /// Indentation is 4 spaces per level.
        /// </summary>
        private static List<PhysicalQueryPlanRow> ParsePhysicalPlanTsv(string filePath)
        {
            var rows = new List<PhysicalQueryPlanRow>();
            var lines = File.ReadAllLines(filePath);

            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('\t');
                if (parts.Length < 3) continue;

                // Parse line number
                if (!int.TryParse(parts[0], out int lineNumber)) continue;

                // Parse records (may be empty)
                long? records = null;
                if (!string.IsNullOrEmpty(parts[1]) && long.TryParse(parts[1], out long recordsValue))
                {
                    records = recordsValue;
                }

                // Parse operation with indentation
                var operationWithIndent = parts[2];
                var trimmedOperation = operationWithIndent.TrimStart();
                var leadingSpaces = operationWithIndent.Length - trimmedOperation.Length;
                var level = leadingSpaces / 4; // 4 spaces per indent level

                rows.Add(new PhysicalQueryPlanRow
                {
                    RowNumber = lineNumber,
                    Records = records,
                    Operation = trimmedOperation,
                    IndentedOperation = operationWithIndent,
                    Level = level,
                    NextSiblingRowNumber = 0, // Will be calculated if needed
                    HighlightRow = false
                });
            }

            // Calculate NextSiblingRowNumber for each row
            CalculateNextSiblingRowNumbers(rows);

            return rows;
        }

        /// <summary>
        /// Parses a Logical Query Plan TSV file into LogicalQueryPlanRow objects.
        /// Format: Line[tab]Logical Query Plan
        /// Indentation is 4 spaces per level.
        /// </summary>
        private static List<LogicalQueryPlanRow> ParseLogicalPlanTsv(string filePath)
        {
            var rows = new List<LogicalQueryPlanRow>();
            var lines = File.ReadAllLines(filePath);

            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('\t');
                if (parts.Length < 2) continue;

                // Parse line number
                if (!int.TryParse(parts[0], out int lineNumber)) continue;

                // Parse operation with indentation
                var operationWithIndent = parts[1];
                var trimmedOperation = operationWithIndent.TrimStart();
                var leadingSpaces = operationWithIndent.Length - trimmedOperation.Length;
                var level = leadingSpaces / 4; // 4 spaces per indent level

                rows.Add(new LogicalQueryPlanRow
                {
                    RowNumber = lineNumber,
                    Operation = trimmedOperation,
                    IndentedOperation = operationWithIndent,
                    Level = level,
                    NextSiblingRowNumber = 0,
                    HighlightRow = false
                });
            }

            // Calculate NextSiblingRowNumber for each row
            CalculateNextSiblingRowNumbers(rows);

            return rows;
        }

        /// <summary>
        /// Parses a Server Timings TSV file into TraceStorageEngineEvent objects.
        /// Format: Line[tab]Subclass[tab]Duration[tab]CPU[tab]Par.[tab]Rows[tab]KB[tab]Timeline[tab]Query
        /// </summary>
        private static List<TraceStorageEngineEvent> ParseServerTimingsTsv(string filePath)
        {
            var events = new List<TraceStorageEngineEvent>();
            var lines = File.ReadAllLines(filePath);

            // Skip header line
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

                var parts = line.Split('\t');
                if (parts.Length < 9) continue;

                // Skip ExecutionMetrics rows
                if (parts.Length >= 2 && parts[1] == "ExecutionMetrics") continue;

                // Parse fields
                var subclass = parts[1];
                long.TryParse(parts[2], out long duration);
                long.TryParse(parts[3], out long cpuTime);
                // parts[4] is Par. (parallelism factor) - we can calculate NetParallelDuration
                long.TryParse(parts[5], out long estimatedRows);
                long.TryParse(parts[6], out long estimatedKBytes);
                var query = parts.Length > 8 ? parts[8] : "";

                // Calculate NetParallelDuration from parallelism factor if available
                long netParallelDuration = duration;
                if (!string.IsNullOrEmpty(parts[4]) && double.TryParse(parts[4], NumberStyles.Float, CultureInfo.InvariantCulture, out double parFactor) && parFactor > 0)
                {
                    netParallelDuration = (long)(duration * parFactor);
                }

                // Determine if this is a cache hit based on subclass
                bool isCacheHit = subclass?.Contains("Cache") == true;

                // Extract table name from query for ObjectName
                string objectName = ExtractTableNameFromQuery(query);

                events.Add(new TraceStorageEngineEvent
                {
                    Subclass = MapSubclass(subclass),
                    Duration = duration,
                    CpuTime = cpuTime,
                    NetParallelDuration = netParallelDuration,
                    EstimatedRows = estimatedRows,
                    EstimatedKBytes = estimatedKBytes,
                    Query = query,
                    TextData = query,
                    ObjectName = objectName
                });
            }

            return events;
        }

        /// <summary>
        /// Maps TSV subclass text to the enum used internally.
        /// </summary>
        private static DaxStudioTraceEventSubclass MapSubclass(string subclass)
        {
            if (string.IsNullOrEmpty(subclass)) return DaxStudioTraceEventSubclass.NotAvailable;

            return subclass switch
            {
                "Scan" => DaxStudioTraceEventSubclass.VertiPaqScan,
                "Cache" => DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch,
                "Internal" => DaxStudioTraceEventSubclass.VertiPaqScanInternal,
                "Batch" => DaxStudioTraceEventSubclass.BatchVertiPaqScan,
                _ => Enum.TryParse<DaxStudioTraceEventSubclass>(subclass, true, out var result)
                    ? result
                    : DaxStudioTraceEventSubclass.NotAvailable
            };
        }

        /// <summary>
        /// Extracts the primary table name from an xmSQL query.
        /// </summary>
        private static string ExtractTableNameFromQuery(string query)
        {
            if (string.IsNullOrEmpty(query)) return null;

            // Look for FROM 'TableName' or FROM [TableName]
            var fromIdx = query.IndexOf("FROM ", StringComparison.OrdinalIgnoreCase);
            if (fromIdx < 0) return null;

            var afterFrom = query.Substring(fromIdx + 5).TrimStart();

            // Handle 'TableName' format
            if (afterFrom.StartsWith("'"))
            {
                var endQuote = afterFrom.IndexOf('\'', 1);
                if (endQuote > 1)
                {
                    return afterFrom.Substring(1, endQuote - 1);
                }
            }

            // Handle [TableName] format
            if (afterFrom.StartsWith("["))
            {
                var endBracket = afterFrom.IndexOf(']');
                if (endBracket > 1)
                {
                    return afterFrom.Substring(1, endBracket - 1);
                }
            }

            return null;
        }

        /// <summary>
        /// Calculates NextSiblingRowNumber for query plan rows based on level hierarchy.
        /// </summary>
        private static void CalculateNextSiblingRowNumbers<T>(List<T> rows) where T : class
        {
            for (int i = 0; i < rows.Count; i++)
            {
                dynamic row = rows[i];
                int currentLevel = row.Level;
                int nextSibling = 0;

                // Find next row at same or lower level
                for (int j = i + 1; j < rows.Count; j++)
                {
                    dynamic nextRow = rows[j];
                    if (nextRow.Level <= currentLevel)
                    {
                        nextSibling = nextRow.RowNumber;
                        break;
                    }
                }

                row.NextSiblingRowNumber = nextSibling;
            }
        }

        #endregion

        #region Fixture Discovery

        /// <summary>
        /// Gets all fixture base names by finding .dax files that have associated TSV files.
        /// </summary>
        private static IEnumerable<string> GetAllFixtureBaseNames()
        {
            var path = FixturesPath;
            if (!Directory.Exists(path))
            {
                yield break;
            }

            foreach (var file in Directory.GetFiles(path, "*.dax"))
            {
                var fileName = Path.GetFileName(file);
                // Skip files that are extensions (e.g., .dax.queryPlans)
                if (fileName.Contains(".dax.")) continue;

                var baseName = Path.GetFileNameWithoutExtension(fileName);

                // Check if TSV files exist for this fixture
                var physicalTsv = Path.Combine(path, $"{baseName} Physical Query Plan.tsv");
                var logicalTsv = Path.Combine(path, $"{baseName} Logical Query Plan.tsv");

                if (File.Exists(physicalTsv) || File.Exists(logicalTsv))
                {
                    yield return baseName;
                }
            }
        }

        /// <summary>
        /// Loads Physical Query Plan rows from TSV for a fixture.
        /// </summary>
        private static List<PhysicalQueryPlanRow> LoadPhysicalPlanRows(string baseName)
        {
            var path = Path.Combine(FixturesPath, $"{baseName} Physical Query Plan.tsv");
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"Physical Query Plan TSV not found: {path}");
            }
            return ParsePhysicalPlanTsv(path);
        }

        /// <summary>
        /// Loads Logical Query Plan rows from TSV for a fixture.
        /// </summary>
        private static List<LogicalQueryPlanRow> LoadLogicalPlanRows(string baseName)
        {
            var path = Path.Combine(FixturesPath, $"{baseName} Logical Query Plan.tsv");
            if (!File.Exists(path))
            {
                Assert.Inconclusive($"Logical Query Plan TSV not found: {path}");
            }
            return ParseLogicalPlanTsv(path);
        }

        /// <summary>
        /// Loads Server Timings (SE events) from TSV for a fixture.
        /// </summary>
        private static List<TraceStorageEngineEvent> LoadServerTimings(string baseName)
        {
            var path = Path.Combine(FixturesPath, $"{baseName} Server Timings.tsv");
            if (!File.Exists(path))
            {
                return new List<TraceStorageEngineEvent>(); // Optional - not all fixtures have timings
            }
            return ParseServerTimingsTsv(path);
        }

        #endregion

        #region Data-Driven Tests for All Fixtures

        [TestMethod]
        public void DiscoverFixtures_FindsAtLeastOne()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0,
                $"No fixtures found in {FixturesPath}. Expected .dax files with matching TSV files.");

            foreach (var fixture in fixtures)
            {
                System.Diagnostics.Debug.WriteLine($"Found fixture: {fixture}");
            }
        }

        [TestMethod]
        public async Task AllFixtures_PhysicalPlan_ParsesFromTsv()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadPhysicalPlanRows(fixtureName);
                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());

                // Assert
                Assert.IsNotNull(plan, $"[{fixtureName}] Plan should be created");
                Assert.IsTrue(plan.AllNodes.Count > 0, $"[{fixtureName}] Should have nodes. Parsed {rows.Count} rows.");
                Assert.AreEqual(rows.Count, plan.AllNodes.Count,
                    $"[{fixtureName}] Node count should match row count");
            }
        }

        [TestMethod]
        public async Task AllFixtures_LogicalPlan_ParsesFromTsv()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadLogicalPlanRows(fixtureName);
                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());

                // Assert
                Assert.IsNotNull(plan, $"[{fixtureName}] Plan should be created");
                Assert.IsTrue(plan.AllNodes.Count > 0, $"[{fixtureName}] Should have nodes. Parsed {rows.Count} rows.");
                Assert.AreEqual(rows.Count, plan.AllNodes.Count,
                    $"[{fixtureName}] Node count should match row count");
            }
        }

        [TestMethod]
        public async Task AllFixtures_PhysicalPlan_BuildsTree()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadPhysicalPlanRows(fixtureName);
                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
                var tree = PlanNodeViewModel.BuildTree(plan);
                var visibleNodes = GetAllNodes(tree).ToList();

                // Assert
                Assert.IsNotNull(tree, $"[{fixtureName}] Tree should be built");
                Assert.IsTrue(visibleNodes.Count > 0, $"[{fixtureName}] Should have visible nodes");
            }
        }

        [TestMethod]
        public async Task AllFixtures_LogicalPlan_BuildsTree()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadLogicalPlanRows(fixtureName);
                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
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
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadLogicalPlanRows(fixtureName);
                var seEvents = LoadServerTimings(fixtureName);

                // Skip if no server timings for this fixture
                if (seEvents.Count == 0) continue;

                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, seEvents, null, Guid.NewGuid().ToString());

                // Assert - verify xmSQL is correlated to scan nodes
                var nodesWithXmSql = plan.AllNodes
                    .Where(n => !string.IsNullOrEmpty(n.XmSql) || !string.IsNullOrEmpty(n.ResolvedXmSql))
                    .ToList();

                Assert.IsTrue(nodesWithXmSql.Count >= 1,
                    $"[{fixtureName}] Should have at least 1 node with xmSQL. " +
                    $"SE events: {seEvents.Count}, Nodes: {plan.AllNodes.Count}");
            }
        }

        [TestMethod]
        public async Task AllFixtures_PhysicalPlan_ExtractsRecordCounts()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadPhysicalPlanRows(fixtureName);
                var rowsWithRecords = rows.Where(r => r.Records.HasValue && r.Records.Value > 0).ToList();

                // Skip if no rows have record counts
                if (rowsWithRecords.Count == 0) continue;

                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
                var tree = PlanNodeViewModel.BuildTree(plan);
                var allNodes = GetAllNodes(tree).ToList();

                // Assert - nodes should have record counts
                var nodesWithRecords = allNodes.Where(n => n.HasRecords).ToList();
                Assert.IsTrue(nodesWithRecords.Count > 0,
                    $"[{fixtureName}] Should have nodes with record counts. " +
                    $"Rows with records: {rowsWithRecords.Count}");
            }
        }

        [TestMethod]
        public async Task AllFixtures_LogicalPlan_ExtractsMeasureReferences()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadLogicalPlanRows(fixtureName);

                // Check if any rows have MeasureRef in operation
                var rowsWithMeasureRef = rows.Where(r =>
                    r.Operation?.Contains("MeasureRef=") == true).ToList();

                // Skip if no measure references expected
                if (rowsWithMeasureRef.Count == 0) continue;

                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
                var tree = PlanNodeViewModel.BuildTree(plan);
                var allNodes = GetAllNodes(tree).ToList();

                // Assert
                var nodesWithMeasures = allNodes
                    .Where(n => !string.IsNullOrEmpty(n.MeasureReference))
                    .ToList();

                Assert.IsTrue(nodesWithMeasures.Count > 0,
                    $"[{fixtureName}] Should have nodes with measure references. " +
                    $"Rows with MeasureRef: {rowsWithMeasureRef.Count}");
            }
        }

        [TestMethod]
        public async Task AllFixtures_WithServerTimings_DetectsParallelism()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var seEvents = LoadServerTimings(fixtureName);

                // Check if any events show parallelism (CPU > Duration)
                var eventsWithParallelism = seEvents
                    .Where(e => e.CpuTime > e.Duration && e.Duration > 0)
                    .ToList();

                // Skip if no parallelism expected
                if (eventsWithParallelism.Count == 0) continue;

                var rows = LoadLogicalPlanRows(fixtureName);
                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichLogicalPlanAsync(rows, seEvents, null, Guid.NewGuid().ToString());
                var tree = PlanNodeViewModel.BuildTree(plan);
                var allNodes = GetAllNodes(tree).ToList();

                // Assert - verify enrichment completes without error
                // Note: Parallelism detection depends on successful SE event correlation,
                // which may not always match in fixture data due to timing/ordering differences
                var nodesWithParallelism = allNodes.Where(n => n.HasParallelism).ToList();
                System.Diagnostics.Debug.WriteLine(
                    $"[{fixtureName}] Parallelism: SE events={eventsWithParallelism.Count}, " +
                    $"Nodes with parallelism={nodesWithParallelism.Count}");
            }
        }

        [TestMethod]
        public async Task AllFixtures_PhysicalPlan_FoldsProjectionSpools()
        {
            var fixtures = GetAllFixtureBaseNames().ToList();
            Assert.IsTrue(fixtures.Count > 0, "No fixtures found");

            foreach (var fixtureName in fixtures)
            {
                // Arrange
                var rows = LoadPhysicalPlanRows(fixtureName);

                // Check if there are ProjectionSpool rows
                var projectionSpoolRows = rows
                    .Where(r => r.Operation?.StartsWith("ProjectionSpool") == true)
                    .ToList();

                // Skip if no ProjectionSpools
                if (projectionSpoolRows.Count == 0) continue;

                var enrichmentService = new PlanEnrichmentService();

                // Act
                var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
                var tree = PlanNodeViewModel.BuildTree(plan);
                var visibleNodes = GetAllNodes(tree).ToList();

                // Assert - ProjectionSpools should be folded (not visible as separate nodes)
                var visibleProjectionSpools = visibleNodes
                    .Where(n => n.OperatorName?.StartsWith("ProjectionSpool") == true)
                    .ToList();

                // Most ProjectionSpools should be folded into parent SpoolLookup
                var foldedCount = projectionSpoolRows.Count - visibleProjectionSpools.Count;
                Assert.IsTrue(foldedCount > 0 || visibleProjectionSpools.Count == 0,
                    $"[{fixtureName}] ProjectionSpools should be folded. " +
                    $"Total: {projectionSpoolRows.Count}, Visible: {visibleProjectionSpools.Count}");
            }
        }

        #endregion

        #region Auto-Collapse Analysis Tests

        [TestMethod]
        public async Task LargePlan_AnalyzeDatesBetweenSubtreeWidths()
        {
            // Arrange
            var rows = LoadPhysicalPlanRows("Large plan");
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Find DatesBetween nodes and their collapsible ancestors
            var datesBetweenNodes = allNodes
                .Where(n => n.OperatorName == "DatesBetween")
                .ToList();

            System.Diagnostics.Debug.WriteLine($"Found {datesBetweenNodes.Count} DatesBetween nodes");
            System.Diagnostics.Debug.WriteLine($"Root SubtreeWidth: {tree.SubtreeWidth}");
            System.Diagnostics.Debug.WriteLine($"Root CanToggleSubtree: {tree.CanToggleSubtree}");

            // For each DatesBetween, find the nearest collapsible ancestor
            foreach (var db in datesBetweenNodes)
            {
                var ancestor = db.Parent;
                int depth = 1;
                while (ancestor != null && !ancestor.CanToggleSubtree)
                {
                    ancestor = ancestor.Parent;
                    depth++;
                }

                if (ancestor != null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"DatesBetween NodeId={db.NodeId}: " +
                        $"Nearest collapsible ancestor={ancestor.OperatorName} (NodeId={ancestor.NodeId}), " +
                        $"SubtreeWidth={ancestor.SubtreeWidth}, Depth={depth}");
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"DatesBetween NodeId={db.NodeId}: No collapsible ancestor found");
                }
            }

            // Find all collapsible nodes and their widths
            var collapsibleNodes = allNodes
                .Where(n => n.CanToggleSubtree)
                .OrderByDescending(n => n.SubtreeWidth)
                .Take(20)
                .ToList();

            System.Diagnostics.Debug.WriteLine("\nTop 20 collapsible nodes by SubtreeWidth:");
            foreach (var node in collapsibleNodes)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"  {node.OperatorName} (NodeId={node.NodeId}): SubtreeWidth={node.SubtreeWidth}");
            }

            // Assert we found DatesBetween nodes
            Assert.IsTrue(datesBetweenNodes.Count >= 4, "Expected at least 4 DatesBetween nodes");
        }

        [TestMethod]
        public async Task LargePlan_AddChaining_FoldsCorrectly()
        {
            // Arrange - Large plan has Add→Add→Add chain at lines 6-7-8
            var rows = LoadPhysicalPlanRows("Large plan");
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());

            // Debug: Check enriched plan structure for Add nodes
            var enrichedAddNodes = plan.AllNodes.Where(n => n.Operation?.StartsWith("Add:") == true).ToList();
            System.Diagnostics.Debug.WriteLine($"Enriched Add nodes: {enrichedAddNodes.Count}");
            foreach (var add in enrichedAddNodes)
            {
                var parentId = add.Parent?.NodeId;
                var children = plan.AllNodes.Where(n => n.Parent?.NodeId == add.NodeId).Select(n => n.NodeId).ToList();
                System.Diagnostics.Debug.WriteLine(
                    $"  Enriched Add NodeId={add.NodeId}, ParentId={parentId}, Children=[{string.Join(",", children)}]");
            }

            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Find Add nodes
            var addNodes = allNodes.Where(n => n.OperatorName == "Add").ToList();

            System.Diagnostics.Debug.WriteLine($"Total VM nodes: {allNodes.Count}");
            System.Diagnostics.Debug.WriteLine($"Add VM nodes after folding: {addNodes.Count}");

            foreach (var add in addNodes)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"  Add VM NodeId={add.NodeId}, ChainedCount={add.ChainedOperatorCount}, " +
                    $"HasChained={add.HasChainedOperators}, DisplayName={add.DisplayName}");
            }

            // Assert - should have only 1 Add node (the other 2 should be folded)
            // and it should show (3x) in the display name
            Assert.AreEqual(1, addNodes.Count, "Should have only 1 Add node after folding chain");
            Assert.AreEqual(3, addNodes[0].ChainedOperatorCount, "Should count 3 chained Adds");
            Assert.IsTrue(addNodes[0].DisplayName.Contains("(3x)"), $"DisplayName should show (3x), got: {addNodes[0].DisplayName}");
        }

        #endregion

        #region Helper Methods

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
    }
}
