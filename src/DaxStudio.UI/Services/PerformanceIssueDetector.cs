using System;
using System.Collections.Generic;
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

            foreach (var node in plan.AllNodes)
            {
                var nodeIssues = DetectNodeIssues(node);
                issues.AddRange(nodeIssues);
            }

            Log.Debug("PerformanceIssueDetector: Detected {IssueCount} issues in plan", issues.Count);
            return issues;
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
    }
}
