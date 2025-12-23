using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
            // Aggregate timing data from storage engine events
            long totalSeDuration = 0;
            long totalSeCpu = 0;
            int cacheHits = 0;

            foreach (var evt in timingEvents)
            {
                if (evt.Duration.HasValue)
                {
                    totalSeDuration += evt.Duration.Value;
                }
                if (evt.CpuTime.HasValue)
                {
                    totalSeCpu += evt.CpuTime.Value;
                }
                // Check for cache hits based on subclass
                if (evt.Subclass == DaxStudioTraceEventSubclass.VertiPaqCacheExactMatch)
                {
                    cacheHits++;
                }
            }

            plan.StorageEngineDurationMs = totalSeDuration;
            plan.StorageEngineCpuMs = totalSeCpu;
            plan.CacheHits = cacheHits;

            // FE time = Total - SE time (approximation)
            if (plan.TotalDurationMs > 0)
            {
                plan.FormulaEngineDurationMs = Math.Max(0, plan.TotalDurationMs - totalSeDuration);
            }
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
    }
}
