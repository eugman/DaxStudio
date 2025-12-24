using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DaxStudio.Common.Enums;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;
using Serilog;

namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for enriching raw query plan data with timing metrics,
    /// resolved column names, and detected performance issues.
    /// </summary>
    public class PlanEnrichmentService : IPlanEnrichmentService
    {
        private readonly IPerformanceIssueDetector _issueDetector;

        public PlanEnrichmentService()
            : this(new PerformanceIssueDetector())
        {
        }

        public PlanEnrichmentService(IPerformanceIssueDetector issueDetector)
        {
            _issueDetector = issueDetector ?? throw new ArgumentNullException(nameof(issueDetector));
        }

        /// <summary>
        /// Enriches a physical query plan with timing and metadata.
        /// </summary>
        public async Task<EnrichedQueryPlan> EnrichPhysicalPlanAsync(
            IEnumerable<PhysicalQueryPlanRow> rawPlan,
            IEnumerable<TraceStorageEngineEvent> timingEvents,
            IColumnNameResolver columnResolver,
            string activityId)
        {
            // Run on background thread to keep UI responsive (FR-016)
            return await Task.Run(() =>
            {
                var plan = new EnrichedQueryPlan
                {
                    ActivityID = activityId,
                    PlanType = PlanType.Physical,
                    State = PlanState.Raw
                };

                var rows = rawPlan?.ToList();
                if (rows == null || rows.Count == 0)
                {
                    Log.Debug("PlanEnrichmentService: No rows in physical plan");
                    return plan;
                }

                // Step 1: Parse and build tree structure
                var nodes = BuildTreeFromRows(rows);
                plan.AllNodes = nodes;
                plan.RootNode = nodes.FirstOrDefault(n => n.Level == 0);
                plan.State = PlanState.Parsed;

                // Step 2: Resolve column names
                if (columnResolver?.IsInitialized == true)
                {
                    ResolveColumnNames(nodes, columnResolver);
                }

                // Step 3: Correlate timing data
                if (timingEvents != null)
                {
                    CorrelateTimingData(plan, timingEvents.ToList());
                }

                // Step 4: Calculate cost percentages
                CalculateCostPercentages(plan);

                // Step 5: Determine engine types
                AssignEngineTypes(nodes);

                // Step 6: Detect performance issues
                var issues = _issueDetector.DetectIssues(plan);
                foreach (var issue in issues)
                {
                    plan.Issues.Add(issue);
                    var affectedNode = plan.FindNodeById(issue.AffectedNodeId);
                    affectedNode?.Issues.Add(issue);
                }

                plan.State = PlanState.Enriched;
                Log.Debug("PlanEnrichmentService: Enriched physical plan with {NodeCount} nodes and {IssueCount} issues",
                    plan.NodeCount, plan.IssueCount);

                return plan;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Enriches a logical query plan with metadata.
        /// </summary>
        public async Task<EnrichedQueryPlan> EnrichLogicalPlanAsync(
            IEnumerable<LogicalQueryPlanRow> rawPlan,
            IColumnNameResolver columnResolver,
            string activityId)
        {
            return await Task.Run(() =>
            {
                var plan = new EnrichedQueryPlan
                {
                    ActivityID = activityId,
                    PlanType = PlanType.Logical,
                    State = PlanState.Raw
                };

                var rows = rawPlan?.ToList();
                if (rows == null || rows.Count == 0)
                {
                    Log.Debug("PlanEnrichmentService: No rows in logical plan");
                    return plan;
                }

                // Build tree from rows (logical plans have same structure)
                var nodes = BuildTreeFromQueryPlanRows(rows);
                plan.AllNodes = nodes;
                plan.RootNode = nodes.FirstOrDefault(n => n.Level == 0);
                plan.State = PlanState.Parsed;

                // Resolve column names
                if (columnResolver?.IsInitialized == true)
                {
                    ResolveColumnNames(nodes, columnResolver);
                }

                plan.State = PlanState.Enriched;
                Log.Debug("PlanEnrichmentService: Enriched logical plan with {NodeCount} nodes", plan.NodeCount);

                return plan;
            }).ConfigureAwait(false);
        }

        private List<EnrichedPlanNode> BuildTreeFromRows(List<PhysicalQueryPlanRow> rows)
        {
            var nodes = new List<EnrichedPlanNode>();
            var nodeStack = new Stack<EnrichedPlanNode>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var node = new EnrichedPlanNode
                {
                    NodeId = i + 1,
                    RowNumber = row.RowNumber,
                    Level = row.Level,
                    Operation = row.Operation,
                    ResolvedOperation = row.Operation,
                    Records = row.Records,
                    NextSiblingRowNumber = row.NextSiblingRowNumber
                };

                // Pop nodes from stack until we find parent level
                while (nodeStack.Count > 0 && nodeStack.Peek().Level >= node.Level)
                {
                    nodeStack.Pop();
                }

                // Assign parent
                if (nodeStack.Count > 0)
                {
                    node.Parent = nodeStack.Peek();
                    node.Parent.Children.Add(node);
                }

                nodeStack.Push(node);
                nodes.Add(node);
            }

            return nodes;
        }

        private List<EnrichedPlanNode> BuildTreeFromQueryPlanRows(List<LogicalQueryPlanRow> rows)
        {
            var nodes = new List<EnrichedPlanNode>();
            var nodeStack = new Stack<EnrichedPlanNode>();

            for (int i = 0; i < rows.Count; i++)
            {
                var row = rows[i];
                var node = new EnrichedPlanNode
                {
                    NodeId = i + 1,
                    RowNumber = row.RowNumber,
                    Level = row.Level,
                    Operation = row.Operation,
                    ResolvedOperation = row.Operation,
                    NextSiblingRowNumber = row.NextSiblingRowNumber
                };

                while (nodeStack.Count > 0 && nodeStack.Peek().Level >= node.Level)
                {
                    nodeStack.Pop();
                }

                if (nodeStack.Count > 0)
                {
                    node.Parent = nodeStack.Peek();
                    node.Parent.Children.Add(node);
                }

                nodeStack.Push(node);
                nodes.Add(node);
            }

            return nodes;
        }

        private void ResolveColumnNames(List<EnrichedPlanNode> nodes, IColumnNameResolver resolver)
        {
            foreach (var node in nodes)
            {
                node.ResolvedOperation = resolver.ResolveOperationString(node.Operation);
            }
        }

        private void CorrelateTimingData(EnrichedQueryPlan plan, List<TraceStorageEngineEvent> timingEvents)
        {
            // DEBUG: Entry point - REMOVE BEFORE RELEASE
            Log.Information(">>> PlanEnrichmentService.CorrelateTimingData() ENTRY - TimingEvents={Count}", timingEvents?.Count ?? 0);

            if (timingEvents == null || timingEvents.Count == 0)
            {
                Log.Information(">>> PlanEnrichmentService: No timing events to correlate - RETURNING EARLY");
                return;
            }

            // Aggregate timing data from storage engine events
            long totalSeDuration = 0;
            long totalSeCpu = 0;
            int cacheHits = 0;

            // Build lookup of Scan_Vertipaq nodes by table name for matching
            // Operations look like: "Scan_Vertipaq: ... ('Customer'[First Name]) ..."
            var scanNodesByTable = new Dictionary<string, EnrichedPlanNode>(StringComparer.OrdinalIgnoreCase);
            foreach (var node in plan.AllNodes.Where(n => n.Operation != null && n.Operation.Contains("Scan_Vertipaq")))
            {
                var tableName = ExtractTableNameFromOperation(node.Operation);
                if (!string.IsNullOrEmpty(tableName) && !scanNodesByTable.ContainsKey(tableName))
                {
                    scanNodesByTable[tableName] = node;
                    Log.Debug(">>> PlanEnrichmentService: Mapped table '{Table}' to node {NodeId}", tableName, node.NodeId);
                }
            }

            Log.Information(">>> PlanEnrichmentService: Built table lookup with {Count} entries: [{Tables}]",
                scanNodesByTable.Count, string.Join(", ", scanNodesByTable.Keys));

            // Process non-internal events first (they have EstimatedRows)
            var userEvents = timingEvents.Where(e => !e.IsInternalEvent).ToList();
            var internalEvents = timingEvents.Where(e => e.IsInternalEvent).ToList();

            Log.Information(">>> PlanEnrichmentService: {UserCount} user events, {InternalCount} internal events",
                userEvents.Count, internalEvents.Count);

            // Match user events to scan nodes by ObjectName
            foreach (var evt in userEvents)
            {
                if (evt.Duration.HasValue) totalSeDuration += evt.Duration.Value;
                if (evt.CpuTime.HasValue) totalSeCpu += evt.CpuTime.Value;

                bool isCacheHit = evt.Subclass == DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch;
                if (isCacheHit) cacheHits++;

                // Match by ObjectName to Scan_Vertipaq nodes
                if (!string.IsNullOrEmpty(evt.ObjectName) && scanNodesByTable.TryGetValue(evt.ObjectName, out var node))
                {
                    Log.Information(">>> PlanEnrichmentService: MATCHED event '{ObjectName}' to node {NodeId}", evt.ObjectName, node.NodeId);

                    // Populate xmSQL data
                    node.XmSql = evt.TextData ?? evt.Query;
                    node.ResolvedXmSql = evt.Query ?? evt.TextData;
                    node.DurationMs = evt.Duration;
                    node.CpuTimeMs = evt.CpuTime;
                    node.EstimatedRows = evt.EstimatedRows;
                    node.EstimatedKBytes = evt.EstimatedKBytes;
                    node.IsCacheHit = isCacheHit;
                    node.EngineType = EngineType.StorageEngine;
                    node.ObjectName = evt.ObjectName;

                    // RECONCILE ROW COUNTS: Use EstimatedRows from server timing when plan shows 0
                    if ((!node.Records.HasValue || node.Records == 0) && evt.EstimatedRows.HasValue && evt.EstimatedRows > 0)
                    {
                        node.Records = evt.EstimatedRows;
                        node.RecordsSource = "ServerTiming";
                        Log.Information(">>> PlanEnrichmentService: Reconciled node {NodeId} records from 0 to {NewValue} (from server timing)",
                            node.NodeId, evt.EstimatedRows);
                    }

                    // CALCULATE PARALLELISM: Duration / NetParallelDuration
                    if (evt.Duration.HasValue && evt.NetParallelDuration.HasValue && evt.NetParallelDuration > 0)
                    {
                        node.NetParallelDurationMs = evt.NetParallelDuration;
                        node.Parallelism = (int)Math.Max(1, Math.Round((double)evt.Duration.Value / evt.NetParallelDuration.Value));
                        Log.Information(">>> PlanEnrichmentService: Node {NodeId} parallelism = x{Parallelism}",
                            node.NodeId, node.Parallelism);
                    }
                }
                else
                {
                    Log.Debug(">>> PlanEnrichmentService: No node match for event ObjectName='{ObjectName}'", evt.ObjectName ?? "(null)");
                }
            }

            // Still aggregate timing from internal events (don't match to nodes)
            foreach (var evt in internalEvents)
            {
                if (evt.Duration.HasValue) totalSeDuration += evt.Duration.Value;
                if (evt.CpuTime.HasValue) totalSeCpu += evt.CpuTime.Value;
            }

            plan.StorageEngineDurationMs = totalSeDuration;
            plan.StorageEngineCpuMs = totalSeCpu;
            plan.CacheHits = cacheHits;

            // FE time = Total - SE time (approximation)
            if (plan.TotalDurationMs > 0)
            {
                plan.FormulaEngineDurationMs = Math.Max(0, plan.TotalDurationMs - totalSeDuration);
            }

            Log.Information(">>> PlanEnrichmentService: Correlation complete. SE duration={SeDuration}ms, Cache hits={CacheHits}",
                totalSeDuration, cacheHits);
        }

        /// <summary>
        /// Extracts the table name from an operation string.
        /// Examples:
        ///   "Scan_Vertipaq: ... ('Customer'[First Name]) ..." → "Customer"
        ///   "Spool_Iterator: ... IterCols(0)('Internet Sales'[Margin]) ..." → "Internet Sales"
        /// </summary>
        private string ExtractTableNameFromOperation(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return null;

            // Match 'TableName'[ColumnName] pattern
            var match = Regex.Match(operation, @"'([^']+)'\s*\[");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        private void CalculateCostPercentages(EnrichedQueryPlan plan)
        {
            // Calculate based on Records if timing not available
            var totalRecords = plan.AllNodes.Where(n => n.Records.HasValue).Sum(n => n.Records.Value);

            if (totalRecords > 0)
            {
                foreach (var node in plan.AllNodes.Where(n => n.Records.HasValue))
                {
                    node.CostPercentage = (double)node.Records.Value / totalRecords * 100;
                }
            }
        }

        private void AssignEngineTypes(List<EnrichedPlanNode> nodes)
        {
            foreach (var node in nodes)
            {
                // Determine engine type based on operation keywords
                var op = node.Operation?.ToUpperInvariant() ?? string.Empty;

                if (op.Contains("VERTIPAQ") || op.Contains("SCAN_VERTIPAQ") ||
                    op.Contains("CACHE") || op.Contains("DIRECTQUERY"))
                {
                    node.EngineType = EngineType.StorageEngine;
                }
                else if (op.Contains("ADDCOLUMNS") || op.Contains("SUMMARIZE") ||
                         op.Contains("CALCULATE") || op.Contains("FILTER") ||
                         op.Contains("CROSSJOIN") || op.Contains("CALLBACK"))
                {
                    node.EngineType = EngineType.FormulaEngine;
                }
            }
        }

        /// <summary>
        /// Cross-references logical plan nodes with physical plan nodes to infer
        /// engine types and row counts for logical nodes that have no direct metrics.
        /// </summary>
        public void CrossReferenceLogicalWithPhysical(EnrichedQueryPlan logicalPlan, EnrichedQueryPlan physicalPlan)
        {
            if (logicalPlan?.AllNodes == null || physicalPlan?.AllNodes == null)
            {
                Log.Debug("PlanEnrichmentService: Cannot cross-reference - one or both plans are null");
                return;
            }

            Log.Debug("PlanEnrichmentService: Cross-referencing {LogicalCount} logical nodes with {PhysicalCount} physical nodes",
                logicalPlan.AllNodes.Count, physicalPlan.AllNodes.Count);

            // Build lookup of physical nodes by operation pattern
            var physicalByPattern = new Dictionary<string, EnrichedPlanNode>();
            foreach (var physNode in physicalPlan.AllNodes.Where(n => n.EngineType != EngineType.Unknown))
            {
                var key = ExtractOperatorKey(physNode.Operation);
                if (!string.IsNullOrEmpty(key) && !physicalByPattern.ContainsKey(key))
                {
                    physicalByPattern[key] = physNode;
                }
            }

            int matchedNodes = 0;
            foreach (var logicalNode in logicalPlan.AllNodes)
            {
                var key = ExtractOperatorKey(logicalNode.Operation);
                if (!string.IsNullOrEmpty(key) && physicalByPattern.TryGetValue(key, out var physicalNode))
                {
                    // Inherit engine type from matching physical node
                    if (logicalNode.EngineType == EngineType.Unknown)
                    {
                        logicalNode.EngineType = physicalNode.EngineType;
                    }

                    // Inherit row counts if logical has none
                    if ((!logicalNode.Records.HasValue || logicalNode.Records == 0) &&
                        physicalNode.Records.HasValue && physicalNode.Records > 0)
                    {
                        logicalNode.Records = physicalNode.Records;
                        logicalNode.RecordsSource = "Physical";
                    }

                    // Inherit timing data if available
                    if (!logicalNode.DurationMs.HasValue && physicalNode.DurationMs.HasValue)
                    {
                        logicalNode.DurationMs = physicalNode.DurationMs;
                        logicalNode.CpuTimeMs = physicalNode.CpuTimeMs;
                        logicalNode.Parallelism = physicalNode.Parallelism;
                    }

                    // Inherit xmSQL if available
                    if (string.IsNullOrEmpty(logicalNode.XmSql) && !string.IsNullOrEmpty(physicalNode.XmSql))
                    {
                        logicalNode.XmSql = physicalNode.XmSql;
                        logicalNode.ResolvedXmSql = physicalNode.ResolvedXmSql;
                    }

                    matchedNodes++;
                }
            }

            Log.Debug("PlanEnrichmentService: Cross-reference matched {MatchedCount} logical nodes with physical nodes",
                matchedNodes);
        }

        /// <summary>
        /// Extracts a key pattern from an operation string for matching between logical and physical plans.
        /// </summary>
        private string ExtractOperatorKey(string operation)
        {
            if (string.IsNullOrEmpty(operation)) return string.Empty;

            // Extract key patterns: "Sum_Vertipaq", "Scan_Vertipaq", "GroupBy_Vertipaq", etc.
            var match = Regex.Match(operation, @"(\w+_Vertipaq|\w+LogOp|\w+PhyOp)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Value.ToUpperInvariant();
            }

            // Fall back to first word before colon or space
            var colonIndex = operation.IndexOf(':');
            if (colonIndex > 0)
            {
                return operation.Substring(0, colonIndex).Trim().ToUpperInvariant();
            }

            var spaceIndex = operation.IndexOf(' ');
            if (spaceIndex > 0)
            {
                return operation.Substring(0, spaceIndex).Trim().ToUpperInvariant();
            }

            return operation.Trim().ToUpperInvariant();
        }
    }
}
