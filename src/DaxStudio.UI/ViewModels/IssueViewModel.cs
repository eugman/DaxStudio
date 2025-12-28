using System;
using System.Windows.Media;
using Caliburn.Micro;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// ViewModel wrapper for PerformanceIssue providing UI-specific properties
    /// for display in the issues panel.
    /// </summary>
    public class IssueViewModel : PropertyChangedBase
    {
        private readonly PerformanceIssue _issue;
        private bool _isExpanded;
        private bool _isSelected;

        public IssueViewModel(PerformanceIssue issue)
        {
            _issue = issue ?? throw new ArgumentNullException(nameof(issue));
        }

        /// <summary>
        /// The underlying performance issue.
        /// </summary>
        public PerformanceIssue Issue => _issue;

        /// <summary>
        /// Unique identifier for the issue.
        /// </summary>
        public Guid IssueId => _issue.IssueId;

        /// <summary>
        /// The type of performance issue.
        /// </summary>
        public IssueType IssueType => _issue.IssueType;

        /// <summary>
        /// Display title based on issue type.
        /// </summary>
        public string Title => GetTitle();

        /// <summary>
        /// The severity level.
        /// </summary>
        public IssueSeverity Severity => _issue.Severity;

        /// <summary>
        /// Human-readable severity text.
        /// </summary>
        public string SeverityText => _issue.Severity.ToString();

        /// <summary>
        /// The affected node ID.
        /// </summary>
        public int AffectedNodeId => _issue.AffectedNodeId;

        /// <summary>
        /// Description of the issue.
        /// </summary>
        public string Description => _issue.Description;

        /// <summary>
        /// Suggested remediation.
        /// </summary>
        public string Remediation => _issue.Remediation ?? GetDefaultRemediation();

        /// <summary>
        /// Whether this issue has a remediation suggestion.
        /// </summary>
        public bool HasRemediation => !string.IsNullOrEmpty(Remediation);

        /// <summary>
        /// Metric value that triggered the issue.
        /// </summary>
        public long? MetricValue => _issue.MetricValue;

        /// <summary>
        /// Formatted metric value for display.
        /// </summary>
        public string MetricDisplay => _issue.MetricValue.HasValue
            ? $"{_issue.MetricValue.Value:N0}"
            : string.Empty;

        /// <summary>
        /// Threshold that was exceeded.
        /// </summary>
        public long? Threshold => _issue.Threshold;

        /// <summary>
        /// Formatted threshold for display.
        /// </summary>
        public string ThresholdDisplay => _issue.Threshold.HasValue
            ? $"{_issue.Threshold.Value:N0}"
            : string.Empty;

        /// <summary>
        /// Whether the metric exceeds threshold info should be shown.
        /// </summary>
        public bool HasMetricInfo => _issue.MetricValue.HasValue && _issue.Threshold.HasValue;

        /// <summary>
        /// Whether the issue details are expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded != value)
                {
                    _isExpanded = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ExpanderIcon));
                }
            }
        }

        /// <summary>
        /// Whether this issue is currently selected.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(BackgroundBrush));
                }
            }
        }

        /// <summary>
        /// Expander icon based on expanded state.
        /// </summary>
        public string ExpanderIcon => _isExpanded ? "\uE70D" : "\uE76C"; // ChevronDown : ChevronRight

        /// <summary>
        /// Background brush based on selection state.
        /// </summary>
        public Brush BackgroundBrush => _isSelected
            ? new SolidColorBrush(Color.FromRgb(220, 235, 252))
            : Brushes.Transparent;

        /// <summary>
        /// Severity indicator color.
        /// </summary>
        public Brush SeverityBrush => _issue.Severity switch
        {
            IssueSeverity.Error => new SolidColorBrush(Color.FromRgb(232, 17, 35)),    // Red
            IssueSeverity.Warning => new SolidColorBrush(Color.FromRgb(255, 185, 0)),  // Orange/Yellow
            IssueSeverity.Info => new SolidColorBrush(Color.FromRgb(0, 120, 212)),     // Blue
            _ => Brushes.Gray
        };

        /// <summary>
        /// Severity icon character (Segoe MDL2 Assets).
        /// </summary>
        public string SeverityIcon => _issue.Severity switch
        {
            IssueSeverity.Error => "\uEA39",    // ErrorBadge
            IssueSeverity.Warning => "\uE7BA",  // Warning
            IssueSeverity.Info => "\uE946",     // Info
            _ => "\uE9CE"                        // Unknown
        };

        /// <summary>
        /// Issue type icon resource key.
        /// </summary>
        public string IssueTypeIcon => _issue.IssueType switch
        {
            IssueType.ExcessiveMaterialization => "database_smallDrawingImage",
            IssueType.CallbackDataID => "functionDrawingImage",
            _ => "warningDrawingImage"
        };

        /// <summary>
        /// Toggle the expanded state.
        /// </summary>
        public void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }

        private string GetTitle()
        {
            return _issue.IssueType switch
            {
                IssueType.ExcessiveMaterialization => "Excessive Materialization",
                IssueType.CallbackDataID => "CallbackDataID Detected",
                _ => _issue.IssueType.ToString()
            };
        }

        private string GetDefaultRemediation()
        {
            return _issue.IssueType switch
            {
                IssueType.ExcessiveMaterialization =>
                    "Consider rewriting the DAX to reduce the number of rows being materialized. " +
                    "Use SUMMARIZE or SUMMARIZECOLUMNS instead of ADDCOLUMNS when possible. " +
                    "Avoid iterating over large tables with row-by-row calculations.",

                IssueType.CallbackDataID =>
                    "CallbackDataID indicates that the Formula Engine had to call back to the Storage Engine " +
                    "during row-by-row iteration. Consider restructuring the measure to avoid row context " +
                    "dependencies or use CALCULATE to change the context.",

                _ => null
            };
        }
    }
}
