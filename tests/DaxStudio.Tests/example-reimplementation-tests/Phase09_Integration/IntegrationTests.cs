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

        #region DirectQuery Fixture Tests

        [TestMethod]
        public async Task DirectQuery_ParsesSuccessfully()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "DirectQuery Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());

            // Assert
            Assert.IsNotNull(plan);
            Assert.IsTrue(plan.AllNodes.Count > 0, "Should have nodes");
            Assert.IsNotNull(plan.RootNode, "Should have root node");
        }

        [TestMethod]
        public async Task DirectQuery_BuildsTreeCorrectly()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "DirectQuery Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert
            Assert.IsNotNull(tree);
            Assert.IsTrue(allNodes.Count >= 1, "Should have at least root node visible");
        }

        [TestMethod]
        public async Task DirectQuery_HasDirectQueryEngineType()
        {
            // Arrange
            var path = Path.Combine(FixturesPath, "DirectQuery Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Fixture not found: {path}");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            // Act
            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Assert - Should have at least one DirectQuery node
            var dqNodes = allNodes.Where(n => n.EngineType == EngineType.DirectQuery).ToList();
            Assert.IsTrue(dqNodes.Count >= 1, "Should have at least 1 DirectQuery node");
        }

        #endregion

        #region Engine Transitions Tests (Awaiting Fixture: Engine_Transitions)
        // These tests require a fixture with clear SE<->FE transitions
        // DAX example: FILTER(Sales, NOT ISBLANK([Region])) with mixed engines

        [TestMethod]
        public async Task EngineTransitions_PreservesEngineTypes()
        {
            // Awaiting fixture: Engine_Transitions Physical Query Plan.tsv
            // Should contain: Filter->Not->ISBLANK chain with Scan_Vertipaq
            var path = Path.Combine(FixturesPath, "Engine_Transitions Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Awaiting fixture: Engine_Transitions - needs query with SE/FE transitions");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // SE nodes should not be folded away
            var scanNodes = allNodes.Where(n => n.OperatorName?.Contains("Scan_Vertipaq") == true).ToList();
            Assert.IsTrue(scanNodes.Count >= 1, "Scan_Vertipaq should be preserved across engine boundary");
        }

        [TestMethod]
        public async Task EngineTransitions_FoldsFilterNotIsBlankChain()
        {
            // Awaiting fixture: ISBLANK_NotChain Physical Query Plan.tsv
            // DAX: FILTER(Table, NOT ISBLANK([Column]))
            var path = Path.Combine(FixturesPath, "ISBLANK_NotChain Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Awaiting fixture: ISBLANK_NotChain - needs FILTER with NOT ISBLANK pattern");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            var filterRows = rows.Count(r => r.Operation?.StartsWith("Filter:") == true);
            var notRows = rows.Count(r => r.Operation?.StartsWith("Not:") == true);
            var isBlankRows = rows.Count(r => r.Operation?.StartsWith("ISBLANK:") == true);

            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            var notNodes = allNodes.Where(n => n.OperatorName == "Not").ToList();
            var isBlankNodes = allNodes.Where(n => n.OperatorName == "ISBLANK").ToList();

            Assert.IsTrue(notNodes.Count < notRows || isBlankNodes.Count < isBlankRows,
                $"Filter/Not/ISBLANK chain should be folded. " +
                $"Source: Not={notRows}, ISBLANK={isBlankRows}. " +
                $"After folding: Not={notNodes.Count}, ISBLANK={isBlankNodes.Count}");
        }

        #endregion

        #region Arithmetic Chain Tests (Awaiting Fixture: Arithmetic_Chain)
        // DAX example: [A] + [B] + [C] + [D] - creates Add->Add->Add chain

        [TestMethod]
        public async Task ArithmeticChain_FoldsAddChain()
        {
            // Awaiting fixture: Arithmetic_Chain Physical Query Plan.tsv
            // DAX: Measure with multiple additions like [A] + [B] + [C] + [D]
            var path = Path.Combine(FixturesPath, "Arithmetic_Chain Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Awaiting fixture: Arithmetic_Chain - needs measure with [A]+[B]+[C]+[D] pattern");

            var rows = ParsePhysicalPlanTsv(path);
            var addRowCount = rows.Count(r => r.Operation?.StartsWith("Add:") == true);
            var enrichmentService = new PlanEnrichmentService();

            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            var addNodes = allNodes.Where(n => n.OperatorName == "Add").ToList();

            Assert.IsTrue(addNodes.Count < addRowCount,
                $"Add chain should be folded. Source: {addRowCount} Add rows, After folding: {addNodes.Count} Add nodes");

            if (addNodes.Count == 1)
            {
                Assert.IsTrue(addNodes[0].ChainedOperatorCount > 1,
                    "Remaining Add node should show chained count");
            }
        }

        #endregion

        #region Spool Patterns Tests (Awaiting Fixture: Spool_Patterns)
        // DAX example: SUMX(Sales, [Qty] * [Price]) - creates spool patterns

        [TestMethod]
        public async Task SpoolPatterns_FoldsProjectionSpools()
        {
            // Awaiting fixture: Spool_Patterns Physical Query Plan.tsv
            // DAX: SUMX or iterator that creates Spool_Iterator + ProjectionSpool
            var path = Path.Combine(FixturesPath, "Spool_Patterns Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Awaiting fixture: Spool_Patterns - needs SUMX or iterator query");

            var rows = ParsePhysicalPlanTsv(path);
            var projSpoolRows = rows.Count(r => r.Operation?.StartsWith("ProjectionSpool") == true);
            var enrichmentService = new PlanEnrichmentService();

            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            var projSpoolNodes = allNodes.Where(n => n.OperatorName?.StartsWith("ProjectionSpool") == true).ToList();

            Assert.IsTrue(projSpoolNodes.Count < projSpoolRows,
                $"ProjectionSpools should be folded. Source: {projSpoolRows}, After folding: {projSpoolNodes.Count}");
        }

        [TestMethod]
        public async Task SpoolPatterns_FoldsAggregationSpools()
        {
            // Awaiting fixture: Spool_Patterns Physical Query Plan.tsv
            var path = Path.Combine(FixturesPath, "Spool_Patterns Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Awaiting fixture: Spool_Patterns - needs SUMX or iterator query");

            var rows = ParsePhysicalPlanTsv(path);
            var aggSpoolRows = rows.Count(r => r.Operation?.StartsWith("AggregationSpool") == true);
            var enrichmentService = new PlanEnrichmentService();

            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            var aggSpoolNodes = allNodes.Where(n => n.OperatorName?.StartsWith("AggregationSpool") == true).ToList();

            Assert.IsTrue(aggSpoolNodes.Count < aggSpoolRows,
                $"AggregationSpools should be folded. Source: {aggSpoolRows}, After folding: {aggSpoolNodes.Count}");
        }

        #endregion

        #region Filter Comparison Tests (Awaiting Fixture: Filter_Comparison)
        // DAX example: CALCULATE([Sales], 'Product'[Price] > 100)

        [TestMethod]
        public async Task FilterComparison_ExtractsPredicate()
        {
            // Awaiting fixture: Filter_Comparison Physical Query Plan.tsv
            // DAX: CALCULATE([M], [Column] > 100) or FILTER with comparison
            var path = Path.Combine(FixturesPath, "Filter_Comparison Physical Query Plan.tsv");
            if (!File.Exists(path)) Assert.Inconclusive($"Awaiting fixture: Filter_Comparison - needs CALCULATE with comparison filter");

            var rows = ParsePhysicalPlanTsv(path);
            var enrichmentService = new PlanEnrichmentService();

            var plan = await enrichmentService.EnrichPhysicalPlanAsync(rows, null, null, Guid.NewGuid().ToString());
            var tree = PlanNodeViewModel.BuildTree(plan);
            var allNodes = GetAllNodes(tree).ToList();

            // Find Filter nodes and check they have predicate expressions
            var filterNodes = allNodes.Where(n => n.OperatorName == "Filter" || n.OperatorName == "Filter_Vertipaq").ToList();
            Assert.IsTrue(filterNodes.Count >= 1, "Should have at least one Filter node");

            // At least one filter should have a predicate expression
            var filtersWithPredicate = filterNodes.Where(n => !string.IsNullOrEmpty(n.FilterPredicateExpression)).ToList();
            Assert.IsTrue(filtersWithPredicate.Count >= 1,
                "At least one Filter should have extracted predicate expression");
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
