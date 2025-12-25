using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using Caliburn.Micro;
using DaxStudio.UI.Model;
using Serilog;

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
        /// For Filter nodes, shows the filter predicate expression.
        /// </summary>
        public string DisplayText
        {
            get
            {
                // If collapsed, show the collapsed comparison text
                // Word wrap enabled in XAML, so no truncation needed
                if (IsCollapsed && CollapsedChildren.Count == 2)
                {
                    return CollapsedDisplayText;
                }

                var name = DisplayName;
                var detail = DisplayDetail;

                if (string.IsNullOrEmpty(detail))
                    return name;

                return $"{name}: {detail}";
            }
        }

        /// <summary>
        /// The operator display name (bold part).
        /// </summary>
        public string DisplayName
        {
            get
            {
                if (IsCollapsed && CollapsedChildren.Count == 2)
                {
                    // For collapsed nodes, the full text is in CollapsedDisplayText
                    return CollapsedDisplayText;
                }

                var opName = GetOperationName();
                if (string.IsNullOrEmpty(opName))
                    return $"Node {NodeId}";

                var displayName = DaxOperatorDictionary.GetDisplayName(opName);

                // Add depth indicator for nested spool chains
                if (IsNestedSpoolChain)
                    return $"{displayName} (×{NestedSpoolDepth})";

                return displayName;
            }
        }

        /// <summary>
        /// The detail text after the colon (normal weight).
        /// </summary>
        public string DisplayDetail
        {
            get
            {
                // Collapsed nodes don't have separate detail
                if (IsCollapsed && CollapsedChildren.Count == 2)
                    return null;

                if (HasFilterPredicate)
                    return FilterPredicateExpression;

                if (HasSpoolTypeInfo)
                    return SpoolTypeInfo;

                if (HasScanColumnInfo)
                    return ScanColumnInfo;

                if (HasCacheColumnInfo)
                    return CacheColumnInfo;

                // For Constant nodes, show the value from DominantValue
                if (OperatorName == "Constant" && !string.IsNullOrEmpty(DominantValue))
                    return FormatConstantValue(DominantValue);

                return null;
            }
        }

        /// <summary>
        /// Formats a constant value for display (e.g., "true" → "TRUE()", "0.5" → "0.5").
        /// </summary>
        private string FormatConstantValue(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            // Boolean values become DAX functions
            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return "TRUE()";
            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return "FALSE()";

            // BLANK becomes BLANK()
            if (value.Equals("BLANK", StringComparison.OrdinalIgnoreCase))
                return "BLANK()";

            // NONE means no dominant value (dense)
            if (value.Equals("NONE", StringComparison.OrdinalIgnoreCase))
                return null;

            // Numeric and other values shown as-is
            return value;
        }

        /// <summary>
        /// Whether this node has detail text to display.
        /// </summary>
        public bool HasDisplayDetail => !string.IsNullOrEmpty(DisplayDetail);

        /// <summary>
        /// The detail text with colon prefix (": detail") or empty if no detail.
        /// Used for XAML binding to show colon only when detail exists.
        /// </summary>
        public string DisplayDetailWithColon => HasDisplayDetail ? $": {DisplayDetail}" : "";

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
        /// dax.guide URL for the corresponding DAX function, if applicable.
        /// </summary>
        public string DaxGuideUrl => DaxOperatorDictionary.GetDaxGuideUrl(GetOperationName());

        /// <summary>
        /// Whether this operator has a dax.guide URL available.
        /// </summary>
        public bool HasDaxGuideUrl => !string.IsNullOrEmpty(DaxGuideUrl);

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

                // Add full operation string (DAX code) if it provides more info than the display name
                var fullOp = _node.ResolvedOperation ?? _node.Operation;
                if (!string.IsNullOrEmpty(fullOp) && fullOp != displayName)
                {
                    lines.Add("");
                    lines.Add("Operation:");
                    // Truncate very long operations to keep tooltip readable
                    if (fullOp.Length > 300)
                    {
                        lines.Add(fullOp.Substring(0, 297) + "...");
                    }
                    else
                    {
                        lines.Add(fullOp);
                    }
                }

                // Add xmSQL if available (storage engine query)
                if (!string.IsNullOrEmpty(_node.XmSql))
                {
                    lines.Add("");
                    lines.Add("xmSQL:");
                    var xmSqlDisplay = _node.ResolvedXmSql ?? _node.XmSql;
                    // Truncate very long xmSQL to keep tooltip readable
                    if (xmSqlDisplay.Length > 300)
                    {
                        lines.Add(xmSqlDisplay.Substring(0, 297) + "...");
                    }
                    else
                    {
                        lines.Add(xmSqlDisplay);
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
        /// Whether records info is available (either from plan data or from row range).
        /// </summary>
        public bool HasRecords => _node.Records.HasValue || HasRowRange;

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
        /// Whether any performance metrics are available (rows, duration, CPU time, data size, cost).
        /// Used to hide the entire PERFORMANCE METRICS section when no data exists.
        /// </summary>
        public bool HasAnyPerformanceMetrics =>
            _node.Records.HasValue ||
            _node.DurationMs.HasValue ||
            _node.CpuTimeMs.HasValue ||
            _node.NetParallelDurationMs.HasValue ||
            _node.EstimatedKBytes.HasValue ||
            _node.CostPercentage.HasValue;

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
        /// When a SpoolLookup/Spool_Iterator pair is folded, shows the row range (e.g., "1-11 rows").
        /// </summary>
        public string RowsDisplayFormatted
        {
            get
            {
                // Show row range if SpoolLookup was folded with Spool_Iterator
                if (HasRowRange)
                    return RowRangeDisplay;

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
                {
                    var measureName = match.Groups[2].Value;
                    // Don't return system property names as measure references
                    if (!SystemPropertyNames.Contains(measureName))
                        return $"[{measureName}]";
                }

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

        // System property names that should not be treated as DAX column references
        private static readonly HashSet<string> SystemPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "IterCols", "LookupCols", "RequiredCols", "DependOnCols", "JoinCols", "SemijoinCols",
            "KeyCols", "ValueCols", "FieldCols", "BlankRow", "MeasureRef", "LogOp", "Table"
        };

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
                    .Where(col => !SystemPropertyNames.Contains(col)) // Filter out system properties
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

        #region Query Plan Properties (Column Lists, BlankRow, Table ID)

        // Regex patterns for extracting column list properties
        // Format: PropertyName(indices)(columns) e.g., RequiredCols(0, 1)('T'[Col1], 'T'[Col2])
        private static readonly Regex RequiredColsPattern = new Regex(
            @"RequiredCols\(([^)]*)\)\(([^)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DependOnColsPattern = new Regex(
            @"DependOnCols\(([^)]*)\)\(([^)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex JoinColsPattern = new Regex(
            @"JoinCols\(([^)]*)\)\(([^)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SemijoinColsPattern = new Regex(
            @"SemijoinCols\(([^)]*)\)\(([^)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex IterColsPattern = new Regex(
            @"IterCols\(([^)]*)\)\(([^)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LookupColsPattern = new Regex(
            @"LookupCols\(([^)]*)\)\(([^)]*)\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BlankRowPattern = new Regex(
            @"([+-])BlankRow",
            RegexOptions.Compiled);

        private static readonly Regex TableIdPattern = new Regex(
            @"Table=(\d+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Extracts column list from a regex match.
        /// Returns the column names or "empty" if none.
        /// </summary>
        private string ExtractColumnList(Regex pattern)
        {
            var op = _node.Operation ?? string.Empty;
            var match = pattern.Match(op);
            if (!match.Success)
                return null;

            var columns = match.Groups[2].Value.Trim();
            if (string.IsNullOrEmpty(columns))
                return "(empty)";

            return columns;
        }

        /// <summary>
        /// Extracts column indices from a regex match.
        /// </summary>
        private string ExtractColumnIndices(Regex pattern)
        {
            var op = _node.Operation ?? string.Empty;
            var match = pattern.Match(op);
            if (!match.Success)
                return null;

            var indices = match.Groups[1].Value.Trim();
            if (string.IsNullOrEmpty(indices))
                return "(empty)";

            return indices;
        }

        /// <summary>
        /// Required columns for this operation (from RequiredCols pattern).
        /// </summary>
        public string RequiredCols => ExtractColumnList(RequiredColsPattern);

        /// <summary>
        /// Required column indices.
        /// </summary>
        public string RequiredColsIndices => ExtractColumnIndices(RequiredColsPattern);

        /// <summary>
        /// Whether RequiredCols info is available.
        /// </summary>
        public bool HasRequiredCols => RequiredCols != null;

        /// <summary>
        /// Columns this operation depends on (from DependOnCols pattern).
        /// </summary>
        public string DependOnCols => ExtractColumnList(DependOnColsPattern);

        /// <summary>
        /// DependOnCols indices.
        /// </summary>
        public string DependOnColsIndices => ExtractColumnIndices(DependOnColsPattern);

        /// <summary>
        /// Whether DependOnCols info is available.
        /// </summary>
        public bool HasDependOnCols => DependOnCols != null;

        /// <summary>
        /// Join columns for relationship operations (from JoinCols pattern).
        /// </summary>
        public string JoinCols => ExtractColumnList(JoinColsPattern);

        /// <summary>
        /// JoinCols indices.
        /// </summary>
        public string JoinColsIndices => ExtractColumnIndices(JoinColsPattern);

        /// <summary>
        /// Whether JoinCols info is available.
        /// </summary>
        public bool HasJoinCols => JoinCols != null;

        /// <summary>
        /// Semijoin columns for filtering (from SemijoinCols pattern).
        /// </summary>
        public string SemijoinCols => ExtractColumnList(SemijoinColsPattern);

        /// <summary>
        /// SemijoinCols indices.
        /// </summary>
        public string SemijoinColsIndices => ExtractColumnIndices(SemijoinColsPattern);

        /// <summary>
        /// Whether SemijoinCols info is available.
        /// </summary>
        public bool HasSemijoinCols => SemijoinCols != null;

        /// <summary>
        /// Iterator output columns (from IterCols pattern).
        /// </summary>
        public string IterCols => ExtractColumnList(IterColsPattern);

        /// <summary>
        /// IterCols indices.
        /// </summary>
        public string IterColsIndices => ExtractColumnIndices(IterColsPattern);

        /// <summary>
        /// Whether IterCols info is available.
        /// </summary>
        public bool HasIterCols => IterCols != null;

        /// <summary>
        /// Lookup input columns (from LookupCols pattern).
        /// </summary>
        public string LookupCols => ExtractColumnList(LookupColsPattern);

        /// <summary>
        /// LookupCols indices.
        /// </summary>
        public string LookupColsIndices => ExtractColumnIndices(LookupColsPattern);

        /// <summary>
        /// Whether LookupCols info is available.
        /// </summary>
        public bool HasLookupCols => LookupCols != null;

        /// <summary>
        /// BlankRow handling indicator (+BlankRow or -BlankRow).
        /// + means blank rows are included in scan, - means excluded.
        /// </summary>
        public string BlankRowIndicator
        {
            get
            {
                var op = _node.Operation ?? string.Empty;
                var match = BlankRowPattern.Match(op);
                if (!match.Success)
                    return null;

                return match.Groups[1].Value == "+" ? "+BlankRow" : "-BlankRow";
            }
        }

        /// <summary>
        /// Whether BlankRow is included in the scan.
        /// </summary>
        public bool? IncludesBlankRow
        {
            get
            {
                var indicator = BlankRowIndicator;
                if (indicator == null)
                    return null;

                return indicator == "+BlankRow";
            }
        }

        /// <summary>
        /// Whether BlankRow indicator is available.
        /// </summary>
        public bool HasBlankRowIndicator => BlankRowIndicator != null;

        /// <summary>
        /// Display text for BlankRow status.
        /// </summary>
        public string BlankRowDisplay
        {
            get
            {
                var indicator = BlankRowIndicator;
                if (indicator == null)
                    return null;

                return indicator == "+BlankRow"
                    ? "Includes blank row"
                    : "Excludes blank row";
            }
        }

        /// <summary>
        /// Internal table ID for root table reference (from Table=N pattern).
        /// </summary>
        public int? TableId
        {
            get
            {
                var op = _node.Operation ?? string.Empty;
                var match = TableIdPattern.Match(op);
                if (!match.Success)
                    return null;

                if (int.TryParse(match.Groups[1].Value, out var id))
                    return id;

                return null;
            }
        }

        /// <summary>
        /// Whether TableId is available.
        /// </summary>
        public bool HasTableId => TableId.HasValue;

        /// <summary>
        /// Data type of scalar output (from operation string).
        /// Common types: Integer, String, Currency, Boolean, DateTime, Real
        /// </summary>
        public string DataType
        {
            get
            {
                var op = _node.Operation ?? string.Empty;

                // Look for common data type keywords
                var types = new[] { "Integer", "String", "Currency", "Boolean", "DateTime", "Real", "Variant" };
                foreach (var type in types)
                {
                    // Look for type as a standalone word (not part of another word)
                    var pattern = $@"\b{type}\b";
                    if (Regex.IsMatch(op, pattern, RegexOptions.IgnoreCase))
                        return type;
                }

                return null;
            }
        }

        /// <summary>
        /// Whether DataType info is available.
        /// </summary>
        public bool HasDataType => !string.IsNullOrEmpty(DataType);

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
            // Special case: "__DS0Core: Union: RelLogOp VarName=..." - variable name followed by operator
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

                // Check for variable name pattern: "VarName: Operator: Suffix"
                // This occurs with __DS0Core, __DS0FilterTable, etc.
                var beforeColon = op.Substring(0, colonIdx);
                if (beforeColon.StartsWith("__") || beforeColon.StartsWith("_"))
                {
                    // This looks like a variable name, check for second colon
                    var afterFirstColon = op.Substring(colonIdx + 1).TrimStart();
                    var secondColonIdx = afterFirstColon.IndexOf(':');
                    if (secondColonIdx > 0)
                    {
                        // Extract the actual operator (between first and second colon)
                        var operatorName = afterFirstColon.Substring(0, secondColonIdx).Trim();
                        Serilog.Log.Debug(">>> GetOperationName: Variable prefix format, var='{Var}', operator='{Op}'", beforeColon, operatorName);
                        return operatorName;
                    }
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

                // Single quotes are only quote delimiters when outside of brackets.
                // Inside brackets, they're apostrophes in column names like [Customer's Order]
                if (c == '\'' && bracketDepth == 0)
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
        /// For Filter nodes, stores the predicate expression (e.g., "[FirstName] > \"Bob\"").
        /// </summary>
        public string FilterPredicateExpression { get; set; }

        /// <summary>
        /// Whether this node has a filter predicate expression to display.
        /// </summary>
        public bool HasFilterPredicate => !string.IsNullOrEmpty(FilterPredicateExpression);

        /// <summary>
        /// For Spool_Iterator nodes, stores the spool type from the folded child
        /// (e.g., "AggregationSpool&lt;GroupBy&gt;").
        /// </summary>
        public string SpoolTypeInfo { get; set; }

        /// <summary>
        /// Whether this node has spool type info to display.
        /// </summary>
        public bool HasSpoolTypeInfo => !string.IsNullOrEmpty(SpoolTypeInfo);

        /// <summary>
        /// Column info for single-column Scan_Vertipaq nodes (e.g., "'Customer'[First Name]").
        /// </summary>
        public string ScanColumnInfo { get; set; }

        /// <summary>
        /// Whether this node has scan column info for display.
        /// </summary>
        public bool HasScanColumnInfo => !string.IsNullOrEmpty(ScanColumnInfo);

        /// <summary>
        /// Column info for Cache nodes, inferred from ancestor Spool_Iterator IterCols.
        /// </summary>
        public string CacheColumnInfo { get; set; }

        /// <summary>
        /// Whether this node has cache column info for display.
        /// </summary>
        public bool HasCacheColumnInfo => !string.IsNullOrEmpty(CacheColumnInfo);

        /// <summary>
        /// Type coercion info when a Variant->* node was folded into this node.
        /// (e.g., "Variant->Numeric/Date" indicates this node's output was coerced)
        /// </summary>
        public string TypeCoercionInfo { get; set; }

        /// <summary>
        /// Whether this node has type coercion info from a folded Variant node.
        /// </summary>
        public bool HasTypeCoercion => !string.IsNullOrEmpty(TypeCoercionInfo);

        /// <summary>
        /// Number of nested Spool_Iterator nodes that have been collapsed into this one.
        /// 1 means just this node (no nesting), 2+ means nested chain collapsed.
        /// </summary>
        public int NestedSpoolDepth { get; set; } = 1;

        /// <summary>
        /// Whether this node represents a collapsed chain of multiple Spool_Iterator nodes.
        /// </summary>
        public bool IsNestedSpoolChain => NestedSpoolDepth > 1;

        /// <summary>
        /// Minimum row count in a SpoolLookup/Spool_Iterator folded range.
        /// When SpoolLookup (1 row) folds with Spool_Iterator (11 rows), shows "1-11 rows".
        /// </summary>
        public long? SpoolRowRangeMin { get; set; }

        /// <summary>
        /// Maximum row count in a SpoolLookup/Spool_Iterator folded range.
        /// </summary>
        public long? SpoolRowRangeMax { get; set; }

        /// <summary>
        /// Whether this node has a row range from folded spool operations.
        /// </summary>
        public bool HasRowRange => SpoolRowRangeMin.HasValue && SpoolRowRangeMax.HasValue &&
                                   SpoolRowRangeMin != SpoolRowRangeMax;

        /// <summary>
        /// Display string for row range (e.g., "1-11 rows").
        /// </summary>
        public string RowRangeDisplay => HasRowRange
            ? $"{SpoolRowRangeMin:N0}-{SpoolRowRangeMax:N0} rows"
            : string.Empty;

        #region Execution Metrics (Root Node Only)

        /// <summary>
        /// Whether this is the root node of the plan tree.
        /// </summary>
        public bool IsRootNode { get; set; }

        /// <summary>
        /// Total query duration in milliseconds (root node only).
        /// </summary>
        public long PlanTotalDurationMs { get; set; }

        /// <summary>
        /// Storage Engine duration in milliseconds (root node only).
        /// </summary>
        public long PlanStorageEngineDurationMs { get; set; }

        /// <summary>
        /// Formula Engine duration in milliseconds (root node only).
        /// </summary>
        public long PlanFormulaEngineDurationMs { get; set; }

        /// <summary>
        /// Storage Engine CPU time in milliseconds (root node only).
        /// </summary>
        public long PlanStorageEngineCpuMs { get; set; }

        /// <summary>
        /// Number of Storage Engine queries executed (root node only).
        /// </summary>
        public int PlanStorageEngineQueryCount { get; set; }

        /// <summary>
        /// Number of VertiPaq cache hits (root node only).
        /// </summary>
        public int PlanCacheHits { get; set; }

        /// <summary>
        /// Whether this node has execution metrics to display.
        /// </summary>
        public bool HasExecutionMetrics => IsRootNode && PlanTotalDurationMs > 0;

        /// <summary>
        /// SE parallelism factor (CPU time / duration).
        /// </summary>
        public string SEParallelismFactor => PlanStorageEngineDurationMs > 0
            ? $"x{(double)PlanStorageEngineCpuMs / PlanStorageEngineDurationMs:F1}"
            : "";

        #endregion

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
        /// Operators that represent arithmetic operations and can be collapsed with their operands.
        /// </summary>
        private static readonly HashSet<string> ArithmeticOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Multiply", "Divide", "Add", "Subtract", "Mod", "Power",
            "Mul", "Div", "Sub", "Plus", "Minus"
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
        /// Gets the arithmetic symbol for display.
        /// </summary>
        public string ArithmeticSymbol
        {
            get
            {
                var op = OperatorName;
                // Use exact match to avoid false positives (e.g., "AddColumns" matching "Add")
                if (op.Equals("Multiply", StringComparison.OrdinalIgnoreCase) ||
                    op.Equals("Mul", StringComparison.OrdinalIgnoreCase)) return "*";
                if (op.Equals("Divide", StringComparison.OrdinalIgnoreCase) ||
                    op.Equals("Div", StringComparison.OrdinalIgnoreCase)) return "/";
                if (op.Equals("Add", StringComparison.OrdinalIgnoreCase) ||
                    op.Equals("Plus", StringComparison.OrdinalIgnoreCase)) return "+";
                if (op.Equals("Subtract", StringComparison.OrdinalIgnoreCase) ||
                    op.Equals("Sub", StringComparison.OrdinalIgnoreCase) ||
                    op.Equals("Minus", StringComparison.OrdinalIgnoreCase)) return "-";
                if (op.Equals("Mod", StringComparison.OrdinalIgnoreCase)) return "%";
                if (op.Equals("Power", StringComparison.OrdinalIgnoreCase)) return "^";
                return "?";
            }
        }

        /// <summary>
        /// Whether this node is a comparison operator.
        /// </summary>
        public bool IsComparisonOperator => ComparisonOperators.Contains(OperatorName);

        /// <summary>
        /// Whether this node is an arithmetic operator.
        /// </summary>
        public bool IsArithmeticOperator => ArithmeticOperators.Contains(OperatorName);

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
        /// Checks if this arithmetic node can be collapsed with its children.
        /// Collapsible if: it's an arithmetic operator with exactly 2 simple value-type children.
        /// Does NOT collapse nested arithmetic to avoid confusing display like "Multiply + [C]".
        /// </summary>
        public bool CanCollapseArithmetic
        {
            get
            {
                if (!IsArithmeticOperator)
                    return false;

                // Must have exactly 2 children
                if (Children.Count != 2)
                    return false;

                // Both children should be simple value operators (NOT nested arithmetic)
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
        /// Gets the collapsed display text for a comparison or arithmetic node.
        /// E.g., "[Amount] > 100" or "[Qty] * [Price]" instead of showing 3 separate nodes.
        /// </summary>
        public string CollapsedDisplayText
        {
            get
            {
                if (!IsCollapsed || CollapsedChildren.Count != 2)
                    return DisplayText;

                var left = GetValueDisplay(CollapsedChildren[0]);
                var right = GetValueDisplay(CollapsedChildren[1]);

                // Use the appropriate symbol based on operator type
                var symbol = IsArithmeticOperator ? ArithmeticSymbol : ComparisonSymbol;

                return $"{left} {symbol} {right}";
            }
        }

        /// <summary>
        /// Collapses this comparison or arithmetic node with its children if possible.
        /// </summary>
        public void CollapseIfPossible()
        {
            if (!CanCollapseComparison && !CanCollapseArithmetic)
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
        /// Folds column reference nodes and filter predicates into their parents for cleaner visualization.
        /// </summary>
        public static PlanNodeViewModel BuildTree(EnrichedQueryPlan plan)
        {
            if (plan?.RootNode == null)
                return null;

            var nodeMap = new Dictionary<int, PlanNodeViewModel>();
            var foldedNodeIds = new HashSet<int>();
            var filterPredicateExpressions = new Dictionary<int, string>();

            // First pass: identify nodes that should be folded into their parent
            foreach (var node in plan.AllNodes)
            {
                if (ShouldFoldNode(node))
                {
                    foldedNodeIds.Add(node.NodeId);
                }
            }

            // Second pass: identify Filter nodes and collect their predicate subtrees
            foreach (var node in plan.AllNodes)
            {
                var opName = GetOperatorNameFromString(node.Operation);
                if (IsFilterOperator(opName))
                {
                    Log.Debug(">>> BuildTree: Found Filter node {NodeId}, opName={OpName}", node.NodeId, opName);

                    // Build the predicate expression for display
                    var predicateExpr = BuildFilterPredicateExpression(node, plan.AllNodes);
                    Log.Debug(">>> BuildTree: PredicateExpr for node {NodeId} = {Expr}", node.NodeId, predicateExpr ?? "(null)");

                    if (!string.IsNullOrEmpty(predicateExpr))
                    {
                        filterPredicateExpressions[node.NodeId] = predicateExpr;
                        Log.Debug(">>> BuildTree: Stored predicate for node {NodeId}", node.NodeId);
                    }

                    // Collect all predicate node IDs to fold
                    var predicateNodeIds = CollectFilterPredicateNodeIds(node, plan.AllNodes);
                    Log.Debug(">>> BuildTree: Folding {Count} predicate nodes for Filter {NodeId}", predicateNodeIds.Count, node.NodeId);
                    foreach (var id in predicateNodeIds)
                    {
                        foldedNodeIds.Add(id);
                    }
                }
            }

            // Third pass: identify Physical Plan nodes with LogOp=<comparison> (e.g., Extend_Lookup with LogOp=GreaterThan)
            // These should show the predicate and fold their comparison child nodes
            foreach (var node in plan.AllNodes)
            {
                // Skip if already processed as a Filter node
                if (filterPredicateExpressions.ContainsKey(node.NodeId))
                    continue;

                // Check for LogOp=<comparison> pattern
                var logOpMatch = Regex.Match(node.Operation ?? "", @"LogOp=(\w+)");
                if (logOpMatch.Success)
                {
                    var logOp = logOpMatch.Groups[1].Value;
                    if (ComparisonOperators.Contains(logOp))
                    {
                        Log.Debug(">>> BuildTree: Found Physical comparison node {NodeId} with LogOp={LogOp}", node.NodeId, logOp);

                        // Build predicate expression from this node and its descendants
                        var predicateExpr = BuildPredicateFromPhysicalComparison(node, logOp, plan.AllNodes);
                        if (!string.IsNullOrEmpty(predicateExpr))
                        {
                            filterPredicateExpressions[node.NodeId] = predicateExpr;
                            Log.Debug(">>> BuildTree: Stored physical predicate for node {NodeId}: {Expr}", node.NodeId, predicateExpr);
                        }

                        // Fold comparison child nodes (GreaterThan, ColValue, Constant, etc.)
                        var children = plan.AllNodes.Where(n => n.Parent?.NodeId == node.NodeId).ToList();
                        foreach (var child in children)
                        {
                            var childOpName = GetOperatorNameFromString(child.Operation);
                            // Fold comparison operators and their descendants, but not data sources
                            if (ComparisonOperators.Contains(childOpName) ||
                                childOpName == "Constant" ||
                                childOpName?.StartsWith("ColValue") == true)
                            {
                                foldedNodeIds.Add(child.NodeId);
                                // Also fold descendants of the comparison
                                FoldDescendants(child, plan.AllNodes, foldedNodeIds);
                                Log.Debug(">>> BuildTree: Folding comparison child {ChildId} ({ChildOp})", child.NodeId, childOpName);
                            }
                        }
                    }
                }
            }

            // Fourth pass: Fold spool children into Spool_Iterator and SpoolLookup parents
            var spoolTypeInfos = new Dictionary<int, string>();
            var iterColsPattern = new Regex(@"IterCols\(\d+\)\(('[^']+'\[[^\]]+\])\)", RegexOptions.Compiled);
            var lookupColsPattern = new Regex(@"LookupCols\(\d+\)\(('[^']+'\[[^\]]+\])\)", RegexOptions.Compiled);
            foreach (var node in plan.AllNodes)
            {
                if (foldedNodeIds.Contains(node.NodeId))
                    continue;

                var opName = GetOperatorNameFromString(node.Operation);

                // Match Spool_Iterator, Spool_Iterator<...>, or SpoolLookup
                var isSpoolIterator = opName == "Spool_Iterator" || opName.StartsWith("Spool_Iterator<", StringComparison.Ordinal);
                var isSpoolLookup = opName == "SpoolLookup";

                if (isSpoolIterator || isSpoolLookup)
                {
                    // Extract column from IterCols (for Spool_Iterator) or LookupCols (for SpoolLookup)
                    string columnName = null;
                    if (isSpoolIterator)
                    {
                        var iterColsMatch = iterColsPattern.Match(node.Operation ?? "");
                        columnName = iterColsMatch.Success ? iterColsMatch.Groups[1].Value : null;
                    }
                    else if (isSpoolLookup)
                    {
                        var lookupColsMatch = lookupColsPattern.Match(node.Operation ?? "");
                        columnName = lookupColsMatch.Success ? lookupColsMatch.Groups[1].Value : null;
                    }

                    // Find spool children (AggregationSpool, ProjectionSpool, etc.)
                    var children = plan.AllNodes.Where(n => n.Parent?.NodeId == node.NodeId).ToList();
                    foreach (var child in children)
                    {
                        if (foldedNodeIds.Contains(child.NodeId))
                            continue;

                        var childOp = child.Operation ?? "";
                        var childOpName = GetOperatorNameFromString(childOp);

                        // Check if child is a spool type (contains "Spool<" or ends with "Spool") but not Spool_Iterator/SpoolLookup
                        var isSpoolType = (childOpName.Contains("Spool<") || childOpName.EndsWith("Spool", StringComparison.OrdinalIgnoreCase)) &&
                                          !childOpName.StartsWith("Spool_Iterator", StringComparison.Ordinal) &&
                                          childOpName != "SpoolLookup";
                        if (isSpoolType)
                        {
                            // Map spool type to simplified description
                            var simplifiedType = GetSimplifiedSpoolType(childOpName);

                            // Combine with column name if available
                            var spoolInfo = !string.IsNullOrEmpty(columnName)
                                ? $"{simplifiedType} {columnName}"
                                : simplifiedType;

                            spoolTypeInfos[node.NodeId] = spoolInfo;
                            foldedNodeIds.Add(child.NodeId);
                            Log.Debug(">>> BuildTree: Folding spool child {ChildId} ({SpoolType}) into {ParentOp} {ParentId}, display: {SpoolInfo}",
                                child.NodeId, childOpName, opName, node.NodeId, spoolInfo);
                            break; // Only fold first spool child
                        }
                    }
                }
            }

            // Fifth pass: Extract single-column info for Scan_Vertipaq nodes
            var scanColumnInfos = new Dictionary<int, string>();
            var requiredColsPattern = new Regex(@"RequiredCols\(\d+\)\(('[^']+'\[[^\]]+\])\)", RegexOptions.Compiled);
            foreach (var node in plan.AllNodes)
            {
                if (foldedNodeIds.Contains(node.NodeId))
                    continue;

                var opName = GetOperatorNameFromString(node.Operation);
                if (opName == "Scan_Vertipaq")
                {
                    // Extract column from RequiredCols(N)('Table'[Column])
                    var match = requiredColsPattern.Match(node.Operation ?? "");
                    if (match.Success)
                    {
                        var column = match.Groups[1].Value;
                        scanColumnInfos[node.NodeId] = column;
                        Log.Debug(">>> BuildTree: Extracted scan column {Column} for node {NodeId}", column, node.NodeId);
                    }
                }
            }

            // Sixth pass: Fold identical parent-child nodes
            // When a parent has a single child with an identical operation string, fold the child
            foreach (var node in plan.AllNodes)
            {
                if (foldedNodeIds.Contains(node.NodeId))
                    continue;

                // Find non-folded children
                var children = plan.AllNodes
                    .Where(n => n.Parent?.NodeId == node.NodeId && !foldedNodeIds.Contains(n.NodeId))
                    .ToList();

                // If exactly one child with identical operation, fold it
                if (children.Count == 1)
                {
                    var child = children[0];
                    if (string.Equals(node.Operation, child.Operation, StringComparison.Ordinal))
                    {
                        foldedNodeIds.Add(child.NodeId);
                        Log.Debug(">>> BuildTree: Folding identical child {ChildId} into parent {ParentId}: {Op}",
                            child.NodeId, node.NodeId, GetOperatorNameFromString(node.Operation));
                    }
                }
            }

            // Seventh pass: Infer Cache column from ancestor Spool_Iterator IterCols
            var cacheColumnInfos = new Dictionary<int, string>();
            var ancestorIterColsPattern = new Regex(@"IterCols\(\d+(?:,\s*\d+)*\)\(('[^']+'\[[^\]]+\])", RegexOptions.Compiled);
            foreach (var node in plan.AllNodes)
            {
                if (foldedNodeIds.Contains(node.NodeId))
                    continue;

                var opName = GetOperatorNameFromString(node.Operation);
                if (opName == "Cache")
                {
                    // Walk up the ancestor chain to find Spool_Iterator with IterCols
                    var ancestor = node.Parent;
                    while (ancestor != null)
                    {
                        var ancestorOpName = GetOperatorNameFromString(ancestor.Operation);
                        if (ancestorOpName == "Spool_Iterator" || ancestorOpName.StartsWith("Spool_Iterator<", StringComparison.Ordinal))
                        {
                            // Extract first column from IterCols
                            var match = ancestorIterColsPattern.Match(ancestor.Operation ?? "");
                            if (match.Success)
                            {
                                var column = match.Groups[1].Value;
                                cacheColumnInfos[node.NodeId] = column;
                                Log.Debug(">>> BuildTree: Inferred cache column {Column} for node {NodeId} from ancestor {AncestorId}",
                                    column, node.NodeId, ancestor.NodeId);
                                break;
                            }
                        }
                        ancestor = ancestor.Parent;
                    }
                }
            }

            // Eighth pass: Group nested Spool_Iterator chains with same #Records
            // When a Spool_Iterator has exactly one non-folded child that is also a Spool_Iterator
            // with the same #Records, fold the child into the parent.
            var nestedSpoolDepths = new Dictionary<int, int>();
            var recordsPattern = new Regex(@"#Records=(\d+)", RegexOptions.Compiled);

            // Helper function to find effective children (skipping folded nodes)
            List<EnrichedPlanNode> FindEffectiveChildren(EnrichedPlanNode node, HashSet<int> folded)
            {
                var result = new List<EnrichedPlanNode>();
                var directChildren = plan.AllNodes.Where(n => n.Parent?.NodeId == node.NodeId).ToList();

                foreach (var child in directChildren)
                {
                    if (!folded.Contains(child.NodeId))
                    {
                        result.Add(child);
                    }
                    else
                    {
                        // This child is folded, so its children become our effective children
                        result.AddRange(FindEffectiveChildren(child, folded));
                    }
                }
                return result;
            }

            bool madeProgress;
            do
            {
                madeProgress = false;
                foreach (var node in plan.AllNodes)
                {
                    if (foldedNodeIds.Contains(node.NodeId))
                        continue;

                    var opName = GetOperatorNameFromString(node.Operation);
                    var isSpoolIterator = opName == "Spool_Iterator" || opName.StartsWith("Spool_Iterator<", StringComparison.Ordinal);

                    if (!isSpoolIterator)
                        continue;

                    // Extract #Records from this node
                    var recordsMatch = recordsPattern.Match(node.Operation ?? "");
                    if (!recordsMatch.Success)
                        continue;
                    var nodeRecords = recordsMatch.Groups[1].Value;

                    // Find effective non-folded children (skipping already folded nodes)
                    var nonFoldedChildren = FindEffectiveChildren(node, foldedNodeIds);

                    // Check if exactly one child and it's a Spool_Iterator with same #Records
                    if (nonFoldedChildren.Count == 1)
                    {
                        var child = nonFoldedChildren[0];
                        var childOpName = GetOperatorNameFromString(child.Operation);
                        var isChildSpoolIterator = childOpName == "Spool_Iterator" || childOpName.StartsWith("Spool_Iterator<", StringComparison.Ordinal);

                        if (isChildSpoolIterator)
                        {
                            var childRecordsMatch = recordsPattern.Match(child.Operation ?? "");
                            if (childRecordsMatch.Success && childRecordsMatch.Groups[1].Value == nodeRecords)
                            {
                                // Same #Records - fold child into parent
                                foldedNodeIds.Add(child.NodeId);

                                // Track the depth - inherit child's depth + 1
                                var childDepth = nestedSpoolDepths.TryGetValue(child.NodeId, out var cd) ? cd : 1;
                                var parentDepth = nestedSpoolDepths.TryGetValue(node.NodeId, out var pd) ? pd : 1;
                                nestedSpoolDepths[node.NodeId] = parentDepth + childDepth;

                                // If child had spool type info, preserve it on parent
                                if (spoolTypeInfos.TryGetValue(child.NodeId, out var childSpoolInfo) &&
                                    !spoolTypeInfos.ContainsKey(node.NodeId))
                                {
                                    spoolTypeInfos[node.NodeId] = childSpoolInfo;
                                }

                                Log.Debug(">>> BuildTree: Folded nested Spool_Iterator {ChildId} into {ParentId}, both have #Records={Records}, depth now {Depth}",
                                    child.NodeId, node.NodeId, nodeRecords, nestedSpoolDepths[node.NodeId]);

                                madeProgress = true;
                            }
                        }
                    }
                }
            } while (madeProgress); // Keep folding until no more progress

            // Ninth pass: Fold Variant->* type coercion nodes down into their children
            // These are purely type conversion wrappers that add noise - fold them away
            // and let their child take their place in the tree
            var typeCoercionInfos = new Dictionary<int, string>();
            foreach (var node in plan.AllNodes)
            {
                if (foldedNodeIds.Contains(node.NodeId))
                    continue;

                var opName = GetOperatorNameFromString(node.Operation);
                if (opName != null && opName.StartsWith("Variant->", StringComparison.OrdinalIgnoreCase))
                {
                    // Find non-folded children
                    var nonFoldedChildren = plan.AllNodes
                        .Where(n => n.Parent?.NodeId == node.NodeId && !foldedNodeIds.Contains(n.NodeId))
                        .ToList();

                    // If exactly one child, fold the Variant node
                    if (nonFoldedChildren.Count == 1)
                    {
                        var child = nonFoldedChildren[0];
                        foldedNodeIds.Add(node.NodeId);

                        // Store the type coercion info on the child so it can be displayed
                        typeCoercionInfos[child.NodeId] = opName;

                        Log.Debug(">>> BuildTree: Folded Variant node {VariantId} ({OpName}) down into child {ChildId}",
                            node.NodeId, opName, child.NodeId);
                    }
                }
            }

            // Tenth pass: SpoolLookup → Spool_Iterator folding with row range
            // When SpoolLookup (1 row) has a Spool_Iterator child (11 rows), fold and show "1-11 rows"
            var rowRanges = new Dictionary<int, (long min, long max)>();
            foreach (var node in plan.AllNodes)
            {
                if (foldedNodeIds.Contains(node.NodeId))
                    continue;

                var opName = GetOperatorNameFromString(node.Operation);
                if (opName != "SpoolLookup")
                    continue;

                // Extract #Records from SpoolLookup
                var lookupRecordsMatch = recordsPattern.Match(node.Operation ?? "");
                if (!lookupRecordsMatch.Success || !long.TryParse(lookupRecordsMatch.Groups[1].Value, out var lookupRecords))
                    continue;

                // Find Spool_Iterator children
                var nonFoldedChildren = FindEffectiveChildren(node, foldedNodeIds);
                if (nonFoldedChildren.Count != 1)
                    continue;

                var child = nonFoldedChildren[0];
                var childOpName = GetOperatorNameFromString(child.Operation);
                var isChildSpoolIterator = childOpName == "Spool_Iterator" || childOpName.StartsWith("Spool_Iterator<", StringComparison.Ordinal);

                if (!isChildSpoolIterator)
                    continue;

                // Extract #Records from Spool_Iterator
                var childRecordsMatch = recordsPattern.Match(child.Operation ?? "");
                if (!childRecordsMatch.Success || !long.TryParse(childRecordsMatch.Groups[1].Value, out var childRecords))
                    continue;

                // Fold the Spool_Iterator into SpoolLookup
                foldedNodeIds.Add(child.NodeId);

                // Track the row range (min, max)
                var minRecords = Math.Min(lookupRecords, childRecords);
                var maxRecords = Math.Max(lookupRecords, childRecords);
                rowRanges[node.NodeId] = (minRecords, maxRecords);

                // If child had spool type info, preserve it on parent
                if (spoolTypeInfos.TryGetValue(child.NodeId, out var childSpoolInfo) &&
                    !spoolTypeInfos.ContainsKey(node.NodeId))
                {
                    spoolTypeInfos[node.NodeId] = childSpoolInfo;
                }

                Log.Debug(">>> BuildTree: Folded Spool_Iterator {ChildId} into SpoolLookup {ParentId}, row range {Min}-{Max}",
                    child.NodeId, node.NodeId, minRecords, maxRecords);
            }

            // Create ViewModels for non-folded nodes only
            Log.Debug(">>> BuildTree: filterPredicateExpressions has {Count} entries", filterPredicateExpressions.Count);
            foreach (var node in plan.AllNodes)
            {
                if (!foldedNodeIds.Contains(node.NodeId))
                {
                    var vm = new PlanNodeViewModel(node);

                    // Set filter predicate expression if available
                    if (filterPredicateExpressions.TryGetValue(node.NodeId, out var predicateExpr))
                    {
                        vm.FilterPredicateExpression = predicateExpr;
                        Log.Debug(">>> BuildTree: Set predicate on VM {NodeId}: {Expr}, HasFilterPredicate={Has}",
                            node.NodeId, predicateExpr, vm.HasFilterPredicate);
                    }

                    // Set spool type info if available
                    if (spoolTypeInfos.TryGetValue(node.NodeId, out var spoolType))
                    {
                        vm.SpoolTypeInfo = spoolType;
                        Log.Debug(">>> BuildTree: Set spool type on VM {NodeId}: {Type}",
                            node.NodeId, spoolType);
                    }

                    // Set scan column info if available
                    if (scanColumnInfos.TryGetValue(node.NodeId, out var scanColumn))
                    {
                        vm.ScanColumnInfo = scanColumn;
                        Log.Debug(">>> BuildTree: Set scan column on VM {NodeId}: {Column}",
                            node.NodeId, scanColumn);
                    }

                    // Set cache column info if available
                    if (cacheColumnInfos.TryGetValue(node.NodeId, out var cacheColumn))
                    {
                        vm.CacheColumnInfo = cacheColumn;
                        Log.Debug(">>> BuildTree: Set cache column on VM {NodeId}: {Column}",
                            node.NodeId, cacheColumn);
                    }

                    // Set nested spool depth if available
                    if (nestedSpoolDepths.TryGetValue(node.NodeId, out var nestedDepth))
                    {
                        vm.NestedSpoolDepth = nestedDepth;
                        Log.Debug(">>> BuildTree: Set nested spool depth on VM {NodeId}: {Depth}",
                            node.NodeId, nestedDepth);
                    }

                    // Set type coercion info if a Variant node was folded into this node
                    if (typeCoercionInfos.TryGetValue(node.NodeId, out var typeCoercion))
                    {
                        vm.TypeCoercionInfo = typeCoercion;
                        Log.Debug(">>> BuildTree: Set type coercion on VM {NodeId}: {Type}",
                            node.NodeId, typeCoercion);
                    }

                    // Set row range if SpoolLookup was folded with Spool_Iterator
                    if (rowRanges.TryGetValue(node.NodeId, out var rowRange))
                    {
                        vm.SpoolRowRangeMin = rowRange.min;
                        vm.SpoolRowRangeMax = rowRange.max;
                        Log.Debug(">>> BuildTree: Set row range on VM {NodeId}: {Min}-{Max}",
                            node.NodeId, rowRange.min, rowRange.max);
                    }

                    nodeMap[node.NodeId] = vm;
                }
            }

            // Build parent-child relationships (skip folded nodes)
            foreach (var node in plan.AllNodes)
            {
                if (foldedNodeIds.Contains(node.NodeId))
                    continue;

                var vm = nodeMap[node.NodeId];

                // Find the nearest non-folded ancestor
                var parent = node.Parent;
                while (parent != null && foldedNodeIds.Contains(parent.NodeId))
                {
                    parent = parent.Parent;
                }

                if (parent != null && nodeMap.TryGetValue(parent.NodeId, out var parentVm))
                {
                    vm.Parent = parentVm;
                    parentVm.Children.Add(vm);
                }
            }

            // Get the root node and set execution metrics
            if (nodeMap.TryGetValue(plan.RootNode.NodeId, out var root))
            {
                root.IsRootNode = true;
                root.PlanTotalDurationMs = plan.TotalDurationMs;
                root.PlanStorageEngineDurationMs = plan.StorageEngineDurationMs;
                root.PlanFormulaEngineDurationMs = plan.FormulaEngineDurationMs;
                root.PlanStorageEngineCpuMs = plan.StorageEngineCpuMs;
                root.PlanStorageEngineQueryCount = plan.StorageEngineQueryCount;
                root.PlanCacheHits = plan.CacheHits;

                Log.Debug(">>> BuildTree: Root node {NodeId} - IsRootNode={IsRoot}, TotalDurationMs={Total}, SEDuration={SE}, FEDuration={FE}, HasExecutionMetrics={HasMetrics}",
                    root.NodeId, root.IsRootNode, root.PlanTotalDurationMs, root.PlanStorageEngineDurationMs,
                    root.PlanFormulaEngineDurationMs, root.HasExecutionMetrics);

                return root;
            }
            return null;
        }

        /// <summary>
        /// Determines if a node should be folded into its parent.
        /// Column reference nodes (like 'Table'[Column]: ScaLogOp...) are folded
        /// because they just provide column context that's already visible in the parent.
        /// </summary>
        private static bool ShouldFoldNode(EnrichedPlanNode node)
        {
            var op = node.Operation;
            if (string.IsNullOrWhiteSpace(op))
                return false;

            // Column reference nodes start with 'Table'[Column] pattern
            // These provide column context but aren't meaningful operations
            if (op.StartsWith("'"))
            {
                // Check if the operator after the colon is just a type indicator (ScaLogOp, RelLogOp)
                // not a real operator like Scan_Vertipaq
                int bracketDepth = 0;
                bool inQuote = false;

                for (int i = 0; i < op.Length; i++)
                {
                    char c = op[i];
                    if (c == '\'' && bracketDepth == 0)
                        inQuote = !inQuote;
                    else if (!inQuote)
                    {
                        if (c == '[') bracketDepth++;
                        else if (c == ']') bracketDepth--;
                        else if (c == ':' && bracketDepth == 0)
                        {
                            // Extract operator name after the colon
                            var afterColon = op.Substring(i + 1).TrimStart();
                            var spaceIndex = afterColon.IndexOf(' ');
                            var operatorName = spaceIndex > 0 ? afterColon.Substring(0, spaceIndex) : afterColon;

                            // ScaLogOp and RelLogOp are type indicators, not real operators
                            // Fold these column reference nodes
                            if (operatorName == "ScaLogOp" || operatorName == "RelLogOp")
                                return true;

                            // If there's a real operator (known in dictionary), don't fold
                            if (DaxOperatorDictionary.GetOperatorInfo(operatorName) != null)
                                return false;

                            // Unknown operator after column reference - fold it
                            return true;
                        }
                    }
                }

                // Column reference with no colon - fold it
                return true;
            }

            return false;
        }

        #region Filter Predicate Rollup

        /// <summary>
        /// Filter operator names that can have predicates rolled up.
        /// </summary>
        private static readonly HashSet<string> FilterOperators = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Filter", "Filter_Vertipaq", "DataPostFilter"
        };

        /// <summary>
        /// Determines if a node is a Filter that can have predicates rolled up.
        /// </summary>
        public static bool IsFilterOperator(string operatorName)
        {
            return FilterOperators.Contains(operatorName);
        }

        /// <summary>
        /// Determines if a node is a predicate child that should be rolled into a Filter.
        /// Does NOT include SE nodes - we never fold across engine transitions.
        /// </summary>
        private static bool IsFilterPredicateNode(EnrichedPlanNode node, EnrichedPlanNode filterParent)
        {
            if (node == null || filterParent == null)
                return false;

            var opName = GetOperatorNameFromString(node.Operation);

            // Never fold SE nodes into FE Filter (engine transition)
            if (node.EngineType == EngineType.StorageEngine && filterParent.EngineType != EngineType.StorageEngine)
                return false;

            // Scan nodes are data sources, not predicates - never fold
            if (opName.StartsWith("Scan", StringComparison.OrdinalIgnoreCase))
                return false;

            // Comparison operators are predicates
            if (ComparisonOperators.Contains(opName))
                return true;

            // Value operators (Constant, ColValue) under a Filter's predicate should be folded
            if (ValueOperators.Contains(opName))
                return true;

            // Column references that are part of predicates
            if (node.Operation?.StartsWith("'") == true)
                return true;

            // Already-condensed predicates like 'Product'[Color] <> Black
            // These contain comparison symbols in the operation
            if (node.Operation != null && ContainsComparisonOperator(node.Operation))
                return true;

            return false;
        }

        /// <summary>
        /// Checks if an operation string contains a comparison operator.
        /// Used to detect already-condensed predicates like 'Product'[Color] <> Black
        /// </summary>
        private static bool ContainsComparisonOperator(string operation)
        {
            if (string.IsNullOrEmpty(operation))
                return false;

            // Look for comparison operators in the string
            return operation.Contains(" <> ") ||
                   operation.Contains(" > ") ||
                   operation.Contains(" < ") ||
                   operation.Contains(" = ") ||
                   operation.Contains(" >= ") ||
                   operation.Contains(" <= ");
        }

        /// <summary>
        /// Maps a spool type to a simplified human-readable description.
        /// </summary>
        private static string GetSimplifiedSpoolType(string spoolType)
        {
            if (string.IsNullOrEmpty(spoolType))
                return "Spool";

            // Extract the base spool type and generic parameter
            // e.g., "AggregationSpool<GroupBy>" -> base="AggregationSpool", param="GroupBy"
            // e.g., "ProjectionSpool<ProjectFusion<>>" -> base="ProjectionSpool", param="ProjectFusion<>"
            var baseType = spoolType;
            var param = "";

            var ltIdx = spoolType.IndexOf('<');
            if (ltIdx > 0)
            {
                baseType = spoolType.Substring(0, ltIdx);
                // Extract parameter, handling nested brackets
                var rest = spoolType.Substring(ltIdx + 1);
                if (rest.EndsWith(">"))
                    param = rest.Substring(0, rest.Length - 1);
            }

            // Map to simplified descriptions
            return baseType switch
            {
                "AggregationSpool" => param switch
                {
                    "GroupBy" => "Group by",
                    "Sum" => "Sum",
                    "Count" => "Count",
                    "Min" => "Min",
                    "Max" => "Max",
                    "Avg" => "Average",
                    _ => string.IsNullOrEmpty(param) ? "Aggregate" : $"Aggregate ({param})"
                },
                "ProjectionSpool" => "Project",
                "HashSpool" => "Hash",
                "SortSpool" => "Sort",
                "UnionSpool" => "Union",
                "CacheSpool" => "Cache",
                _ => baseType.Replace("Spool", "").Trim()
            };
        }

        /// <summary>
        /// Extracts the operator name from an operation string.
        /// </summary>
        private static string GetOperatorNameFromString(string operation)
        {
            if (string.IsNullOrWhiteSpace(operation))
                return string.Empty;

            // Handle 'Table'[Column]: Operator format
            if (operation.StartsWith("'"))
            {
                int colonIdx = FindColonOutsideBrackets(operation);
                if (colonIdx > 0)
                {
                    var afterColon = operation.Substring(colonIdx + 1).TrimStart();
                    var spaceIdx = afterColon.IndexOf(' ');
                    return spaceIdx > 0 ? afterColon.Substring(0, spaceIdx) : afterColon;
                }
                // No colon - might be an already-condensed predicate
                return operation;
            }

            // Standard format: Operator: ...
            var colonIndex = operation.IndexOf(':');
            if (colonIndex > 0)
                return operation.Substring(0, colonIndex).Trim();

            var spaceIndex = operation.IndexOf(' ');
            if (spaceIndex > 0)
                return operation.Substring(0, spaceIndex);

            return operation;
        }

        /// <summary>
        /// Finds the colon position outside of brackets and quotes.
        /// </summary>
        private static int FindColonOutsideBrackets(string s)
        {
            int bracketDepth = 0;
            bool inQuote = false;

            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\'' && bracketDepth == 0)
                    inQuote = !inQuote;
                else if (!inQuote)
                {
                    if (c == '[') bracketDepth++;
                    else if (c == ']') bracketDepth--;
                    else if (c == ':' && bracketDepth == 0)
                        return i;
                }
            }
            return -1;
        }

        /// <summary>
        /// Builds a filter predicate expression from a Filter node's predicate children.
        /// </summary>
        private static string BuildFilterPredicateExpression(EnrichedPlanNode filterNode, IEnumerable<EnrichedPlanNode> allNodes)
        {
            if (filterNode == null)
                return null;

            Log.Debug(">>> BuildFilterPredicateExpression: Filter node {NodeId}: {Operation}", filterNode.NodeId, filterNode.Operation);

            // Find direct children of the filter that are predicates (not Scan nodes)
            var children = allNodes.Where(n => n.Parent?.NodeId == filterNode.NodeId).ToList();
            Log.Debug(">>> BuildFilterPredicateExpression: Found {Count} children", children.Count);

            foreach (var child in children)
            {
                var childOpName = GetOperatorNameFromString(child.Operation);
                Log.Debug(">>> BuildFilterPredicateExpression: Child {NodeId} opName={OpName}, Operation={Operation}", child.NodeId, childOpName, child.Operation);

                // Skip Scan nodes - they're data sources, not predicates
                if (childOpName.StartsWith("Scan", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Debug(">>> BuildFilterPredicateExpression: Skipping Scan node");
                    continue;
                }

                // Check for already-condensed predicate like 'Product'[Color] <> Black
                if (child.Operation?.StartsWith("'") == true && ContainsComparisonOperator(child.Operation))
                {
                    var condensed = ExtractCondensedPredicate(child.Operation);
                    Log.Debug(">>> BuildFilterPredicateExpression: Found condensed predicate: {Predicate}", condensed);
                    return condensed;
                }

                // Check for comparison operator (direct, e.g., "GreaterThan: ScaLogOp...")
                if (ComparisonOperators.Contains(childOpName))
                {
                    var predicate = BuildPredicateFromComparison(child, allNodes);
                    Log.Debug(">>> BuildFilterPredicateExpression: Built predicate from comparison: {Predicate}", predicate);
                    return predicate;
                }

                // Check for LogOp=<comparison> pattern in Physical Plan (e.g., "Extend_Lookup: IterPhyOp LogOp=GreaterThan...")
                var logOpMatch = Regex.Match(child.Operation ?? "", @"LogOp=(\w+)");
                if (logOpMatch.Success)
                {
                    var logOp = logOpMatch.Groups[1].Value;
                    Log.Debug(">>> BuildFilterPredicateExpression: Found LogOp={LogOp}", logOp);
                    if (ComparisonOperators.Contains(logOp))
                    {
                        var predicate = BuildPredicateFromPhysicalComparison(child, logOp, allNodes);
                        Log.Debug(">>> BuildFilterPredicateExpression: Built predicate from physical comparison: {Predicate}", predicate);
                        return predicate;
                    }
                }
            }

            Log.Debug(">>> BuildFilterPredicateExpression: No predicate found");
            return null;
        }

        /// <summary>
        /// Extracts a predicate from an already-condensed operation string.
        /// E.g., "'Product'[Color] <> Black: ScaLogOp..." → "[Color] <> Black"
        /// </summary>
        private static string ExtractCondensedPredicate(string operation)
        {
            if (string.IsNullOrEmpty(operation))
                return null;

            // Extract the part before the colon
            var colonIdx = FindColonOutsideBrackets(operation);
            var predicatePart = colonIdx > 0 ? operation.Substring(0, colonIdx).Trim() : operation;

            // Try to simplify 'Table'[Column] to just [Column]
            var match = Regex.Match(predicatePart, @"'[^']+'\[([^\]]+)\]\s*((?:<>|>=|<=|>|<|=))\s*(.+)");
            if (match.Success)
            {
                var column = match.Groups[1].Value;
                var op = match.Groups[2].Value;
                var value = match.Groups[3].Value.Trim();

                // Quote string values if they're not already quoted and not numeric
                if (!IsNumeric(value) && !value.StartsWith("\"") && !value.StartsWith("'"))
                    value = $"\"{value}\"";

                return $"[{column}] {op} {value}";
            }

            return predicatePart;
        }

        /// <summary>
        /// Builds a predicate expression from a comparison node and its children.
        /// </summary>
        private static string BuildPredicateFromComparison(EnrichedPlanNode comparisonNode, IEnumerable<EnrichedPlanNode> allNodes)
        {
            var children = allNodes.Where(n => n.Parent?.NodeId == comparisonNode.NodeId).ToList();
            Log.Debug(">>> BuildPredicateFromComparison: Comparison node {NodeId} has {Count} children", comparisonNode.NodeId, children.Count);

            if (children.Count < 2)
            {
                Log.Debug(">>> BuildPredicateFromComparison: Not enough children (need 2)");
                return null;
            }

            var compOpName = GetOperatorNameFromString(comparisonNode.Operation);
            var symbol = GetComparisonSymbolStatic(compOpName);
            Log.Debug(">>> BuildPredicateFromComparison: operator={Op}, symbol={Symbol}", compOpName, symbol);

            string leftValue = null;
            string rightValue = null;

            foreach (var child in children)
            {
                var value = ExtractValueFromNode(child);
                Log.Debug(">>> BuildPredicateFromComparison: Child {NodeId} extracted value: {Value}", child.NodeId, value);
                if (leftValue == null)
                    leftValue = value;
                else
                    rightValue = value;
            }

            if (leftValue != null && rightValue != null)
            {
                var result = $"{leftValue} {symbol} {rightValue}";
                Log.Debug(">>> BuildPredicateFromComparison: Result: {Result}", result);
                return result;
            }

            Log.Debug(">>> BuildPredicateFromComparison: Could not build predicate (left={Left}, right={Right})", leftValue, rightValue);
            return null;
        }

        /// <summary>
        /// Builds a predicate expression from a physical plan comparison node.
        /// Physical plan uses operators like Extend_Lookup with LogOp=GreaterThan embedded.
        /// Column is in IterCols(...) and values may be in child nodes.
        /// </summary>
        private static string BuildPredicateFromPhysicalComparison(EnrichedPlanNode physicalNode, string logOp, IEnumerable<EnrichedPlanNode> allNodes)
        {
            var op = physicalNode.Operation ?? "";
            var symbol = GetComparisonSymbolStatic(logOp);

            // Extract column from IterCols(...) pattern: IterCols(0)('Table'[Column])
            // Pattern: IterCols(digit)('TableName'[ColumnName])
            var iterColsMatch = Regex.Match(op, @"IterCols\(\d+\)\('[^']+'\[([^\]]+)\]\)");
            string column = null;
            if (iterColsMatch.Success)
            {
                column = $"[{iterColsMatch.Groups[1].Value}]";
                Log.Debug(">>> BuildPredicateFromPhysicalComparison: Matched IterCols pattern, column={Column}", column);
            }
            else
            {
                // Fallback: look for any column reference in the operation string
                var colMatch = Regex.Match(op, @"'[^']+'\[([^\]]+)\]");
                if (colMatch.Success)
                {
                    column = $"[{colMatch.Groups[1].Value}]";
                    Log.Debug(">>> BuildPredicateFromPhysicalComparison: Fallback column match, column={Column}", column);
                }
            }

            Log.Debug(">>> BuildPredicateFromPhysicalComparison: Extracted column={Column}", column);

            // Look for constant value in child nodes AND grandchildren (Physical Plans have deeper nesting)
            var children = allNodes.Where(n => n.Parent?.NodeId == physicalNode.NodeId).ToList();
            Log.Debug(">>> BuildPredicateFromPhysicalComparison: Found {Count} direct children", children.Count);

            string constantValue = null;

            // First check direct children
            foreach (var child in children)
            {
                constantValue = ExtractConstantFromNodeOrDescendants(child, allNodes, 0);
                if (constantValue != null)
                    break;
            }

            if (column != null && constantValue != null)
            {
                var result = $"{column} {symbol} {constantValue}";
                Log.Debug(">>> BuildPredicateFromPhysicalComparison: Result: {Result}", result);
                return result;
            }

            // If no constant found in children, try to extract from the operation string itself
            if (column != null)
            {
                // Some physical plans might have the value embedded differently
                // Return partial result if we at least have the column and operator
                Log.Debug(">>> BuildPredicateFromPhysicalComparison: No constant found, returning null");
            }

            return null;
        }

        /// <summary>
        /// Recursively searches for a constant value in a node and its descendants.
        /// Physical Plans often have the constant nested several levels deep.
        /// </summary>
        private static string ExtractConstantFromNodeOrDescendants(EnrichedPlanNode node, IEnumerable<EnrichedPlanNode> allNodes, int depth)
        {
            if (depth > 3) return null; // Limit recursion depth

            var op = node.Operation ?? "";

            // Check for "Constant: ... String Bob" pattern (Physical Plan format)
            if (op.StartsWith("Constant", StringComparison.OrdinalIgnoreCase))
            {
                // Physical plan format: "Constant: LookupPhyOp LogOp=Constant String Bob"
                var stringMatch = Regex.Match(op, @"String\s+(\S+)");
                if (stringMatch.Success)
                {
                    var value = stringMatch.Groups[1].Value;
                    Log.Debug(">>> ExtractConstantFromNodeOrDescendants: Found String constant: {Value}", value);
                    return IsNumeric(value) ? value : $"\"{value}\"";
                }

                // Try DominantValue pattern
                var domMatch = Regex.Match(op, @"DominantValue=(\S+)");
                if (domMatch.Success && domMatch.Groups[1].Value != "NONE")
                {
                    var value = domMatch.Groups[1].Value;
                    Log.Debug(">>> ExtractConstantFromNodeOrDescendants: Found DominantValue constant: {Value}", value);
                    return IsNumeric(value) ? value : $"\"{value}\"";
                }

                // Try to extract numeric value
                var numMatch = Regex.Match(op, @"(?:Integer|Currency|Double|Decimal)\s+(\S+)");
                if (numMatch.Success)
                {
                    Log.Debug(">>> ExtractConstantFromNodeOrDescendants: Found numeric constant: {Value}", numMatch.Groups[1].Value);
                    return numMatch.Groups[1].Value;
                }
            }

            // Check DominantValue on any node
            var constMatch = Regex.Match(op, @"DominantValue=(\S+)");
            if (constMatch.Success)
            {
                var value = constMatch.Groups[1].Value;
                if (value != "NONE" && value != "BLANK")
                {
                    Log.Debug(">>> ExtractConstantFromNodeOrDescendants: Found DominantValue: {Value}", value);
                    return IsNumeric(value) ? value : $"\"{value}\"";
                }
            }

            // Recursively check children
            var children = allNodes.Where(n => n.Parent?.NodeId == node.NodeId).ToList();
            foreach (var child in children)
            {
                var result = ExtractConstantFromNodeOrDescendants(child, allNodes, depth + 1);
                if (result != null)
                    return result;
            }

            return null;
        }

        /// <summary>
        /// Extracts a display value from a node (column reference, constant, etc).
        /// </summary>
        private static string ExtractValueFromNode(EnrichedPlanNode node)
        {
            var op = node.Operation ?? "";

            // Column reference: 'Table'[Column]
            var colMatch = Regex.Match(op, @"'[^']+'\[([^\]]+)\]");
            if (colMatch.Success)
                return $"[{colMatch.Groups[1].Value}]";

            // Simple [Column]
            var simpleColMatch = Regex.Match(op, @"\[([^\]]+)\]");
            if (simpleColMatch.Success)
                return $"[{simpleColMatch.Groups[1].Value}]";

            // Constant with DominantValue
            var constMatch = Regex.Match(op, @"DominantValue=(\S+)");
            if (constMatch.Success)
            {
                var value = constMatch.Groups[1].Value;
                // Quote string values
                if (!IsNumeric(value) && value != "NONE" && value != "BLANK" && value != "true" && value != "false")
                    return $"\"{value}\"";
                return value;
            }

            // Fallback to operator name
            return GetOperatorNameFromString(op);
        }

        /// <summary>
        /// Gets the comparison symbol for an operator name.
        /// </summary>
        private static string GetComparisonSymbolStatic(string operatorName)
        {
            return operatorName switch
            {
                "GreaterThan" => ">",
                "LessThan" => "<",
                "GreaterOrEqualTo" => ">=",
                "LessOrEqualTo" => "<=",
                "Equal" => "=",
                "NotEqual" => "<>",
                _ => "?"
            };
        }

        /// <summary>
        /// Checks if a string value is numeric.
        /// </summary>
        private static bool IsNumeric(string value)
        {
            return double.TryParse(value, out _);
        }

        /// <summary>
        /// Collects all nodes in the predicate subtree of a Filter (excluding Scan nodes).
        /// </summary>
        private static HashSet<int> CollectFilterPredicateNodeIds(EnrichedPlanNode filterNode, IEnumerable<EnrichedPlanNode> allNodes)
        {
            var predicateNodeIds = new HashSet<int>();
            var nodeList = allNodes.ToList();

            // Find direct children that are predicates (not Scan nodes)
            var directChildren = nodeList.Where(n => n.Parent?.NodeId == filterNode.NodeId).ToList();

            foreach (var child in directChildren)
            {
                if (IsFilterPredicateNode(child, filterNode))
                {
                    CollectPredicateSubtree(child, nodeList, predicateNodeIds, filterNode);
                }
            }

            return predicateNodeIds;
        }

        /// <summary>
        /// Recursively collects all nodes in a predicate subtree.
        /// </summary>
        private static void CollectPredicateSubtree(EnrichedPlanNode node, List<EnrichedPlanNode> allNodes, HashSet<int> collected, EnrichedPlanNode filterParent)
        {
            if (node == null || collected.Contains(node.NodeId))
                return;

            // Check engine transition - never fold SE nodes into FE filter
            if (node.EngineType == EngineType.StorageEngine && filterParent.EngineType != EngineType.StorageEngine)
                return;

            collected.Add(node.NodeId);

            // Collect children
            var children = allNodes.Where(n => n.Parent?.NodeId == node.NodeId);
            foreach (var child in children)
            {
                CollectPredicateSubtree(child, allNodes, collected, filterParent);
            }
        }

        /// <summary>
        /// Recursively folds all descendants of a node (adds them to the foldedNodeIds set).
        /// Used to fold comparison subtrees in physical plan nodes with LogOp=comparison.
        /// </summary>
        private static void FoldDescendants(EnrichedPlanNode node, IEnumerable<EnrichedPlanNode> allNodes, HashSet<int> foldedNodeIds)
        {
            if (node == null)
                return;

            var children = allNodes.Where(n => n.Parent?.NodeId == node.NodeId);
            foreach (var child in children)
            {
                foldedNodeIds.Add(child.NodeId);
                Log.Debug(">>> FoldDescendants: Folding descendant {ChildId}", child.NodeId);
                FoldDescendants(child, allNodes, foldedNodeIds);
            }
        }

        #endregion
    }
}
