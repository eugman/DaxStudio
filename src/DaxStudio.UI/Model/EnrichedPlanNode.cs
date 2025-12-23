using System.Collections.Generic;
using System.Linq;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Represents a single operator in an enriched query plan with metrics and layout data.
    /// </summary>
    public class EnrichedPlanNode
    {
        /// <summary>
        /// Unique identifier within the plan.
        /// </summary>
        public int NodeId { get; set; }

        /// <summary>
        /// Line number in original plan text.
        /// </summary>
        public int RowNumber { get; set; }

        /// <summary>
        /// Tree depth (0 = root).
        /// </summary>
        public int Level { get; set; }

        /// <summary>
        /// Raw operation text from the plan.
        /// </summary>
        public string Operation { get; set; }

        /// <summary>
        /// Operation text with column IDs resolved to human-readable names.
        /// </summary>
        public string ResolvedOperation { get; set; }

        /// <summary>
        /// Estimated or actual row count.
        /// </summary>
        public long? Records { get; set; }

        /// <summary>
        /// Operation duration in milliseconds (from trace correlation).
        /// </summary>
        public long? DurationMs { get; set; }

        /// <summary>
        /// CPU time in milliseconds (from trace correlation).
        /// </summary>
        public long? CpuTimeMs { get; set; }

        /// <summary>
        /// Percentage of total query cost attributed to this operation.
        /// </summary>
        public double? CostPercentage { get; set; }

        /// <summary>
        /// Which engine executes this operation.
        /// </summary>
        public EngineType EngineType { get; set; } = EngineType.Unknown;

        /// <summary>
        /// Whether this operation hit the VertiPaq cache.
        /// </summary>
        public bool IsCacheHit { get; set; }

        /// <summary>
        /// Referenced table or column name, if applicable.
        /// </summary>
        public string ObjectName { get; set; }

        /// <summary>
        /// Parent node reference.
        /// </summary>
        public EnrichedPlanNode Parent { get; set; }

        /// <summary>
        /// Child node references.
        /// </summary>
        public List<EnrichedPlanNode> Children { get; set; } = new List<EnrichedPlanNode>();

        /// <summary>
        /// Performance issues detected for this node.
        /// </summary>
        public List<PerformanceIssue> Issues { get; set; } = new List<PerformanceIssue>();

        /// <summary>
        /// Layout X position for rendering.
        /// </summary>
        public double X { get; set; }

        /// <summary>
        /// Layout Y position for rendering.
        /// </summary>
        public double Y { get; set; }

        /// <summary>
        /// Visual width of the node.
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Visual height of the node.
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Whether the subtree under this node is expanded.
        /// </summary>
        public bool IsExpanded { get; set; } = true;

        /// <summary>
        /// Whether this node is currently selected.
        /// </summary>
        public bool IsSelected { get; set; }

        /// <summary>
        /// Row number of the next sibling node for tree traversal.
        /// </summary>
        public int NextSiblingRowNumber { get; set; }

        /// <summary>
        /// Whether this node has any issues.
        /// </summary>
        public bool HasIssues => Issues?.Count > 0;

        /// <summary>
        /// Whether this node has any warning-level issues.
        /// </summary>
        public bool IsWarning => Issues?.Any(i => i.Severity == IssueSeverity.Warning) ?? false;

        /// <summary>
        /// Whether this node has any error-level issues.
        /// </summary>
        public bool IsError => Issues?.Any(i => i.Severity == IssueSeverity.Error) ?? false;

        /// <summary>
        /// Formatted duration string for display.
        /// </summary>
        public string DisplayDuration => DurationMs.HasValue ? $"{DurationMs:N0} ms" : string.Empty;

        /// <summary>
        /// Formatted record count for display.
        /// </summary>
        public string DisplayRecords => Records.HasValue ? $"{Records:N0}" : string.Empty;

        /// <summary>
        /// Short display name for the operator.
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(Operation)) return string.Empty;
                var colonIndex = Operation.IndexOf(':');
                return colonIndex > 0 ? Operation.Substring(0, colonIndex).Trim() : Operation;
            }
        }
    }
}
