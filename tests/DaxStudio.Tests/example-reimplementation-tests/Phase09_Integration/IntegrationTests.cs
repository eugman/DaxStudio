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

namespace DaxStudio.Tests.ExampleReimplementation.Phase09_Integration
{
    /// <summary>
    /// Phase 9: Integration Tests (End-to-End)
    ///
    /// These tests verify the complete pipeline from TSV parsing to tree building:
    /// - Loading fixtures
    /// - Parsing TSV into EnrichedPlanNode objects
    /// - Building and folding the tree
    /// - Verifying expected outcomes
    ///
    /// Prerequisites: Phases 1-8 complete
    ///
    /// Reference: docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md - All phases
    /// </summary>
    [TestClass]
    public class IntegrationTests
    {
        private static string FixturesPath
        {
            get
            {
                var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                return Path.Combine(assemblyDir, "..", "..", "..", "tests", "DaxStudio.Tests", "example-reimplementation-tests", "Fixtures");
            }
        }

        #region Fixture Loading Helpers

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

                if (!int.TryParse(parts[0], out int lineNumber)) continue;

                long? records = null;
                if (!string.IsNullOrEmpty(parts[1]) && long.TryParse(parts[1], out long recordsValue))
                {
                    records = recordsValue;
                }

                var operationWithIndent = parts[2];
                var trimmedOperation = operationWithIndent.TrimStart();
                var leadingSpaces = operationWithIndent.Length - trimmedOperation.Length;
                var level = leadingSpaces / 4;

                rows.Add(new PhysicalQueryPlanRow
                {
                    RowNumber = lineNumber,
                    Records = records,
                    Operation = trimmedOperation,
                    IndentedOperation = operationWithIndent,
                    Level = level,
                    NextSiblingRowNumber = 0,
                    HighlightRow = false
                });
            }

