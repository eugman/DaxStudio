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
                plan.RootNode = GetEffectiveRootNode(nodes);
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
            IEnumerable<TraceStorageEngineEvent> timingEvents,
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
                plan.RootNode = GetEffectiveRootNode(nodes);
                plan.State = PlanState.Parsed;

                // Resolve column names
                if (columnResolver?.IsInitialized == true)
                {
                    ResolveColumnNames(nodes, columnResolver);
                }

                // Correlate timing data (xmSQL, durations) from SE events
                CorrelateTimingData(plan, timingEvents?.ToList());

                // Assign engine types based on operator dictionary
                AssignEngineTypes(nodes);

                // Detect performance issues (same as physical plan)
                var issues = _issueDetector.DetectIssues(plan);
                foreach (var issue in issues)
                {
                    plan.Issues.Add(issue);
                    var affectedNode = plan.FindNodeById(issue.AffectedNodeId);
                    affectedNode?.Issues.Add(issue);
                }

                plan.State = PlanState.Enriched;
                Log.Debug("PlanEnrichmentService: Enriched logical plan with {NodeCount} nodes and {IssueCount} issues",
                    plan.NodeCount, plan.IssueCount);

                return plan;
            }).ConfigureAwait(false);
        }

        /// <summary>
        /// Gets the effective root node for the plan. When there are multiple Level 0 nodes,
        /// creates a synthetic "Query" root that contains all Level 0 nodes as children.
        /// For single root, returns that root directly.
        /// </summary>
        private EnrichedPlanNode GetEffectiveRootNode(List<EnrichedPlanNode> nodes)
        {
            if (nodes == null || nodes.Count == 0)
                return null;

            var rootNodes = nodes.Where(n => n.Level == 0).ToList();

            if (rootNodes.Count == 0)
                return null;

            if (rootNodes.Count == 1)
                return rootNodes[0];

            // Multiple root nodes - create a synthetic "Query" root to contain them all
            // This occurs with DEFINE VAR patterns where multiple subtrees exist
            Log.Debug("PlanEnrichmentService: Found {Count} root-level nodes, creating synthetic Query root", rootNodes.Count);

            // Find the minimum NodeId to create a unique ID for the synthetic root
            var minNodeId = nodes.Min(n => n.NodeId);
            var syntheticNodeId = minNodeId - 1; // Ensure unique ID below all other nodes

            var syntheticRoot = new EnrichedPlanNode
            {
                NodeId = syntheticNodeId,
                Level = -1, // Above Level 0
                Operation = "Query",
                ResolvedOperation = "Query"
            };

            // Add all root nodes as children of the synthetic root
            foreach (var rootNode in rootNodes)
            {
                rootNode.Parent = syntheticRoot;
                syntheticRoot.Children.Add(rootNode);
            }

            // Add synthetic root to the nodes list so it's included in BuildTree processing
            nodes.Insert(0, syntheticRoot);

            return syntheticRoot;
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

            // Collect ALL Storage Engine and DirectQuery nodes (not just one per table)
            // We'll match by column overlap, so multiple nodes for the same table is fine
            var allScanNodes = plan.AllNodes.Where(n => n.Operation != null &&
                (n.Operation.Contains("Scan_Vertipaq") ||
                 n.Operation.Contains("Sum_Vertipaq") ||
                 n.Operation.Contains("Count_Vertipaq") ||
                 n.Operation.Contains("Min_Vertipaq") ||
                 n.Operation.Contains("Max_Vertipaq") ||
                 n.Operation.Contains("Average_Vertipaq") ||
                 n.Operation.Contains("GroupBy_Vertipaq") ||
                 n.Operation.Contains("Filter_Vertipaq") ||
                 n.Operation.Contains("DistinctCount_Vertipaq") ||
                 n.Operation.Contains("_Vertipaq") ||
                 n.Operation.Contains("DirectQueryResult"))).ToList();

            // Pre-extract columns from each scan node's RequiredCols
            var scanNodeColumns = new Dictionary<EnrichedPlanNode, HashSet<string>>();
            foreach (var node in allScanNodes)
            {
                var columns = ExtractColumnsFromRequiredCols(node.Operation);
                scanNodeColumns[node] = columns;
                Log.Debug(">>> PlanEnrichmentService: Node {NodeId} has columns: [{Columns}]",
                    node.NodeId, string.Join(", ", columns));
            }

            Log.Information(">>> PlanEnrichmentService: Collected {Count} scan nodes for column-based matching", allScanNodes.Count);

            // Process ALL timing events
            var userEvents = timingEvents.Where(e => !e.IsInternalEvent).ToList();
            var internalEvents = timingEvents.Where(e => e.IsInternalEvent).ToList();

            Log.Information(">>> PlanEnrichmentService: {UserCount} user events, {InternalCount} internal events",
                userEvents.Count, internalEvents.Count);

            // Track which nodes have been matched to avoid double-assignment
            var matchedNodes = new HashSet<int>();

            // Match timing events to scan nodes by column overlap
            foreach (var evt in timingEvents)
            {
                if (evt.Duration.HasValue) totalSeDuration += evt.Duration.Value;
                if (evt.CpuTime.HasValue) totalSeCpu += evt.CpuTime.Value;

                bool isCacheHit = evt.Subclass == DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch;
                if (isCacheHit) cacheHits++;

                var xmSql = evt.Query ?? evt.TextData;
                var xmSqlColumns = ExtractColumnsFromXmSql(xmSql);
                var xmSqlTable = ExtractTableNameFromXmSql(xmSql);

                Log.Debug(">>> PlanEnrichmentService: Event xmSQL table='{Table}', columns=[{Columns}]",
                    xmSqlTable ?? "(null)", string.Join(", ", xmSqlColumns));

                // Find the best matching node by column overlap
                EnrichedPlanNode bestMatch = null;
                int bestOverlap = 0;

                foreach (var node in allScanNodes)
                {
                    // Skip if already matched
                    if (matchedNodes.Contains(node.NodeId))
                        continue;

                    var nodeColumns = scanNodeColumns[node];
                    var nodeTable = ExtractTableNameFromOperation(node.Operation);

                    // Calculate column overlap (ignoring table qualification and GUID suffixes)
                    int overlap = CalculateColumnOverlap(nodeColumns, xmSqlColumns);

                    // Bonus for matching table name
                    if (!string.IsNullOrEmpty(xmSqlTable) && !string.IsNullOrEmpty(nodeTable) &&
                        string.Equals(xmSqlTable, nodeTable, StringComparison.OrdinalIgnoreCase))
                    {
                        overlap += 10; // Table match bonus
                    }

                    if (overlap > bestOverlap)
                    {
                        bestOverlap = overlap;
                        bestMatch = node;
                    }
                }

                if (bestMatch != null && bestOverlap > 0)
                {
                    matchedNodes.Add(bestMatch.NodeId);
                    Log.Information(">>> PlanEnrichmentService: MATCHED event to node {NodeId} (overlap={Overlap}, table='{Table}', IsDirectQuery={IsDirectQuery})",
                        bestMatch.NodeId, bestOverlap, ExtractTableNameFromOperation(bestMatch.Operation), evt.IsDirectQuery);

                    // Populate xmSQL/SQL data
                    bestMatch.XmSql = evt.TextData ?? evt.Query;
                    bestMatch.ResolvedXmSql = evt.Query ?? evt.TextData;
                    bestMatch.DurationMs = evt.Duration;
                    bestMatch.CpuTimeMs = evt.CpuTime;
                    bestMatch.EstimatedRows = evt.EstimatedRows;
                    bestMatch.EstimatedKBytes = evt.EstimatedKBytes;
                    bestMatch.IsCacheHit = isCacheHit;
                    bestMatch.EngineType = evt.IsDirectQuery ? EngineType.DirectQuery : EngineType.StorageEngine;
                    bestMatch.ObjectName = xmSqlTable;

                    // RECONCILE ROW COUNTS
                    if ((!bestMatch.Records.HasValue || bestMatch.Records == 0) && evt.EstimatedRows.HasValue && evt.EstimatedRows > 0)
                    {
                        bestMatch.Records = evt.EstimatedRows;
                        bestMatch.RecordsSource = "ServerTiming";
                    }

                    // CALCULATE PARALLELISM
                    if (evt.Duration.HasValue && evt.NetParallelDuration.HasValue && evt.NetParallelDuration > 0)
                    {
                        bestMatch.NetParallelDurationMs = evt.NetParallelDuration;
                        bestMatch.Parallelism = (int)Math.Max(1, Math.Round((double)evt.Duration.Value / evt.NetParallelDuration.Value));
                    }
                }
                else
                {
                    Log.Debug(">>> PlanEnrichmentService: No node match for event, table='{Table}', columns=[{Columns}]",
                        xmSqlTable ?? "(null)", string.Join(", ", xmSqlColumns.Take(3)));
                }
            }

            plan.StorageEngineDurationMs = totalSeDuration;
            plan.StorageEngineCpuMs = totalSeCpu;
            plan.CacheHits = cacheHits;
            plan.StorageEngineQueryCount = userEvents.Count + internalEvents.Count;

            if (plan.TotalDurationMs > 0)
            {
                plan.FormulaEngineDurationMs = Math.Max(0, plan.TotalDurationMs - totalSeDuration);
            }

            Log.Information(">>> PlanEnrichmentService: Correlation complete. SE duration={SeDuration}ms, SE queries={SeQueryCount}, Cache hits={CacheHits}, Matched nodes={MatchedCount}",
                totalSeDuration, plan.StorageEngineQueryCount, cacheHits, matchedNodes.Count);
        }

        /// <summary>
        /// Extracts column names from RequiredCols pattern in operation string.
        /// E.g., "RequiredCols(0, 48, 56)('Product'[Brand], 'Sales'[RowNumber-GUID], 'Sales'[Quantity])"
        /// Returns: {"Brand", "RowNumber", "Quantity"}
        /// </summary>
        private HashSet<string> ExtractColumnsFromRequiredCols(string operation)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(operation)) return columns;

            // Match all 'Table'[Column] patterns
            var matches = Regex.Matches(operation, @"'[^']*'\[([^\]]+)\]");
            foreach (Match match in matches)
            {
                var colName = match.Groups[1].Value;
                // Strip GUID suffix from RowNumber columns (e.g., "RowNumber-2662979B-1795-...")
                var dashIdx = colName.IndexOf('-');
                if (dashIdx > 0 && colName.StartsWith("RowNumber", StringComparison.OrdinalIgnoreCase))
                {
                    colName = colName.Substring(0, dashIdx);
                }
                columns.Add(colName);
            }

            return columns;
        }

        /// <summary>
        /// Extracts column names from xmSQL SELECT clause.
        /// E.g., "SELECT 'Sales'[RowNumber], 'Sales'[Quantity] FROM 'Sales'"
        /// Returns: {"RowNumber", "Quantity"}
        /// </summary>
        private HashSet<string> ExtractColumnsFromXmSql(string xmSql)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(xmSql)) return columns;

            // Match all 'Table'[Column] patterns in SELECT (before FROM)
            var fromIdx = xmSql.IndexOf("FROM", StringComparison.OrdinalIgnoreCase);
            var selectPart = fromIdx > 0 ? xmSql.Substring(0, fromIdx) : xmSql;

            var matches = Regex.Matches(selectPart, @"'[^']*'\[([^\]]+)\]");
            foreach (Match match in matches)
            {
                columns.Add(match.Groups[1].Value);
            }

            return columns;
        }

        /// <summary>
        /// Calculates the overlap between two sets of column names.
        /// </summary>
        private int CalculateColumnOverlap(HashSet<string> nodeColumns, HashSet<string> xmSqlColumns)
        {
            int overlap = 0;
            foreach (var xmCol in xmSqlColumns)
            {
                // Check for exact match or prefix match (for RowNumber columns)
                if (nodeColumns.Contains(xmCol))
                {
                    overlap++;
                }
                else if (xmCol.StartsWith("RowNumber", StringComparison.OrdinalIgnoreCase))
                {
                    // RowNumber in xmSQL matches RowNumber in plan (after GUID stripping)
                    if (nodeColumns.Any(nc => nc.StartsWith("RowNumber", StringComparison.OrdinalIgnoreCase)))
                    {
                        overlap++;
                    }
                }
            }
            return overlap;
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

        /// <summary>
        /// Extracts the table name from an xmSQL query's FROM clause.
        /// Matches patterns like: FROM 'Date' or FROM 'Internet Sales'
        /// </summary>
        private string ExtractTableNameFromXmSql(string xmSql)
        {
            if (string.IsNullOrEmpty(xmSql)) return null;

            // Match FROM 'TableName' pattern (case-insensitive)
            var match = Regex.Match(xmSql, @"\bFROM\s+'([^']+)'", RegexOptions.IgnoreCase);
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
                // Skip if engine type is already set (e.g., from xmSQL correlation)
                if (node.EngineType != EngineType.Unknown)
                    continue;

                // Extract operator name from operation string
                var operatorName = ExtractOperatorName(node.Operation);
                if (string.IsNullOrEmpty(operatorName))
                    continue;

                // First, try to get engine type from dictionary (most accurate)
                var operatorInfo = DaxOperatorDictionary.GetOperatorInfo(operatorName);
                if (operatorInfo != null && operatorInfo.Engine != EngineType.Unknown)
                {
                    node.EngineType = operatorInfo.Engine;
                    continue;
                }

                // Fallback: pattern-based heuristic for operators not in dictionary
                var op = node.Operation?.ToUpperInvariant() ?? string.Empty;
                if (op.Contains("DIRECTQUERY"))
                {
                    node.EngineType = EngineType.DirectQuery;
                }
                else if (op.Contains("VERTIPAQ") || op.Contains("CACHE"))
                {
                    node.EngineType = EngineType.StorageEngine;
                }
                else
                {
                    // Default to Formula Engine when operator type is unknown
                    // (most query plan operations are FE-coordinated)
                    node.EngineType = EngineType.FormulaEngine;
                }
            }
        }

        /// <summary>
        /// Extracts the operator name from an operation string.
        /// Handles formats like "Operator: details", "'Table'[Col]: Operator details", etc.
        /// </summary>
        private string ExtractOperatorName(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                return null;

            // Check if this is a column reference format: 'Table'[Column]: Operator
            if (operation.StartsWith("'"))
            {
                // Find the colon that separates the column reference from the operator
                int bracketDepth = 0;
                bool inQuote = false;

                for (int i = 0; i < operation.Length; i++)
                {
                    char c = operation[i];
                    if (c == '\'' && bracketDepth == 0)
                        inQuote = !inQuote;
                    else if (!inQuote)
                    {
                        if (c == '[') bracketDepth++;
                        else if (c == ']') bracketDepth--;
                        else if (c == ':' && bracketDepth == 0)
                        {
                            // Extract operator name after the colon
                            var afterColon = operation.Substring(i + 1).TrimStart();
                            var spaceIndex = afterColon.IndexOf(' ');
                            return spaceIndex > 0 ? afterColon.Substring(0, spaceIndex) : afterColon;
                        }
                    }
                }
                return null; // Column reference with no operator
            }

            // Standard format: "Operator: Details" or "Operator Details"
            // Also handle variable prefix pattern: "__DS0Core: Union: details"
            var colonIdx = operation.IndexOf(':');
            if (colonIdx > 0)
            {
                var firstSpace = operation.IndexOf(' ');
                if (firstSpace > 0 && firstSpace < colonIdx)
                    return operation.Substring(0, firstSpace);

                var beforeColon = operation.Substring(0, colonIdx);

                // Check for variable name pattern: __VarName or _VarName
                // These are variable names, not operators - look for real operator after second colon
                if (beforeColon.StartsWith("__") || (beforeColon.StartsWith("_") && !beforeColon.StartsWith("_Vertipaq")))
                {
                    var afterFirstColon = operation.Substring(colonIdx + 1).TrimStart();
                    var secondColonIdx = afterFirstColon.IndexOf(':');
                    if (secondColonIdx > 0)
                    {
                        // Extract the actual operator (between first and second colon)
                        return afterFirstColon.Substring(0, secondColonIdx).Trim();
                    }
                }

                return beforeColon;
            }

            // No colon, use first space
            var idx = operation.IndexOf(' ');
            return idx > 0 ? operation.Substring(0, idx) : operation;
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
