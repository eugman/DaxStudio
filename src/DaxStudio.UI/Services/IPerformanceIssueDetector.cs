using System.Collections.Generic;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for detecting performance anti-patterns in query plans.
    /// </summary>
    public interface IPerformanceIssueDetector
    {
        /// <summary>
        /// Analyzes a plan and returns all detected issues.
        /// </summary>
        /// <param name="plan">Enriched query plan to analyze</param>
        /// <returns>List of detected performance issues</returns>
        IReadOnlyList<PerformanceIssue> DetectIssues(EnrichedQueryPlan plan);

        /// <summary>
        /// Analyzes a single node for issues.
        /// </summary>
        /// <param name="node">Plan node to analyze</param>
        /// <returns>List of issues affecting this node</returns>
        IReadOnlyList<PerformanceIssue> DetectNodeIssues(EnrichedPlanNode node);

        /// <summary>
        /// Gets detection thresholds and settings.
        /// </summary>
        IssueDetectionSettings Settings { get; }
    }

    /// <summary>
    /// Configuration for issue detection thresholds.
    /// </summary>
    public class IssueDetectionSettings
    {
        /// <summary>
        /// Row count threshold for excessive materialization warning.
        /// </summary>
        public long ExcessiveMaterializationThreshold { get; set; } = 100_000;

        /// <summary>
        /// Row count threshold for excessive materialization error.
        /// </summary>
        public long ExcessiveMaterializationErrorThreshold { get; set; } = 1_000_000;

        /// <summary>
        /// Whether to flag CallbackDataID operations.
        /// </summary>
        public bool DetectCallbackDataId { get; set; } = true;
    }
}
