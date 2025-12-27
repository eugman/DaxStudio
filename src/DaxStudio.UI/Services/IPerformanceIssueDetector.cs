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

        /// <summary>
        /// Formula Engine ratio threshold (0-1). If FE time > this ratio of total, issue warning.
        /// Default is 0.5 (50%).
        /// </summary>
        public double HighFormulaEngineRatioThreshold { get; set; } = 0.5;

        /// <summary>
        /// Whether to detect high Formula Engine ratio issues.
        /// </summary>
        public bool DetectHighFormulaEngineRatio { get; set; } = true;

        /// <summary>
        /// Row count threshold for CrossApply warnings.
        /// </summary>
        public long ExcessiveCrossApplyThreshold { get; set; } = 50_000;

        /// <summary>
        /// Whether to detect excessive CrossApply issues.
        /// </summary>
        public bool DetectExcessiveCrossApply { get; set; } = true;

        /// <summary>
        /// Whether to detect xmSQL callbacks in SE queries.
        /// </summary>
        public bool DetectXmSqlCallbacks { get; set; } = true;

        /// <summary>
        /// Minimum SE queries to check cache ratio (avoid false positives on small queries).
        /// </summary>
        public int MinSeQueriesForCacheCheck { get; set; } = 3;

        /// <summary>
        /// Cache hit ratio threshold (0-1). Below this triggers warning.
        /// Default is 0.2 (20% cache hits expected).
        /// </summary>
        public double LowCacheHitRatioThreshold { get; set; } = 0.2;

        /// <summary>
        /// Whether to detect low cache hit ratio issues.
        /// </summary>
        public bool DetectLowCacheHitRatio { get; set; } = true;

        /// <summary>
        /// Data size threshold for excessive data size warning (in KB).
        /// Default is 100 MB (102,400 KB).
        /// </summary>
        public long ExcessiveDataSizeThreshold { get; set; } = 102_400;

        /// <summary>
        /// Data size threshold for excessive data size error (in KB).
        /// Default is 1 GB (1,048,576 KB).
        /// </summary>
        public long ExcessiveDataSizeErrorThreshold { get; set; } = 1_048_576;

        /// <summary>
        /// Whether to detect excessive data size issues in SE operations.
        /// </summary>
        public bool DetectExcessiveDataSize { get; set; } = true;
    }
}
