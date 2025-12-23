using System;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Represents a detected performance anti-pattern in a query plan.
    /// </summary>
    public class PerformanceIssue
    {
        /// <summary>
        /// Unique identifier for this issue instance.
        /// </summary>
        public Guid IssueId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The type of performance issue detected.
        /// </summary>
        public IssueType IssueType { get; set; }

        /// <summary>
        /// The severity level of this issue.
        /// </summary>
        public IssueSeverity Severity { get; set; }

        /// <summary>
        /// The NodeId of the plan node where this issue was detected.
        /// </summary>
        public int AffectedNodeId { get; set; }

        /// <summary>
        /// Human-readable description of the issue.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Suggested remediation or fix for this issue.
        /// </summary>
        public string Remediation { get; set; }

        /// <summary>
        /// Quantified metric value (e.g., row count for excessive materialization).
        /// </summary>
        public long? MetricValue { get; set; }

        /// <summary>
        /// The threshold that was exceeded to trigger this issue.
        /// </summary>
        public long? Threshold { get; set; }
    }
}