            CalculateNextSiblingRowNumbers(rows);
            return rows;
        }

        private static void CalculateNextSiblingRowNumbers<T>(List<T> rows) where T : class
        {
            for (int i = 0; i < rows.Count; i++)
            {
                dynamic row = rows[i];
                int currentLevel = row.Level;
                int nextSibling = 0;

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

        #region Minimal 3-Node Fixture Tests

        [TestMethod]
        public async Task Minimal3Node_ParsesSuccessfully()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Minimal_3Node.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());

            // Assert
            Assert.IsNotNull(plan);
            Assert.AreEqual(3, plan.AllNodes.Count, "Should have 3 nodes");
            Assert.IsNotNull(plan.RootNode, "Should have root node");
            Assert.AreEqual("AddColumns", plan.RootNode.Operation.Split(':')[0], "Root should be AddColumns");
        }

        [TestMethod]
        public async Task Minimal3Node_BuildsTreeCorrectly()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Minimal_3Node.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert
            Assert.IsNotNull(tree);
            Assert.AreEqual("AddColumns", tree.OperatorName, "Root should be AddColumns");
            // After folding, some nodes may be collapsed
            Assert.IsTrue(allNodes.Count >= 1, "Should have at least root node visible");
        }

        #endregion

        #region Engine Transitions Fixture Tests

        [TestMethod]
        public async Task EngineTransitions_PreservesEngineTypes()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Engine_Transitions.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - Find SE nodes (Scan_Vertipaq)
            var seNodes = allNodes.Where(n => n.EngineType == EngineType.StorageEngine).ToList();
            Assert.IsTrue(seNodes.Count >= 1, "Should have at least 1 SE node preserved");

            // SE nodes should not be folded away
            var scanNodes = allNodes.Where(n => n.OperatorName?.Contains("Scan_Vertipaq") == true).ToList();
            Assert.IsTrue(scanNodes.Count >= 1, "Scan_Vertipaq should be preserved");
        }

        [TestMethod]
        public async Task EngineTransitions_FoldsFilterNotIsBlankChain()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Engine_Transitions.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Count Filter->Not->ISBLANK chain in source
            var filterRows = rows.Count(r => r.Operation?.StartsWith("Filter:") == true);
            var notRows = rows.Count(r => r.Operation?.StartsWith("Not:") == true);
            var isBlankRows = rows.Count(r => r.Operation?.StartsWith("ISBLANK:") == true);

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - Not and ISBLANK should be folded into Filter
            var filterNodes = allNodes.Where(n => n.OperatorName == "Filter").ToList();
            var notNodes = allNodes.Where(n => n.OperatorName == "Not").ToList();
            var isBlankNodes = allNodes.Where(n => n.OperatorName == "ISBLANK").ToList();

            // After folding, we should have fewer Not/ISBLANK nodes
            Assert.IsTrue(notNodes.Count < notRows || isBlankNodes.Count < isBlankRows,
                $"Filter/Not/ISBLANK chain should be folded. " +
                $"Source: Filter={filterRows}, Not={notRows}, ISBLANK={isBlankRows}. " +
                $"After folding: Filter={filterNodes.Count}, Not={notNodes.Count}, ISBLANK={isBlankNodes.Count}");
        }

        #endregion

        #region Arithmetic Chain Fixture Tests

        [TestMethod]
        public async Task ArithmeticChain_FoldsAddChain()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Arithmetic_Chain.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var addRowCount = rows.Count(r => r.Operation?.StartsWith("Add:") == true);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert
            var addNodes = allNodes.Where(n => n.OperatorName == "Add").ToList();

            // Should have fewer Add nodes after folding
            Assert.IsTrue(addNodes.Count < addRowCount,
                $"Add chain should be folded. Source: {addRowCount} Add rows, After folding: {addNodes.Count} Add nodes");

            // The surviving Add node should have chained count
            if (addNodes.Count == 1)
            {
                Assert.IsTrue(addNodes[0].ChainedOperatorCount > 1,
                    "Remaining Add node should show chained count");
            }
        }

        #endregion

        #region Spool Patterns Fixture Tests

        [TestMethod]
        public async Task SpoolPatterns_FoldsProjectionSpools()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Spool_Patterns.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var projSpoolRows = rows.Count(r => r.Operation?.StartsWith("ProjectionSpool") == true);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert
            var projSpoolNodes = allNodes.Where(n => n.OperatorName?.StartsWith("ProjectionSpool") == true).ToList();

            // ProjectionSpools should be folded into parent Spool_Iterator/SpoolLookup
            Assert.IsTrue(projSpoolNodes.Count < projSpoolRows,
                $"ProjectionSpools should be folded. Source: {projSpoolRows}, After folding: {projSpoolNodes.Count}");
        }

        [TestMethod]
        public async Task SpoolPatterns_FoldsAggregationSpools()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Spool_Patterns.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var aggSpoolRows = rows.Count(r => r.Operation?.StartsWith("AggregationSpool") == true);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert
            var aggSpoolNodes = allNodes.Where(n => n.OperatorName?.StartsWith("AggregationSpool") == true).ToList();

            // AggregationSpools should be folded into parent
            Assert.IsTrue(aggSpoolNodes.Count < aggSpoolRows,
                $"AggregationSpools should be folded. Source: {aggSpoolRows}, After folding: {aggSpoolNodes.Count}");
        }

        #endregion

        #region Large Plan Fixture Tests

        [TestMethod]
        public async Task LargePlan_ParsesAllNodes()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Large plan Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());

            // Assert
            Assert.IsNotNull(plan);
            Assert.AreEqual(rows.Count, plan.AllNodes.Count,
                $"Should parse all {rows.Count} rows into nodes");
        }

        [TestMethod]
        public async Task LargePlan_BuildsTreeWithFolding()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Large plan Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert
            Assert.IsNotNull(tree);
            Assert.IsTrue(allNodes.Count > 0, "Should have visible nodes");
            Assert.IsTrue(allNodes.Count < rows.Count,
                $"Folding should reduce node count. Source: {rows.Count}, Visible: {allNodes.Count}");

            // Log the reduction
            System.Diagnostics.Debug.WriteLine(
                $"Large plan: {rows.Count} source rows -> {allNodes.Count} visible nodes " +
                $"({100 - (allNodes.Count * 100 / rows.Count)}% reduction)");
        }

        [TestMethod]
        public async Task LargePlan_PreservesSENodes()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Large plan Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var scanRows = rows.Count(r => r.Operation?.Contains("Scan_Vertipaq") == true);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - SE scans should be preserved
            var scanNodes = allNodes.Where(n => n.OperatorName?.Contains("Scan_Vertipaq") == true).ToList();
            Assert.IsTrue(scanNodes.Count > 0,
                $"Should preserve Scan_Vertipaq nodes. Source had {scanRows} Scan rows.");
        }

        [TestMethod]
        public async Task LargePlan_CalculatesSubtreeWidths()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "Large plan Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);

            // Assert
            Assert.IsTrue(tree.SubtreeWidth >= 1, "Root should have SubtreeWidth >= 1");

            // Log the tree structure
            System.Diagnostics.Debug.WriteLine($"Root SubtreeWidth: {tree.SubtreeWidth}");
            System.Diagnostics.Debug.WriteLine($"Root CanToggleSubtree: {tree.CanToggleSubtree}");
        }

        #endregion
    }
}
