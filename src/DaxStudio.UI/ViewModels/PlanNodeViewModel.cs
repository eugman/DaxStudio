using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Caliburn.Micro;
using DaxStudio.UI.Model;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// ViewModel wrapper for EnrichedPlanNode providing UI-specific properties
    /// for rendering in the visual query plan graph.
    /// </summary>
    public class PlanNodeViewModel : PropertyChangedBase
    {
        private readonly EnrichedPlanNode _node;
        private bool _isSelected;
        private bool _isExpanded = true;
        private Point _position;

        public PlanNodeViewModel(EnrichedPlanNode node)
        {
            _node = node ?? throw new ArgumentNullException(nameof(node));
            Children = new BindableCollection<PlanNodeViewModel>();
        }

        /// <summary>
        /// The underlying enriched plan node.
        /// </summary>
        public EnrichedPlanNode Node => _node;

        /// <summary>
        /// Unique identifier for the node.
        /// </summary>
        public int NodeId => _node.NodeId;

        /// <summary>
        /// The operation name (e.g., "CrossApply", "Scan_Vertipaq").
        /// </summary>
        public string Operation => _node.Operation;

        /// <summary>
        /// The resolved operation with column names replaced.
        /// </summary>
        public string ResolvedOperation => _node.ResolvedOperation;

        /// <summary>
        /// Display text for the node (truncated for graph view).
        /// Uses human-readable operator names. Shows collapsed comparison text when applicable.
        /// </summary>
        public string DisplayText
        {
            get
            {
                // If collapsed, show the collapsed comparison text
                if (IsCollapsed && CollapsedChildren.Count == 2)
                {
                    var collapsedText = CollapsedDisplayText;
                    return collapsedText.Length > 25 ? collapsedText.Substring(0, 22) + "..." : collapsedText;
                }

                var opName = GetOperationName();
                if (string.IsNullOrEmpty(opName))
                    return $"Node {NodeId}";

                var displayName = DaxOperatorDictionary.GetDisplayName(opName);
                return displayName.Length > 25 ? displayName.Substring(0, 22) + "..." : displayName;
            }
        }

        /// <summary>
        /// The raw operator name extracted from the operation string.
        /// </summary>
        public string OperatorName => GetOperationName();

        /// <summary>
        /// Human-readable display name for the operator.
        /// </summary>
        public string OperatorDisplayName => DaxOperatorDictionary.GetDisplayName(GetOperationName());

        /// <summary>
        /// Description of what this operator does.
        /// </summary>
        public string OperatorDescription => DaxOperatorDictionary.GetDescription(GetOperationName());

        /// <summary>
        /// Category of the operator (e.g., Iterator, Spool, Storage Engine).
        /// </summary>
        public string OperatorCategory => DaxOperatorDictionary.GetCategory(GetOperationName());

        /// <summary>
        /// Short engine type badge for node display (SE, FE, or empty).
        /// </summary>
        public string EngineBadge => _node.EngineType switch
        {
            EngineType.StorageEngine => "SE",
            EngineType.FormulaEngine => "FE",
            _ => ""
        };

        /// <summary>
        /// Whether to show engine badge.
        /// </summary>
        public bool HasEngineBadge => _node.EngineType != EngineType.Unknown;

        /// <summary>
        /// Short category abbreviation for node display.
        /// </summary>
        public string CategoryBadge
        {
            get
            {
                var category = OperatorCategory;
                return category switch
                {
                    "Iterator" => "Iter",
                    "Spool" => "Spool",
                    "Storage Engine" => "SE",
                    "Aggregate" => "Agg",
                    "Join" => "Join",
                    "Filter" => "Filter",
                    "Sort" => "Sort",
                    "Projection" => "Proj",
                    "Table" => "Table",
                    _ => category.Length > 6 ? category.Substring(0, 5) : category
                };
            }
        }

        /// <summary>
        /// Number of key columns (parsed from #KeyCols=N in operation string).
        /// </summary>
        public int? KeyColsCount => ParseIntFromOperation("#KeyCols=");

        /// <summary>
        /// Number of value columns (parsed from #ValueCols=N in operation string).
        /// </summary>
        public int? ValueColsCount => ParseIntFromOperation("#ValueCols=");

        /// <summary>
        /// Dominant value if specified in operation (parsed from DominantValue=X).
        /// </summary>
        public string DominantValue => ParseStringFromOperation("DominantValue=");

        /// <summary>
        /// A summary of key metrics for compact node display.
        /// Shows key/value cols if available, or record count, or category.
        /// </summary>
        public string MetricsSummary
        {
            get
            {
                var parts = new List<string>();

                // Show key/value cols if available
                if (KeyColsCount.HasValue)
                {
                    parts.Add($"{KeyColsCount.Value} keys");
                }
                if (ValueColsCount.HasValue)
                {
                    parts.Add($"{ValueColsCount.Value} val");
                }

                if (parts.Count > 0)
                {
                    return string.Join(", ", parts);
                }

                // Show records if available and > 0
                if (_node.Records.HasValue && _node.Records.Value > 0)
                {
                    return FormatRecordCount(_node.Records.Value);
                }

                // Show duration if available
                if (_node.DurationMs.HasValue)
                {
                    return $"{_node.DurationMs.Value:N0} ms";
                }

                return string.Empty;
            }
        }

        /// <summary>
        /// Whether metrics summary has content to display.
        /// </summary>
        public bool HasMetricsSummary => !string.IsNullOrEmpty(MetricsSummary);

        /// <summary>
        /// Formats a record count in a compact human-readable form.
        /// </summary>
        private string FormatRecordCount(long records)
        {
            if (records >= 1_000_000)
            {
                return $"{records / 1_000_000.0:F1}M rows";
            }
            if (records >= 1_000)
            {
                return $"{records / 1_000.0:F1}K rows";
            }
            return $"{records:N0} rows";
        }

        /// <summary>
        /// Parses an integer value from the operation string for a given key.
        /// </summary>
        private int? ParseIntFromOperation(string key)
        {
            var op = _node.Operation ?? string.Empty;
            var idx = op.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var start = idx + key.Length;
            var end = start;
            while (end < op.Length && char.IsDigit(op[end]))
            {
                end++;
            }

            if (end > start && int.TryParse(op.Substring(start, end - start), out var result))
            {
                return result;
            }
            return null;
        }

        /// <summary>
        /// Parses a string value from the operation string for a given key.
        /// </summary>
        private string ParseStringFromOperation(string key)
        {
            var op = _node.Operation ?? string.Empty;
            var idx = op.IndexOf(key, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;

            var start = idx + key.Length;
            var end = start;
            while (end < op.Length && !char.IsWhiteSpace(op[end]))
            {
                end++;
            }

            if (end > start)
            {
                return op.Substring(start, end - start);
            }
            return null;
        }

        /// <summary>
        /// Full tooltip text showing complete operation details.
        /// </summary>
        public string TooltipText
        {
            get
            {
                var opName = GetOperationName();
                var displayName = DaxOperatorDictionary.GetDisplayName(opName);
                var description = DaxOperatorDictionary.GetDescription(opName);
                var category = DaxOperatorDictionary.GetCategory(opName);

                var lines = new List<string>
                {
                    displayName,
                    $"({category})",
                    "",
                    description
                };

                lines.Add("");
                lines.Add($"Row: {_node.RowNumber} | Level: {_node.Level}");

                if (_node.Records.HasValue)
                {
                    lines.Add($"Records: {_node.Records.Value:N0}");
                }

                if (_node.CostPercentage.HasValue)
                {
                    lines.Add($"Cost: {_node.CostPercentage.Value:F1}%");
                }

                if (_node.DurationMs.HasValue)
                {
                    lines.Add($"Duration: {_node.DurationMs.Value:N0} ms");
                }

                if (_node.EngineType != EngineType.Unknown)
                {
                    lines.Add($"Engine: {_node.EngineType}");
                }

                if (_node.Issues.Count > 0)
                {
                    lines.Add("");
                    lines.Add("Issues:");
                    foreach (var issue in _node.Issues)
                    {
                        lines.Add($"  - {issue.IssueType}: {issue.Description}");
                    }
                }

                return string.Join(Environment.NewLine, lines);
            }
        }

        /// <summary>
        /// Number of records processed by this operation.
        /// </summary>
        public long? Records => _node.Records;

        /// <summary>
        /// Formatted records string for display.
        /// </summary>
        public string RecordsDisplay => _node.Records.HasValue
            ? $"{_node.Records.Value:N0}"
            : string.Empty;

        /// <summary>
        /// Whether records info is available.
        /// </summary>
        public bool HasRecords => _node.Records.HasValue;

        /// <summary>
        /// Source of the records value (Plan, ServerTiming, or Physical).
        /// </summary>
        public string RecordsSource => _node.RecordsSource ?? "Plan";

        /// <summary>
        /// Formatted records display with source annotation.
        /// </summary>
        public string RecordsWithSourceDisplay
        {
            get
            {
                if (!_node.Records.HasValue)
                    return "No row count";

                var source = _node.RecordsSource ?? "Plan";
                if (source == "Plan")
                    return $"{_node.Records.Value:N0}";
                return $"{_node.Records.Value:N0} (from {source})";
            }
        }

        /// <summary>
        /// Parallelism factor (number of threads) for SE operations.
        /// </summary>
        public int? Parallelism => _node.Parallelism;

        /// <summary>
        /// Whether parallelism data is available.
        /// </summary>
        public bool HasParallelism => _node.Parallelism.HasValue && _node.Parallelism.Value > 1;

        /// <summary>
        /// Formatted parallelism display (e.g., "x16 parallelism").
        /// </summary>
        public string ParallelismDisplay => _node.Parallelism.HasValue
            ? $"x{_node.Parallelism.Value} parallelism"
            : string.Empty;

        /// <summary>
        /// Net parallel duration in milliseconds.
        /// </summary>
        public long? NetParallelDurationMs => _node.NetParallelDurationMs;

        /// <summary>
        /// Formatted net parallel duration display.
        /// </summary>
        public string NetParallelDurationDisplay => _node.NetParallelDurationMs.HasValue
            ? $"{_node.NetParallelDurationMs.Value:N0} ms (parallel)"
            : string.Empty;

        /// <summary>
        /// Whether net parallel duration is available.
        /// </summary>
        public bool HasNetParallelDuration => _node.NetParallelDurationMs.HasValue;

        /// <summary>
        /// CPU time in milliseconds.
        /// </summary>
        public long? CpuTimeMs => _node.CpuTimeMs;

        /// <summary>
        /// Formatted CPU time display.
        /// </summary>
        public string CpuTimeDisplay => _node.CpuTimeMs.HasValue
            ? $"{_node.CpuTimeMs.Value:N0} ms"
            : string.Empty;

        /// <summary>
        /// Whether CPU time is available.
        /// </summary>
        public bool HasCpuTime => _node.CpuTimeMs.HasValue;

        /// <summary>
        /// Cost percentage relative to total query cost.
        /// </summary>
        public double? CostPercentage => _node.CostPercentage;

        /// <summary>
        /// Formatted cost percentage for display.
        /// </summary>
        public string CostDisplay => _node.CostPercentage.HasValue
            ? $"{_node.CostPercentage.Value:F1}%"
            : string.Empty;

        /// <summary>
        /// Whether cost info is available.
        /// </summary>
        public bool HasCost => _node.CostPercentage.HasValue;

        /// <summary>
        /// Duration in milliseconds.
        /// </summary>
        public long? DurationMs => _node.DurationMs;

        /// <summary>
        /// Formatted duration for display.
        /// </summary>
        public string DurationDisplay => _node.DurationMs.HasValue
            ? $"{_node.DurationMs.Value:N0} ms"
            : string.Empty;

        /// <summary>
        /// Whether duration info is available.
        /// </summary>
        public bool HasDuration => _node.DurationMs.HasValue;

        /// <summary>
        /// Row number in the query plan.
        /// </summary>
        public int RowNumber => _node.RowNumber;

        /// <summary>
        /// Indentation level.
        /// </summary>
        public int Level => _node.Level;

        /// <summary>
        /// Full operation text for details view.
        /// </summary>
        public string FullOperation => _node.ResolvedOperation ?? _node.Operation ?? string.Empty;

        /// <summary>
        /// The xmSQL query sent to the storage engine.
        /// </summary>
        public string XmSql => _node.XmSql;

        /// <summary>
        /// The resolved xmSQL with column IDs replaced by names.
        /// </summary>
        public string ResolvedXmSql => _node.ResolvedXmSql ?? _node.XmSql;

        /// <summary>
        /// Whether xmSQL is available for this node.
        /// </summary>
        public bool HasXmSql => !string.IsNullOrEmpty(_node.XmSql);

        #region Edge Thickness and Row Count Coloring (SSMS-style)

        /// <summary>
        /// Logarithmic edge thickness based on row count (like SSMS query plans).
        /// Range: 1px (0-10 rows) to 10px (100K+ rows)
        /// </summary>
        public double EdgeThickness
        {
            get
            {
                if (!_node.Records.HasValue || _node.Records.Value <= 0)
                    return 1.0;

                // Logarithmic scaling: thickness = log10(rows + 1) * 2, clamped to 1-10
                var rows = _node.Records.Value;
                return Math.Min(10, Math.Max(1, Math.Log10(rows + 1) * 2));
            }
        }

        /// <summary>
        /// Severity level based on row count thresholds.
        /// </summary>
        public string RowCountSeverity
        {
            get
            {
                if (!_node.Records.HasValue || _node.Records.Value == 0)
                    return "None";

                var records = _node.Records.Value;
                if (records < 10_000)
                    return "Fine";      // Green - acceptable
                if (records < 100_000)
                    return "Warning";   // Yellow - concerning
                return "Critical";      // Red - excessive materialization
            }
        }

        /// <summary>
        /// Color for row count display based on severity thresholds.
        /// Only highlight warnings/critical - normal counts use standard text color.
        /// </summary>
        public Brush RowCountColor
        {
            get
            {
                return RowCountSeverity switch
                {
                    "Warning" => new SolidColorBrush(Color.FromRgb(200, 120, 0)),  // Muted orange
                    "Critical" => new SolidColorBrush(Color.FromRgb(180, 40, 40)), // Muted red
                    _ => new SolidColorBrush(Color.FromRgb(80, 80, 80))             // Dark gray (default)
                };
            }
        }

        /// <summary>
        /// Edge/arrow color - neutral gray for most, only highlight warnings.
        /// </summary>
        public Brush EdgeColor
        {
            get
            {
                return RowCountSeverity switch
                {
                    "Warning" => new SolidColorBrush(Color.FromRgb(200, 140, 60)),  // Muted orange
                    "Critical" => new SolidColorBrush(Color.FromRgb(180, 80, 80)),  // Muted red
                    _ => new SolidColorBrush(Color.FromRgb(140, 140, 140))           // Neutral gray
                };
            }
        }

        /// <summary>
        /// Formatted row count with commas for edge tooltip display.
        /// </summary>
        public string RowsDisplayFormatted
        {
            get
            {
                if (!_node.Records.HasValue)
                    return "No row count";
                return $"{_node.Records.Value:N0} rows";
            }
        }

        /// <summary>
        /// Data size display for edge tooltip (from EstimatedKBytes).
        /// </summary>
        public string DataSizeDisplay
        {
            get
            {
                if (!_node.EstimatedKBytes.HasValue)
                    return string.Empty;

                var kb = _node.EstimatedKBytes.Value;
                if (kb >= 1024 * 1024)
                    return $"{kb / (1024.0 * 1024.0):F1} GB";
                if (kb >= 1024)
                    return $"{kb / 1024.0:F1} MB";
                return $"{kb:N0} KB";
            }
        }

        /// <summary>
        /// Whether data size is available.
        /// </summary>
        public bool HasDataSize => _node.EstimatedKBytes.HasValue;

        #endregion

        #region CallbackDataID Detection

        /// <summary>
        /// The CallbackDataID value if present (parsed from operation string).
        /// CallbackDataID indicates SE calling back to FE - a performance concern.
        /// </summary>
        public int? CallbackDataId => ParseIntFromOperation("CallbackDataID=");

        /// <summary>
        /// Whether this operation has a CallbackDataID (SE calling FE).
        /// </summary>
        public bool HasCallback => CallbackDataId.HasValue;

        /// <summary>
        /// Whether this node represents a performance concern.
        /// True if: has callback, or excessive row count (1M+), or has issues.
        /// </summary>
        public bool IsPerformanceConcern
        {
            get
            {
                if (HasCallback)
                    return true;
                if (_node.Records.HasValue && _node.Records.Value >= 1_000_000)
                    return true;
                if (HasIssues)
                    return true;
                return false;
            }
        }

        /// <summary>
        /// Warning text for CallbackDataID (shown in details panel).
        /// </summary>
        public string CallbackWarningText => HasCallback
            ? "Storage Engine is calling back to Formula Engine during table scan. This operation runs in parallel but results are NOT CACHED, which can significantly impact performance."
            : string.Empty;

        #endregion

        #region DAX Measure and Table/Column References

        private string _measureFormula;

        /// <summary>
        /// DAX measure reference parsed from LogOp pattern (e.g., "LogOp=Sum_Vertipaq Currency").
        /// </summary>
        public string MeasureReference
        {
            get
            {
                var op = _node.Operation ?? string.Empty;

                // Look for MeasureRef=[MeasureName] or MeasureRef='MeasureName' pattern
                var match = Regex.Match(op, @"MeasureRef=['\[]([^'\]]+)['\]]", RegexOptions.IgnoreCase);
                if (match.Success)
                    return $"[{match.Groups[1].Value}]";

                // Look for measure references in format (''[MeasureName]) - common in physical plans
                match = Regex.Match(op, @"\(''\[([^\]]+)\]\)", RegexOptions.IgnoreCase);
                if (match.Success)
                    return $"[{match.Groups[1].Value}]";

                // Look for aggregation operators (Sum, Count, Min, Max, etc.) with measure names
                // Excludes Scan_Vertipaq which doesn't have measure names
                match = Regex.Match(op, @"LogOp=(Sum|Count|Min|Max|Average|Avg)_Vertipaq\s+([A-Za-z]\w*)\b", RegexOptions.IgnoreCase);
                if (match.Success)
                    return $"[{match.Groups[2].Value}]";

                return string.Empty;
            }
        }

        /// <summary>
        /// Whether a DAX measure reference is available.
        /// </summary>
        public bool HasMeasureReference => !string.IsNullOrEmpty(MeasureReference);

        /// <summary>
        /// The DAX formula for the referenced measure (resolved from model metadata).
        /// </summary>
        public string MeasureFormula
        {
            get => _measureFormula;
            set
            {
                if (_measureFormula != value)
                {
                    _measureFormula = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(HasMeasureFormula));
                }
            }
        }

        /// <summary>
        /// Whether a DAX measure formula is available.
        /// </summary>
        public bool HasMeasureFormula => !string.IsNullOrEmpty(_measureFormula);

        /// <summary>
        /// Table name referenced in the operation (parsed from 'TableName' patterns).
        /// </summary>
        public string TableReference
        {
            get
            {
                var op = _node.Operation ?? string.Empty;

                // Look for 'TableName' pattern (single quotes)
                var match = Regex.Match(op, @"'([^']+)'");
                if (match.Success)
                    return match.Groups[1].Value;

                return string.Empty;
            }
        }

        /// <summary>
        /// Whether a table reference is available.
        /// </summary>
        public bool HasTableReference => !string.IsNullOrEmpty(TableReference);

        /// <summary>
        /// Column references parsed from the operation string.
        /// </summary>
        public string ColumnReferences
        {
            get
            {
                var op = _node.Operation ?? string.Empty;

                // Look for column references in format 'Table'[Column] or just [Column]
                var matches = Regex.Matches(op, @"(?:'[^']+')?\[([^\]]+)\]");
                if (matches.Count == 0)
                    return string.Empty;

                var columns = matches.Cast<Match>()
                    .Select(m => m.Groups[1].Value)
                    .Distinct()
                    .Take(5); // Limit to 5 columns for display

                return string.Join(", ", columns);
            }
        }

        /// <summary>
        /// Whether column references are available.
        /// </summary>
        public bool HasColumnReferences => !string.IsNullOrEmpty(ColumnReferences);

        #endregion

        #region Engine Details

        /// <summary>
        /// Full engine description with threading information.
        /// </summary>
        public string EngineDescription
        {
            get
            {
                return _node.EngineType switch
                {
                    EngineType.StorageEngine => "Storage Engine (SE)\nMulti-threaded, highly optimized for simple operations",
                    EngineType.FormulaEngine => "Formula Engine (FE)\nSingle-threaded, handles complex DAX expressions",
                    _ => "Unknown Engine"
                };
            }
        }

        /// <summary>
        /// Engine badge background color.
        /// </summary>
        public Brush EngineBadgeBackground
        {
            get
            {
                return _node.EngineType switch
                {
                    EngineType.StorageEngine => new SolidColorBrush(Color.FromRgb(76, 175, 80)),   // Green
                    EngineType.FormulaEngine => new SolidColorBrush(Color.FromRgb(156, 39, 176)), // Purple
                    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))                         // Gray
                };
            }
        }

        /// <summary>
        /// Engine badge foreground (text) color.
        /// </summary>
        public Brush EngineBadgeForeground => Brushes.White;

        #endregion

        /// <summary>
        /// Estimated rows from SE query.
        /// </summary>
        public long? EstimatedRows => _node.EstimatedRows;

        /// <summary>
        /// Formatted estimated rows for display.
        /// </summary>
        public string EstimatedRowsDisplay => _node.EstimatedRows.HasValue
            ? $"{_node.EstimatedRows.Value:N0}"
            : string.Empty;

        /// <summary>
        /// Whether estimated rows info is available.
        /// </summary>
        public bool HasEstimatedRows => _node.EstimatedRows.HasValue;

        /// <summary>
        /// Whether this node is a cache hit.
        /// </summary>
        public bool IsCacheHit => _node.IsCacheHit;

        /// <summary>
        /// Engine type display string.
        /// </summary>
        public string EngineTypeDisplay => _node.EngineType switch
        {
            EngineType.StorageEngine => "Storage Engine (SE)",
            EngineType.FormulaEngine => "Formula Engine (FE)",
            _ => "Unknown"
        };

        /// <summary>
        /// Number of child nodes.
        /// </summary>
        public int ChildCount => Children.Count;

        /// <summary>
        /// Whether this node has children.
        /// </summary>
        public bool HasChildren => Children.Count > 0;

        /// <summary>
        /// Parent node display info.
        /// </summary>
        public string ParentInfo => Parent != null
            ? $"{Parent.DisplayText} (Node {Parent.NodeId})"
            : "None (Root)";

        /// <summary>
        /// Engine type (StorageEngine, FormulaEngine, Unknown).
        /// </summary>
        public EngineType EngineType => _node.EngineType;

        /// <summary>
        /// Whether this node has performance issues.
        /// </summary>
        public bool HasIssues => _node.Issues.Count > 0;

        /// <summary>
        /// Number of issues on this node.
        /// </summary>
        public int IssueCount => _node.Issues.Count;

        /// <summary>
        /// Performance issues detected on this node.
        /// </summary>
        public IReadOnlyList<PerformanceIssue> Issues => _node.Issues;

        /// <summary>
        /// Whether this node is currently selected.
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
                    NotifyOfPropertyChange(nameof(BorderBrush));
                }
            }
        }

        /// <summary>
        /// Whether the node's children are expanded.
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
                }
            }
        }

        /// <summary>
        /// Position of the node in the graph layout.
        /// </summary>
        public Point Position
        {
            get => _position;
            set
            {
                if (_position != value)
                {
                    _position = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(X));
                    NotifyOfPropertyChange(nameof(Y));
                    NotifyOfPropertyChange(nameof(CenterX));
                    NotifyOfPropertyChange(nameof(CenterY));
                    NotifyOfPropertyChange(nameof(EdgeTop));
                    NotifyOfPropertyChange(nameof(EdgeBottom));
                    NotifyOfPropertyChange(nameof(EdgeTopControl));
                    NotifyOfPropertyChange(nameof(EdgeBottomControl));
                }
            }
        }

        /// <summary>
        /// X coordinate in the graph.
        /// </summary>
        public double X => _position.X;

        /// <summary>
        /// Y coordinate in the graph.
        /// </summary>
        public double Y => _position.Y;

        /// <summary>
        /// Center X coordinate of the node.
        /// </summary>
        public double CenterX => _position.X + Width / 2;

        /// <summary>
        /// Center Y coordinate of the node.
        /// </summary>
        public double CenterY => _position.Y + Height / 2;

        /// <summary>
        /// Top center point for edge connections (incoming from parent).
        /// </summary>
        public Point EdgeTop => new Point(CenterX, Y);

        /// <summary>
        /// Bottom center point for edge connections (outgoing to children).
        /// </summary>
        public Point EdgeBottom => new Point(CenterX, Y + Height);

        /// <summary>
        /// Control point for bezier curve from top edge.
        /// </summary>
        public Point EdgeTopControl => new Point(CenterX, Y - 30);

        /// <summary>
        /// Control point for bezier curve from bottom edge.
        /// </summary>
        public Point EdgeBottomControl => new Point(CenterX, Y + Height + 30);

        /// <summary>
        /// Width of the node for rendering.
        /// Increased from 180 to 200 to reduce text truncation.
        /// </summary>
        public double Width { get; set; } = 200;

        /// <summary>
        /// Height of the node for rendering.
        /// Increased from 65 to 70 to accommodate badges and row count.
        /// </summary>
        public double Height { get; set; } = 70;

        /// <summary>
        /// Parent node in the tree.
        /// </summary>
        public PlanNodeViewModel Parent { get; set; }

        /// <summary>
        /// Child nodes.
        /// </summary>
        public BindableCollection<PlanNodeViewModel> Children { get; }

        /// <summary>
        /// Background color - subtle shading based on engine type.
        /// Only highlight nodes with issues, otherwise use neutral colors.
        /// </summary>
        public Brush BackgroundBrush
        {
            get
            {
                if (HasIssues)
                {
                    // Subtle highlight for nodes with issues
                    var severity = Issues.Max(i => i.Severity);
                    return severity == IssueSeverity.Error
                        ? new SolidColorBrush(Color.FromRgb(255, 235, 235))  // Very light red
                        : new SolidColorBrush(Color.FromRgb(255, 245, 230)); // Very light orange
                }

                // Subtle engine-based background - much lighter than before
                return EngineType switch
                {
                    EngineType.StorageEngine => new SolidColorBrush(Color.FromRgb(245, 250, 245)), // Very light green tint
                    EngineType.FormulaEngine => new SolidColorBrush(Color.FromRgb(250, 248, 252)), // Very light purple tint
                    _ => new SolidColorBrush(Color.FromRgb(248, 248, 248))                          // Near white
                };
            }
        }

        /// <summary>
        /// Border color - subtle by default, blue when selected.
        /// </summary>
        public Brush BorderBrush
        {
            get
            {
                if (IsSelected)
                {
                    return new SolidColorBrush(Color.FromRgb(0, 120, 215)); // Blue selection
                }

                // Subtle border based on engine type - much lighter than before
                return EngineType switch
                {
                    EngineType.StorageEngine => new SolidColorBrush(Color.FromRgb(180, 200, 180)), // Light green-gray
                    EngineType.FormulaEngine => new SolidColorBrush(Color.FromRgb(200, 180, 200)), // Light purple-gray
                    _ => new SolidColorBrush(Color.FromRgb(200, 200, 200))                          // Light gray
                };
            }
        }

        /// <summary>
        /// Icon resource key based on engine type.
        /// </summary>
        public string IconResource
        {
            get
            {
                return EngineType switch
                {
                    EngineType.StorageEngine => "database_smallDrawingImage",
                    EngineType.FormulaEngine => "functionDrawingImage",
                    _ => "query_planDrawingImage"
                };
            }
        }

        /// <summary>
        /// Extracts the operation name from the full operation string.
        /// Handles various formats:
        /// - Physical: "AddColumns: RelLogOp DependOnCols()() 0-3"
        /// - Logical: "'Internet Sales'[Sales Amount]: ScaLogOp DependOnCols(106)..."
        /// </summary>
        private string GetOperationName()
        {
            var op = ResolvedOperation ?? Operation ?? string.Empty;
            if (string.IsNullOrWhiteSpace(op))
                return string.Empty;

            // DEBUG: Log raw operation for debugging - REMOVE BEFORE RELEASE
            Serilog.Log.Debug(">>> GetOperationName: Input='{Op}'", op.Substring(0, Math.Min(100, op.Length)));

            // Check if this is a column reference format: 'Table'[Column]: Operator
            // These start with a single quote
            if (op.StartsWith("'"))
            {
                // Find the colon that separates the column reference from the operator
                // Need to handle nested brackets in column names
                var colonIndex = FindOperatorColonIndex(op);
                if (colonIndex > 0 && colonIndex < op.Length - 2)
                {
                    // Extract everything after the colon
                    var afterColon = op.Substring(colonIndex + 1).TrimStart();
                    // Get the operator name (first word)
                    var spaceIndex = afterColon.IndexOf(' ');
                    var result = spaceIndex > 0 ? afterColon.Substring(0, spaceIndex) : afterColon;
                    Serilog.Log.Debug(">>> GetOperationName: Column reference format, extracted='{Result}'", result);
                    return result;
                }
            }

            // Standard format: "Operator: Details" or "Operator Details"
            var colonIdx = op.IndexOf(':');
            if (colonIdx > 0)
            {
                // Check if there's a space before the colon (means colon is not the operator separator)
                var firstSpace = op.IndexOf(' ');
                if (firstSpace > 0 && firstSpace < colonIdx)
                {
                    // Space comes before colon, use space as delimiter
                    var result = op.Substring(0, firstSpace);
                    Serilog.Log.Debug(">>> GetOperationName: Space before colon, extracted='{Result}'", result);
                    return result;
                }
                // Colon is the delimiter
                var colonResult = op.Substring(0, colonIdx);
                Serilog.Log.Debug(">>> GetOperationName: Colon format, extracted='{Result}'", colonResult);
                return colonResult;
            }

            // No colon, use first space
            var idx = op.IndexOf(' ');
            if (idx > 0)
            {
                var result = op.Substring(0, idx);
                Serilog.Log.Debug(">>> GetOperationName: Space format, extracted='{Result}'", result);
                return result;
            }

            Serilog.Log.Debug(">>> GetOperationName: No delimiter, returning full='{Op}'", op);
            return op;
        }

        /// <summary>
        /// Finds the colon that separates a column reference from the operator name.
        /// Handles nested brackets in column/table names.
        /// Format: 'Table Name'[Column Name]: OperatorName
        /// </summary>
        private int FindOperatorColonIndex(string op)
        {
            int bracketDepth = 0;
            bool inQuote = false;

            for (int i = 0; i < op.Length; i++)
            {
                char c = op[i];

                if (c == '\'')
                {
                    inQuote = !inQuote;
                }
                else if (!inQuote)
                {
                    if (c == '[')
                        bracketDepth++;
                    else if (c == ']')
                        bracketDepth--;
                    else if (c == ':' && bracketDepth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        #region Node Collapsing for Simple Comparisons

        private bool _isCollapsed;
        private List<PlanNodeViewModel> _collapsedChildren;

        /// <summary>
        /// Whether this node's children have been collapsed into the node display.
        /// </summary>
        public bool IsCollapsed
        {
            get => _isCollapsed;
            set
            {
                if (_isCollapsed != value)
                {
                    _isCollapsed = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(DisplayText));
                    NotifyOfPropertyChange(nameof(VisibleChildren));
                }
            }
        }

        /// <summary>
        /// Children that have been collapsed into this node (stored for details panel).
        /// </summary>
        public List<PlanNodeViewModel> CollapsedChildren
        {
            get => _collapsedChildren ?? new List<PlanNodeViewModel>();
            set => _collapsedChildren = value;
        }

        /// <summary>
        /// Children that are visible in the tree (excludes collapsed children).
        /// </summary>
        public IEnumerable<PlanNodeViewModel> VisibleChildren =>
            IsCollapsed ? Enumerable.Empty<PlanNodeViewModel>() : Children;

        /// <summary>
        /// Whether this node has collapsed children.
        /// </summary>
        public bool HasCollapsedChildren => IsCollapsed && CollapsedChildren.Count > 0;

        /// <summary>
        /// Operators that represent comparisons and can be collapsed with their operands.
        /// </summary>
        private static readonly HashSet<string> ComparisonOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "GreaterThan", "GreaterOrEqualTo", "LessThan", "LessOrEqualTo",
            "Equal", "NotEqual", "Cmp_GreaterThan", "Cmp_GreaterOrEqualTo",
            "Cmp_LessThan", "Cmp_LessOrEqualTo", "Cmp_Equal", "Cmp_NotEqual"
        };

        /// <summary>
        /// Operators that represent simple values (columns, constants).
        /// </summary>
        private static readonly HashSet<string> ValueOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Constant", "ColValue", "Coerce", "Integer", "Real", "String", "Currency", "DateTime", "Boolean"
        };

        /// <summary>
        /// Gets the comparison symbol for display.
        /// </summary>
        public string ComparisonSymbol
        {
            get
            {
                var op = OperatorName;
                if (op.IndexOf("GreaterOrEqual", StringComparison.OrdinalIgnoreCase) >= 0) return ">=";
                if (op.IndexOf("LessOrEqual", StringComparison.OrdinalIgnoreCase) >= 0) return "<=";
                if (op.IndexOf("GreaterThan", StringComparison.OrdinalIgnoreCase) >= 0) return ">";
                if (op.IndexOf("LessThan", StringComparison.OrdinalIgnoreCase) >= 0) return "<";
                if (op.IndexOf("NotEqual", StringComparison.OrdinalIgnoreCase) >= 0) return "<>";
                if (op.IndexOf("Equal", StringComparison.OrdinalIgnoreCase) >= 0) return "=";
                return "?";
            }
        }

        /// <summary>
        /// Whether this node is a comparison operator.
        /// </summary>
        public bool IsComparisonOperator => ComparisonOperators.Contains(OperatorName);

        /// <summary>
        /// Whether this node is a value operator (Constant, ColValue, etc).
        /// </summary>
        public bool IsValueOperator => ValueOperators.Contains(OperatorName);

        /// <summary>
        /// Checks if this comparison node can be collapsed with its children.
        /// Collapsible if: it's a comparison with exactly 2 value-type children.
        /// </summary>
        public bool CanCollapseComparison
        {
            get
            {
                if (!IsComparisonOperator)
                    return false;

                // Must have exactly 2 children
                if (Children.Count != 2)
                    return false;

                // Both children should be simple value operators
                return Children.All(c => c.IsValueOperator || c.Children.Count == 0);
            }
        }

        /// <summary>
        /// Gets a simplified value display from a child node.
        /// </summary>
        private string GetValueDisplay(PlanNodeViewModel child)
        {
            var op = child.Operation ?? "";

            // Look for column reference like 'Table'[Column]
            var colMatch = Regex.Match(op, @"'([^']+)'\[([^\]]+)\]");
            if (colMatch.Success)
                return $"[{colMatch.Groups[2].Value}]";

            // Look for just [Column]
            var simpleColMatch = Regex.Match(op, @"\[([^\]]+)\]");
            if (simpleColMatch.Success)
                return $"[{simpleColMatch.Groups[1].Value}]";

            // Look for constant values - numbers, strings, etc
            var constMatch = Regex.Match(op, @"Constant[:\s]+(\S+)|:\s*(\d+(?:\.\d+)?)\s*$|Integer\s+(\d+)|Real\s+([\d.]+)|String\s+'([^']*)'");
            if (constMatch.Success)
            {
                for (int i = 1; i <= 5; i++)
                {
                    if (constMatch.Groups[i].Success)
                        return constMatch.Groups[i].Value;
                }
            }

            // If nothing specific found, use the operator name
            return child.OperatorName;
        }

        /// <summary>
        /// Gets the collapsed display text for a comparison node.
        /// E.g., "[Amount] > 100" instead of showing 3 separate nodes.
        /// </summary>
        public string CollapsedDisplayText
        {
            get
            {
                if (!IsCollapsed || CollapsedChildren.Count != 2)
                    return DisplayText;

                var left = GetValueDisplay(CollapsedChildren[0]);
                var right = GetValueDisplay(CollapsedChildren[1]);

                return $"{left} {ComparisonSymbol} {right}";
            }
        }

        /// <summary>
        /// Collapses this comparison node with its children if possible.
        /// </summary>
        public void CollapseIfPossible()
        {
            if (!CanCollapseComparison)
                return;

            // Store children before clearing
            _collapsedChildren = Children.ToList();

            // Clear visible children
            Children.Clear();

            IsCollapsed = true;
        }

        #endregion

        /// <summary>
        /// Creates a tree of PlanNodeViewModels from a flat list of EnrichedPlanNodes.
        /// </summary>
        public static PlanNodeViewModel BuildTree(EnrichedQueryPlan plan)
        {
            if (plan?.RootNode == null)
                return null;

            var nodeMap = new Dictionary<int, PlanNodeViewModel>();

            // Create ViewModels for all nodes
            foreach (var node in plan.AllNodes)
            {
                nodeMap[node.NodeId] = new PlanNodeViewModel(node);
            }

            // Build parent-child relationships
            foreach (var node in plan.AllNodes)
            {
                var vm = nodeMap[node.NodeId];
                if (node.Parent != null && nodeMap.TryGetValue(node.Parent.NodeId, out var parentVm))
                {
                    vm.Parent = parentVm;
                    parentVm.Children.Add(vm);
                }
            }

            return nodeMap.TryGetValue(plan.RootNode.NodeId, out var root) ? root : null;
        }
    }
}
