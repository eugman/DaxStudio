using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DaxStudio.UI.Model;
using Serilog;

namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Detects performance anti-patterns in query plans.
    /// </summary>
    public class PerformanceIssueDetector : IPerformanceIssueDetector
    {
        // Pattern to detect Spool operations with row counts
        private static readonly Regex SpoolPattern = new Regex(
            @"Spool(Lookup)?.*#Records=(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern to detect SpoolLookup with #Recs format
        private static readonly Regex SpoolRecsPattern = new Regex(
            @"Spool(Lookup)?.*#Recs=(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern to detect CallbackDataID operations
        private static readonly Regex CallbackDataIdPattern = new Regex(
            @"CallbackDataID",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern to detect CrossApply operations with row counts
        private static readonly Regex CrossApplyPattern = new Regex(
            @"CrossApply.*#Records=(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Pattern to detect records in operation string
        private static readonly Regex RecordsPattern = new Regex(
            @"#Records=(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Gets detection thresholds and settings.
        /// </summary>
        public IssueDetectionSettings Settings { get; } = new IssueDetectionSettings();

        /// <summary>
        /// Analyzes a plan and returns all detected issues.
        /// </summary>
        /// <param name="plan">Enriched query plan to analyze</param>
        /// <returns>List of detected performance issues</returns>
        public IReadOnlyList<PerformanceIssue> DetectIssues(EnrichedQueryPlan plan)
        {
            if (plan?.AllNodes == null)
                return Array.Empty<PerformanceIssue>();

            var issues = new List<PerformanceIssue>();

            // Plan-level checks
            DetectHighFormulaEngineRatio(plan, issues);
            DetectLowCacheHitRatio(plan, issues);

            // Node-level checks
            foreach (var node in plan.AllNodes)
            {
                var nodeIssues = DetectNodeIssues(node);
                issues.AddRange(nodeIssues);
            }

            // Dedupe ExcessiveMaterialization: keep only leaf-most node per row count in path
            issues = DedupeExcessiveMaterializationIssues(issues, plan);

            Log.Debug("PerformanceIssueDetector: Detected {IssueCount} issues in plan", issues.Count);
            return issues;
        }

        /// <summary>
        /// Removes duplicate ExcessiveMaterialization issues where an ancestor has the same row count.
        /// Only the leaf-most node per row count in a path is kept.
        /// </summary>
        private List<PerformanceIssue> DedupeExcessiveMaterializationIssues(List<PerformanceIssue> issues, EnrichedQueryPlan plan)
        {
            // Group materialization issues by row count
            var materializationIssues = issues
                .Where(i => i.IssueType == IssueType.ExcessiveMaterialization && i.MetricValue.HasValue)
                .ToList();

            if (materializationIssues.Count <= 1)
                return issues;

            // Build node lookup for quick access
            var nodeById = plan.AllNodes.ToDictionary(n => n.NodeId);

            // For each materialization issue, check if any descendant has the same row count
            var issuesToRemove = new HashSet<PerformanceIssue>();

            foreach (var issue in materializationIssues)
            {
                if (issuesToRemove.Contains(issue))
                    continue;

                var rowCount = issue.MetricValue.Value;

                // Find all other issues with same row count
                var sameRowCountIssues = materializationIssues
                    .Where(i => i != issue && i.MetricValue == rowCount)
                    .ToList();

                if (sameRowCountIssues.Count == 0)
                    continue;

                // Check if any of them is a descendant of this issue's node
                if (!nodeById.TryGetValue(issue.AffectedNodeId, out var thisNode))
                    continue;

                foreach (var otherIssue in sameRowCountIssues)
                {
                    if (!nodeById.TryGetValue(otherIssue.AffectedNodeId, out var otherNode))
                        continue;

                    // Check if otherNode is a descendant of thisNode
                    if (IsDescendant(otherNode, thisNode))
                    {
                        // otherNode is a descendant with same row count - remove this (ancestor) issue
                        issuesToRemove.Add(issue);
                        Log.Debug("DedupeExcessiveMaterialization: Removing issue for node {AncestorId} ({RowCount} rows) - descendant {DescendantId} has same count",
                            issue.AffectedNodeId, rowCount, otherIssue.AffectedNodeId);
                        break;
                    }
                }
            }

            if (issuesToRemove.Count > 0)
            {
                return issues.Where(i => !issuesToRemove.Contains(i)).ToList();
            }

            return issues;
        }

        /// <summary>
        /// Checks if candidateDescendant is a descendant of potentialAncestor.
        /// </summary>
        private bool IsDescendant(EnrichedPlanNode candidateDescendant, EnrichedPlanNode potentialAncestor)
        {
            var current = candidateDescendant.Parent;
            while (current != null)
            {
                if (current.NodeId == potentialAncestor.NodeId)
                    return true;
                current = current.Parent;
            }
            return false;
        }

        /// <summary>
        /// Analyzes a single node for issues.
        /// </summary>
        /// <param name="node">Plan node to analyze</param>
        /// <returns>List of issues affecting this node</returns>
        public IReadOnlyList<PerformanceIssue> DetectNodeIssues(EnrichedPlanNode node)
        {
            if (node == null || string.IsNullOrEmpty(node.Operation))
                return Array.Empty<PerformanceIssue>();

            var issues = new List<PerformanceIssue>();

            // Check for excessive materialization (Spool operations)
            DetectExcessiveMaterialization(node, issues);

            // Check for CallbackDataID
            if (Settings.DetectCallbackDataId)
            {
                DetectCallbackDataId(node, issues);
            }

            // Check for excessive CrossApply
            if (Settings.DetectExcessiveCrossApply)
            {
                DetectExcessiveCrossApply(node, issues);
            }

            // Check for xmSQL callbacks
            if (Settings.DetectXmSqlCallbacks && !string.IsNullOrEmpty(node.XmSql))
            {
                DetectXmSqlCallbacks(node, issues);
            }

            return issues;
        }

        private void DetectExcessiveMaterialization(EnrichedPlanNode node, List<PerformanceIssue> issues)
        {
            long? rowCount = null;

            // Try #Records= format first
            var spoolMatch = SpoolPattern.Match(node.Operation);
            if (spoolMatch.Success && long.TryParse(spoolMatch.Groups[2].Value, out var records))
            {
                rowCount = records;
            }
            else
            {
                // Try #Recs= format
                var recsMatch = SpoolRecsPattern.Match(node.Operation);
                if (recsMatch.Success && long.TryParse(recsMatch.Groups[2].Value, out var recs))
                {
                    rowCount = recs;
                }
            }

            if (!rowCount.HasValue)
                return;

            // Skip if this node will be folded into its parent (same spool operator type with same #Records)
            // This prevents duplicate issues for nested spool chains
            if (WillBeFoldedIntoParent(node, rowCount.Value))
                return;

            if (rowCount.Value >= Settings.ExcessiveMaterializationErrorThreshold)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.ExcessiveMaterialization,
                    Severity = IssueSeverity.Error,
                    AffectedNodeId = node.NodeId,
                    Description = $"Spool operation materialized {rowCount.Value:N0} rows",
                    Remediation = "Consider restructuring the query to avoid intermediate materialization. " +
                                  "Use SUMMARIZE instead of ADDCOLUMNS where possible, or add appropriate filters to reduce row counts.",
                    MetricValue = rowCount.Value,
                    Threshold = Settings.ExcessiveMaterializationErrorThreshold
                });
            }
            else if (rowCount.Value >= Settings.ExcessiveMaterializationThreshold)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.ExcessiveMaterialization,
                    Severity = IssueSeverity.Warning,
                    AffectedNodeId = node.NodeId,
                    Description = $"Spool operation materialized {rowCount.Value:N0} rows",
                    Remediation = "Consider restructuring the query to reduce intermediate materialization. " +
                                  "Review filter conditions and iterator patterns.",
                    MetricValue = rowCount.Value,
                    Threshold = Settings.ExcessiveMaterializationThreshold
                });
            }
        }

        private void DetectCallbackDataId(EnrichedPlanNode node, List<PerformanceIssue> issues)
        {
            if (CallbackDataIdPattern.IsMatch(node.Operation))
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.CallbackDataID,
                    Severity = IssueSeverity.Warning,
                    AffectedNodeId = node.NodeId,
                    Description = "CallbackDataID operation detected",
                    Remediation = "CallbackDataID indicates row-by-row processing between Storage Engine and Formula Engine. " +
                                  "This often occurs with complex calculated columns or measures that cannot be pushed to the storage engine. " +
                                  "Consider simplifying the calculation or pre-computing values."
                });
            }
        }

        private void DetectExcessiveCrossApply(EnrichedPlanNode node, List<PerformanceIssue> issues)
        {
            // Check if this is a CrossApply operation
            if (!node.Operation.StartsWith("CrossApply", StringComparison.OrdinalIgnoreCase))
                return;

            long? rowCount = null;

            // Get row count from node or operation string
            if (node.Records.HasValue)
            {
                rowCount = node.Records.Value;
            }
            else
            {
                var match = RecordsPattern.Match(node.Operation);
                if (match.Success && long.TryParse(match.Groups[1].Value, out var records))
                {
                    rowCount = records;
                }
            }

            if (!rowCount.HasValue)
                return;

            if (rowCount.Value >= Settings.ExcessiveCrossApplyThreshold)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.ExcessiveCrossApply,
                    Severity = IssueSeverity.Warning,
                    AffectedNodeId = node.NodeId,
                    Description = $"CrossApply operation processed {rowCount.Value:N0} rows",
                    Remediation = "CrossApply performs row-by-row processing (nested loop). " +
                                  "Consider restructuring the query to use set-based operations. " +
                                  "Review CALCULATE context transitions and iterator functions (SUMX, FILTER, etc.).",
                    MetricValue = rowCount.Value,
                    Threshold = Settings.ExcessiveCrossApplyThreshold
                });
            }
        }

        private void DetectXmSqlCallbacks(EnrichedPlanNode node, List<PerformanceIssue> issues)
        {
            var callbackType = XmSqlParser.DetectCallbackType(node.XmSql);

            if (callbackType == XmSqlCallbackType.CallbackDataID)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.XmSqlCallback,
                    Severity = IssueSeverity.Warning,
                    AffectedNodeId = node.NodeId,
                    Description = "xmSQL contains CallbackDataID - results NOT cached",
                    Remediation = XmSqlParser.GetCallbackDescription(callbackType)
                });
            }
            else if (callbackType != XmSqlCallbackType.None)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.XmSqlCallback,
                    Severity = IssueSeverity.Info,
                    AffectedNodeId = node.NodeId,
                    Description = $"xmSQL contains {callbackType} callback",
                    Remediation = XmSqlParser.GetCallbackDescription(callbackType)
                });
            }
        }

        private void DetectHighFormulaEngineRatio(EnrichedQueryPlan plan, List<PerformanceIssue> issues)
        {
            if (!Settings.DetectHighFormulaEngineRatio)
                return;

            // Need timing data
            if (plan.TotalDurationMs == 0)
                return;

            // Calculate FE time (Total - SE)
            var totalMs = plan.TotalDurationMs;
            var seMs = plan.StorageEngineDurationMs;
            var feMs = totalMs - seMs;

            if (feMs <= 0)
                return;

            var feRatio = (double)feMs / totalMs;

            if (feRatio >= Settings.HighFormulaEngineRatioThreshold)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.HighFormulaEngineRatio,
                    Severity = IssueSeverity.Warning,
                    AffectedNodeId = plan.RootNode?.NodeId ?? 0,
                    Description = $"Formula Engine used {feRatio:P0} of query time ({feMs:N0}ms of {totalMs:N0}ms)",
                    Remediation = "Formula Engine is single-threaded. High FE ratio indicates complex DAX calculations. " +
                                  "Consider simplifying measures, pre-calculating values in the model, or reducing iterator usage.",
                    MetricValue = feMs,
                    Threshold = (long)(totalMs * Settings.HighFormulaEngineRatioThreshold)
                });
            }
        }

        private void DetectLowCacheHitRatio(EnrichedQueryPlan plan, List<PerformanceIssue> issues)
        {
            if (!Settings.DetectLowCacheHitRatio)
                return;

            // Count SE queries and cache hits
            var seQueries = plan.StorageEngineQueryCount;
            var cacheHits = plan.CacheHits;

            // Need minimum number of queries to check
            if (seQueries < Settings.MinSeQueriesForCacheCheck)
                return;

            var cacheRatio = (double)cacheHits / seQueries;

            // Only flag if we have many queries with low cache hits
            if (cacheRatio < Settings.LowCacheHitRatioThreshold && seQueries > 5)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.LowCacheHitRatio,
                    Severity = IssueSeverity.Info,
                    AffectedNodeId = plan.RootNode?.NodeId ?? 0,
                    Description = $"Low cache hit ratio: {cacheHits} hits out of {seQueries} SE queries ({cacheRatio:P0})",
                    Remediation = "Low cache hit ratio may indicate the query pattern isn't benefiting from SE caching. " +
                                  "This can be normal for first-time queries or complex filter combinations.",
                    MetricValue = cacheHits,
                    Threshold = (long)(seQueries * Settings.LowCacheHitRatioThreshold)
                });
            }
        }

        /// <summary>
        /// Checks if a node will be folded into an ancestor during tree building.
        /// This is used to prevent duplicate issues for nodes in spool chains.
        /// Walks up the ancestor chain because intermediate nodes (AggregationSpool, etc.) get folded first.
        /// </summary>
        private bool WillBeFoldedIntoParent(EnrichedPlanNode node, long rowCount)
        {
            if (node.Parent == null)
                return false;

            // Check if this is a spool iterator
            var opName = GetNormalizedOperatorName(node.Operation);
            if (!IsSpoolIterator(opName))
                return false;

            // Walk up ancestors to find a spool iterator (intermediate nodes like AggregationSpool get folded)
            var ancestor = node.Parent;
            while (ancestor != null)
            {
                var ancestorOpName = GetNormalizedOperatorName(ancestor.Operation);

                // Check if we hit a spool iterator ancestor
                if (IsSpoolIterator(ancestorOpName))
                {
                    // Spool_Iterator chains are now folded regardless of row count
                    // Both same and different row counts get folded together (with row range display)
                    return true;
                }

                // Check if this is a spool type that will be folded (AggregationSpool, ProjectionSpool, etc.)
                // These intermediate nodes get folded, so continue up the chain
                if (ancestorOpName.Contains("Spool<") || ancestorOpName.EndsWith("Spool"))
                {
                    ancestor = ancestor.Parent;
                    continue;
                }

                // Hit a non-spool ancestor - not folded
                return false;
            }

            return false;
        }

        private static string GetNormalizedOperatorName(string operation)
        {
            if (string.IsNullOrEmpty(operation))
                return string.Empty;

            var colonIndex = operation.IndexOf(':');
            var opName = colonIndex > 0 ? operation.Substring(0, colonIndex).Trim() : operation;

            // Strip trailing #N suffix (e.g., "Spool_Iterator#1" -> "Spool_Iterator")
            var hashIndex = opName.LastIndexOf('#');
            if (hashIndex > 0)
            {
                var suffix = opName.Substring(hashIndex + 1);
                if (int.TryParse(suffix, out _))
                {
                    opName = opName.Substring(0, hashIndex);
                }
            }

            return opName;
        }

        private static bool IsSpoolIterator(string opName)
        {
            return opName == "Spool_Iterator" ||
                   opName.StartsWith("Spool_Iterator<", StringComparison.Ordinal) ||
                   opName == "SpoolLookup";
        }
    }
}
