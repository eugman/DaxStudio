using System;
using System.Collections.Generic;
using System.Linq;
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
        /// </summary>
        public string DisplayText
        {
            get
            {
                var text = GetOperationName();
                return text.Length > 30 ? text.Substring(0, 27) + "..." : text;
            }
        }

        /// <summary>
        /// Full tooltip text showing complete operation details.
        /// </summary>
        public string TooltipText
        {
            get
            {
                var lines = new List<string>
                {
                    $"Operation: {GetOperationName()}",
                    $"Row: {_node.RowNumber}",
                    $"Level: {_node.Level}"
                };

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
                        lines.Add($"  • {issue.IssueType}: {issue.Description}");
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
        /// Width of the node for rendering.
        /// </summary>
        public double Width { get; set; } = 180;

        /// <summary>
        /// Height of the node for rendering.
        /// </summary>
        public double Height { get; set; } = 60;

        /// <summary>
        /// Parent node in the tree.
        /// </summary>
        public PlanNodeViewModel Parent { get; set; }

        /// <summary>
        /// Child nodes.
        /// </summary>
        public BindableCollection<PlanNodeViewModel> Children { get; }

        /// <summary>
        /// Background color based on cost percentage.
        /// </summary>
        public Brush BackgroundBrush
        {
            get
            {
                if (HasIssues)
                {
                    // Orange/Red for nodes with issues
                    var severity = Issues.Max(i => i.Severity);
                    return severity == IssueSeverity.Error
                        ? new SolidColorBrush(Color.FromRgb(255, 200, 200))  // Light red
                        : new SolidColorBrush(Color.FromRgb(255, 235, 200)); // Light orange
                }

                if (!CostPercentage.HasValue || CostPercentage.Value < 1)
                {
                    return new SolidColorBrush(Color.FromRgb(240, 240, 240)); // Light gray
                }

                // Gradient from green (low cost) to red (high cost)
                var cost = Math.Min(CostPercentage.Value, 100);
                byte r, g;

                if (cost < 50)
                {
                    // Green to Yellow
                    r = (byte)(cost * 5.1);
                    g = 200;
                }
                else
                {
                    // Yellow to Red
                    r = 255;
                    g = (byte)(200 - (cost - 50) * 4);
                }

                return new SolidColorBrush(Color.FromRgb(r, g, 180));
            }
        }

        /// <summary>
        /// Border color indicating selection or engine type.
        /// </summary>
        public Brush BorderBrush
        {
            get
            {
                if (IsSelected)
                {
                    return new SolidColorBrush(Color.FromRgb(0, 120, 215)); // Blue selection
                }

                return EngineType switch
                {
                    EngineType.StorageEngine => new SolidColorBrush(Color.FromRgb(0, 128, 0)),   // Green
                    EngineType.FormulaEngine => new SolidColorBrush(Color.FromRgb(128, 0, 128)), // Purple
                    _ => new SolidColorBrush(Color.FromRgb(128, 128, 128))                        // Gray
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
        /// </summary>
        private string GetOperationName()
        {
            var op = ResolvedOperation ?? Operation ?? string.Empty;

            // Extract just the operation name before any parameters
            var spaceIndex = op.IndexOf(' ');
            if (spaceIndex > 0)
            {
                return op.Substring(0, spaceIndex);
            }

            return op;
        }

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
