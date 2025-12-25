using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Caliburn.Micro;
using DaxStudio.Common;
using DaxStudio.Common.Enums;
using DaxStudio.Interfaces;
using DaxStudio.QueryTrace;
using DaxStudio.UI.Events;
using DaxStudio.UI.Extensions;
using DaxStudio.UI.Interfaces;
using DaxStudio.UI.Model;
using DaxStudio.UI.Services;
using DaxStudio.UI.Utils;
using Newtonsoft.Json;
using Serilog;

namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the Visual Query Plan trace window.
    /// Displays DAX query plans as an interactive graph visualization.
    /// </summary>
    public class VisualQueryPlanViewModel : TraceWatcherBaseViewModel,
        ISaveState,
        ITraceDiagnostics,
        IHaveData
    {
        private readonly IPlanEnrichmentService _enrichmentService;
        private EnrichedQueryPlan _physicalPlan;
        private EnrichedQueryPlan _logicalPlan;
        private PlanNodeViewModel _rootNode;
        private PlanNodeViewModel _selectedNode;
        private string _physicalQueryPlanText;
        private string _logicalQueryPlanText;
        private bool _showPhysicalPlan = true;
        private double _zoomLevel = 1.0;
        private int _selectedTabIndex = 0;

        /// <summary>
        /// Event raised after the plan layout is updated, allowing the view to adjust scroll position.
        /// </summary>
        public event System.EventHandler PlanLayoutUpdated;

        /// <summary>
        /// Minimum canvas size to ensure scrollbars are available for panning.
        /// </summary>
        private const double MinCanvasSize = 800;

        private double _actualContentWidth = MinCanvasSize;
        private double _actualContentHeight = MinCanvasSize;

        /// <summary>
        /// Actual content width based on positioned nodes (with minimum for panning).
        /// </summary>
        public double ActualContentWidth
        {
            get => _actualContentWidth;
            private set
            {
                var newValue = Math.Max(value, MinCanvasSize);
                if (Math.Abs(_actualContentWidth - newValue) > 0.1)
                {
                    _actualContentWidth = newValue;
                    NotifyOfPropertyChange();
                }
            }
        }

        /// <summary>
        /// Actual content height based on positioned nodes (with minimum for panning).
        /// </summary>
        public double ActualContentHeight
        {
            get => _actualContentHeight;
            private set
            {
                var newValue = Math.Max(value, MinCanvasSize);
                if (Math.Abs(_actualContentHeight - newValue) > 0.1)
                {
                    _actualContentHeight = newValue;
                    NotifyOfPropertyChange();
                }
            }
        }

        [ImportingConstructor]
        public VisualQueryPlanViewModel(
            IEventAggregator eventAggregator,
            IGlobalOptions globalOptions,
            IWindowManager windowManager)
            : this(eventAggregator, globalOptions, windowManager, new PlanEnrichmentService())
        {
        }

        public VisualQueryPlanViewModel(
            IEventAggregator eventAggregator,
            IGlobalOptions globalOptions,
            IWindowManager windowManager,
            IPlanEnrichmentService enrichmentService)
            : base(eventAggregator, globalOptions, windowManager)
        {
            Log.Information("{class} {method} {message}", nameof(VisualQueryPlanViewModel), "ctor", "Visual Query Plan ViewModel created");
            _enrichmentService = enrichmentService ?? throw new ArgumentNullException(nameof(enrichmentService));
            Issues = new BindableCollection<IssueViewModel>();
        }

        #region Properties

        /// <summary>
        /// The root node of the currently displayed plan tree.
        /// </summary>
        public PlanNodeViewModel RootNode
        {
            get => _rootNode;
            private set
            {
                _rootNode = value;
                NotifyOfPropertyChange();
                NotifyOfPropertyChange(nameof(HasPlanData));
            }
        }

        /// <summary>
        /// All nodes in a flat list for easy iteration.
        /// </summary>
        public BindableCollection<PlanNodeViewModel> AllNodes { get; } = new BindableCollection<PlanNodeViewModel>();

        /// <summary>
        /// Currently selected node in the graph.
        /// </summary>
        public PlanNodeViewModel SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (_selectedNode != value)
                {
                    if (_selectedNode != null)
                        _selectedNode.IsSelected = false;

                    _selectedNode = value;

                    if (_selectedNode != null)
                        _selectedNode.IsSelected = true;

                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(HasSelectedNode));
                }
            }
        }

        /// <summary>
        /// Whether a node is currently selected.
        /// </summary>
        public bool HasSelectedNode => _selectedNode != null;

        /// <summary>
        /// Index of the selected tab in the right panel (0=Node Details, 1=Issues).
        /// </summary>
        public int SelectedTabIndex
        {
            get => _selectedTabIndex;
            set
            {
                if (_selectedTabIndex != value)
                {
                    _selectedTabIndex = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        /// <summary>
        /// Detected performance issues wrapped for UI display.
        /// </summary>
        public BindableCollection<IssueViewModel> Issues { get; }

        /// <summary>
        /// Currently selected issue in the issues panel.
        /// </summary>
        private IssueViewModel _selectedIssue;
        public IssueViewModel SelectedIssue
        {
            get => _selectedIssue;
            set
            {
                if (_selectedIssue != value)
                {
                    // Deselect previous issue
                    if (_selectedIssue != null)
                        _selectedIssue.IsSelected = false;

                    _selectedIssue = value;

                    // Select new issue (but don't auto-navigate - user must click "Go to Node")
                    if (_selectedIssue != null)
                    {
                        _selectedIssue.IsSelected = true;
                    }

                    NotifyOfPropertyChange();
                }
            }
        }

        /// <summary>
        /// Whether there are any detected issues.
        /// </summary>
        public bool HasIssues => Issues.Count > 0;

        /// <summary>
        /// Whether there are no detected issues (for "No issues detected" message).
        /// </summary>
        public bool HasNoIssues => Issues.Count == 0;

        /// <summary>
        /// Number of detected issues.
        /// </summary>
        public int IssueCount => Issues.Count;

        /// <summary>
        /// Whether to show the physical plan (true) or logical plan (false).
        /// </summary>
        public bool ShowPhysicalPlan
        {
            get => _showPhysicalPlan;
            set
            {
                if (_showPhysicalPlan != value)
                {
                    _showPhysicalPlan = value;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ShowLogicalPlan));
                    UpdateDisplayedPlan();
                }
            }
        }

        /// <summary>
        /// Whether to show the logical plan.
        /// </summary>
        public bool ShowLogicalPlan
        {
            get => !_showPhysicalPlan;
            set => ShowPhysicalPlan = !value;
        }

        /// <summary>
        /// Current zoom level for the graph.
        /// </summary>
        public double ZoomLevel
        {
            get => _zoomLevel;
            set
            {
                var newValue = Math.Max(0.1, Math.Min(3.0, value));
                if (Math.Abs(_zoomLevel - newValue) > 0.001)
                {
                    _zoomLevel = newValue;
                    NotifyOfPropertyChange();
                    NotifyOfPropertyChange(nameof(ZoomPercentage));
                }
            }
        }

        /// <summary>
        /// Zoom level as a percentage string.
        /// </summary>
        public string ZoomPercentage => $"{ZoomLevel * 100:F0}%";

        /// <summary>
        /// Whether there is plan data to display.
        /// </summary>
        public bool HasPlanData => _rootNode != null;

        /// <summary>
        /// Raw physical query plan text.
        /// </summary>
        public string PhysicalQueryPlanText
        {
            get => _physicalQueryPlanText;
            private set
            {
                _physicalQueryPlanText = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Raw logical query plan text.
        /// </summary>
        public string LogicalQueryPlanText
        {
            get => _logicalQueryPlanText;
            private set
            {
                _logicalQueryPlanText = value;
                NotifyOfPropertyChange();
            }
        }

        /// <summary>
        /// Total query duration in milliseconds.
        /// </summary>
        public long TotalDuration { get; private set; }

        /// <summary>
        /// Activity ID for the query.
        /// </summary>
        public string ActivityID { get; set; }

        /// <summary>
        /// Request ID for the query.
        /// </summary>
        public string RequestID { get; set; }

        /// <summary>
        /// Query start time.
        /// </summary>
        public DateTime StartDatetime { get; set; }

        /// <summary>
        /// The DAX query text.
        /// </summary>
        public string CommandText { get; set; }

        /// <summary>
        /// Query parameters.
        /// </summary>
        public string Parameters { get; set; }

        #endregion

        #region TraceWatcherBaseViewModel Overrides

        public override string Title => "Visual Query Plan";
        public override string ImageResource => "visual_query_planDrawingImage";
        public override string TraceSuffix => "visual-plan";
        public override string ContentId => "visual-query-plan";
        public override string KeyTip => "VP";
        public override int SortOrder => 25;  // After Query Plan (20)
        public override string ToolTipText => "Displays an interactive graphical representation of the DAX Query Plan";

        public override bool FilterForCurrentSession => true;

        protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
        {
            return new List<DaxStudioTraceEventClass>
            {
                DaxStudioTraceEventClass.DAXQueryPlan,
                DaxStudioTraceEventClass.QueryBegin,
                DaxStudioTraceEventClass.QueryEnd,
                DaxStudioTraceEventClass.VertiPaqSEQueryEnd,       // For SE timing correlation
                DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch, // For cache hit info
                DaxStudioTraceEventClass.DirectQueryEnd             // For DirectQuery timing correlation
            };
        }

        protected override bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent)
        {
            return traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd ||
                   traceEvent.EventClass == DaxStudioTraceEventClass.Error;
        }

        protected override async void ProcessResults()
        {
            if (HasPlanData)
            {
                // Results have not been cleared, probably from another action
                return;
            }

            var physicalPlanRows = new List<PhysicalQueryPlanRow>();
            var logicalPlanRows = new List<LogicalQueryPlanRow>();
            var timingEvents = new List<TraceStorageEngineEvent>();

            while (!Events.IsEmpty)
            {
                Events.TryDequeue(out var traceEvent);

                if (traceEvent.EventClass == DaxStudioTraceEventClass.DAXQueryPlan)
                {
                    if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.DAXVertiPaqPhysicalPlan)
                    {
                        PhysicalQueryPlanText = traceEvent.TextData;
                        physicalPlanRows.AddRange(ParsePhysicalPlan(traceEvent.TextData));
                    }
                    else if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.DAXVertiPaqLogicalPlan)
                    {
                        LogicalQueryPlanText = traceEvent.TextData;
                        logicalPlanRows.AddRange(ParseLogicalPlan(traceEvent.TextData));
                    }
                }
                else if (traceEvent.EventClass == DaxStudioTraceEventClass.QueryBegin)
                {
                    Parameters = traceEvent.RequestParameters;
                    StartDatetime = traceEvent.StartTime;
                }
                else if (traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd)
                {
                    ActivityID = traceEvent.ActivityId;
                    RequestID = traceEvent.RequestId;
                    CommandText = traceEvent.TextData;
                    TotalDuration = traceEvent.Duration;
                }
                else if (traceEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryEnd)
                {
                    // Extract SE events directly from the queue
                    Log.Debug("{class} {method} Found SE event - Duration={Duration}, ObjectName={ObjectName}",
                        nameof(VisualQueryPlanViewModel), nameof(ProcessResults), traceEvent.Duration, traceEvent.ObjectName);
                    var seEvent = new TraceStorageEngineEvent
                    {
                        ObjectName = traceEvent.ObjectName,
                        Query = traceEvent.TextData,
                        TextData = traceEvent.TextData,
                        Duration = traceEvent.Duration,
                        CpuTime = traceEvent.CpuTime,
                        Subclass = traceEvent.EventSubclass,
                        NetParallelDuration = traceEvent.NetParallelDuration,
                        StartTime = traceEvent.StartTime,
                        EndTime = traceEvent.EndTime
                    };
                    ExtractEstimatedSizeFromQuery(seEvent);
                    timingEvents.Add(seEvent);
                }
                else if (traceEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryCacheMatch)
                {
                    // Cache match events - may have timing data for cache hits
                    Log.Debug("{class} {method} Found SE cache match - Duration={Duration}, ObjectName={ObjectName}",
                        nameof(VisualQueryPlanViewModel), nameof(ProcessResults), traceEvent.Duration, traceEvent.ObjectName);
                    var seEvent = new TraceStorageEngineEvent
                    {
                        ObjectName = traceEvent.ObjectName,
                        Query = traceEvent.TextData,
                        TextData = traceEvent.TextData,
                        Duration = traceEvent.Duration,
                        CpuTime = traceEvent.CpuTime,
                        Subclass = traceEvent.EventSubclass,
                        NetParallelDuration = traceEvent.NetParallelDuration,
                        StartTime = traceEvent.StartTime,
                        EndTime = traceEvent.EndTime
                    };
                    ExtractEstimatedSizeFromQuery(seEvent);
                    timingEvents.Add(seEvent);
                }
                else if (traceEvent.EventClass == DaxStudioTraceEventClass.DirectQueryEnd)
                {
                    // DirectQuery events - timing for queries sent to external data source
                    Log.Debug("{class} {method} Found DirectQuery event - Duration={Duration}, ObjectName={ObjectName}",
                        nameof(VisualQueryPlanViewModel), nameof(ProcessResults), traceEvent.Duration, traceEvent.ObjectName);
                    var seEvent = new TraceStorageEngineEvent
                    {
                        ObjectName = traceEvent.ObjectName ?? "DirectQuery",
                        Query = traceEvent.TextData,
                        TextData = traceEvent.TextData,
                        Duration = traceEvent.Duration,
                        CpuTime = traceEvent.CpuTime,
                        Subclass = traceEvent.EventSubclass,
                        NetParallelDuration = traceEvent.NetParallelDuration,
                        StartTime = traceEvent.StartTime,
                        EndTime = traceEvent.EndTime,
                        IsDirectQuery = true
                    };
                    ExtractEstimatedSizeFromQuery(seEvent);
                    timingEvents.Add(seEvent);
                }
            }

            Log.Debug("{class} {method} Event processing complete - PhysicalRows={Physical}, LogicalRows={Logical}, SEEvents={SE}, TotalDuration={TotalDuration}ms",
                nameof(VisualQueryPlanViewModel), nameof(ProcessResults), physicalPlanRows.Count, logicalPlanRows.Count, timingEvents.Count, TotalDuration);

            // Enrich the plans asynchronously
            try
            {
                if (physicalPlanRows.Count > 0)
                {
                    _physicalPlan = await _enrichmentService.EnrichPhysicalPlanAsync(
                        physicalPlanRows,
                        timingEvents,
                        null,  // Column resolver - can be enhanced later
                        ActivityID);

                    // Set total duration and recalculate FE duration
                    _physicalPlan.TotalDurationMs = TotalDuration;
                    if (TotalDuration > 0)
                    {
                        _physicalPlan.FormulaEngineDurationMs = Math.Max(0, TotalDuration - _physicalPlan.StorageEngineDurationMs);
                    }

                    Log.Debug("{class} {method} Physical plan set - TotalDuration={Total}, SEDuration={SE}, FEDuration={FE}",
                        nameof(VisualQueryPlanViewModel), nameof(ProcessResults),
                        _physicalPlan.TotalDurationMs, _physicalPlan.StorageEngineDurationMs, _physicalPlan.FormulaEngineDurationMs);
                }

                if (logicalPlanRows.Count > 0)
                {
                    _logicalPlan = await _enrichmentService.EnrichLogicalPlanAsync(
                        logicalPlanRows,
                        timingEvents,
                        null,  // Column resolver
                        ActivityID);

                    // Set total duration and recalculate FE duration for logical plan too
                    _logicalPlan.TotalDurationMs = TotalDuration;
                    if (TotalDuration > 0)
                    {
                        _logicalPlan.FormulaEngineDurationMs = Math.Max(0, TotalDuration - _logicalPlan.StorageEngineDurationMs);
                    }

                    Log.Debug("{class} {method} Logical plan set - TotalDuration={Total}, SEDuration={SE}, FEDuration={FE}",
                        nameof(VisualQueryPlanViewModel), nameof(ProcessResults),
                        _logicalPlan.TotalDurationMs, _logicalPlan.StorageEngineDurationMs, _logicalPlan.FormulaEngineDurationMs);
                }

                // Cross-reference logical plan with physical plan to infer engine types and row counts
                if (_logicalPlan != null && _physicalPlan != null)
                {
                    _enrichmentService.CrossReferenceLogicalWithPhysical(_logicalPlan, _physicalPlan);
                }

                // Update the displayed plan (also updates Issues collection)
                UpdateDisplayedPlan();

                NotifyOfPropertyChange(nameof(TotalDuration));
                NotifyOfPropertyChange(nameof(CanExport));
                NotifyOfPropertyChange(nameof(CanShowTraceDiagnostics));
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error enriching query plan");
                ErrorMessage = $"Error processing query plan: {ex.Message}";
            }
        }

        public override void OnReset()
        {
            IsBusy = false;
            ClearAll();
            ProcessResults();
        }

        public override void ClearAll()
        {
            Events.Clear();

            _physicalPlan = null;
            _logicalPlan = null;
            RootNode = null;
            SelectedNode = null;
            SelectedIssue = null;
            AllNodes.Clear();
            Issues.Clear();
            PhysicalQueryPlanText = null;
            LogicalQueryPlanText = null;
            ActivityID = null;
            RequestID = null;
            CommandText = null;
            Parameters = null;

            NotifyOfPropertyChange(nameof(HasPlanData));
            NotifyOfPropertyChange(nameof(HasIssues));
            NotifyOfPropertyChange(nameof(HasNoIssues));
            NotifyOfPropertyChange(nameof(IssueCount));
            NotifyOfPropertyChange(nameof(CanExport));
        }

        public override void CopyAll()
        {
            try
            {
                var text = ShowPhysicalPlan ? PhysicalQueryPlanText : LogicalQueryPlanText;
                if (!string.IsNullOrEmpty(text))
                {
                    Clipboard.SetText(text);
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Error copying plan text to clipboard");
            }
        }

        public override void CopyResults()
        {
            CopyAll();
        }

        public override void CopyEventContent()
        {
            CopyAll();
        }

        public override bool CanExport => HasPlanData;

        public override void ExportTraceDetails(string filePath)
        {
            var json = GetJson();
            File.WriteAllText(filePath, json);
        }

        #endregion

        #region Commands

        public void ZoomIn()
        {
            ZoomLevel += 0.1;
        }

        public void ZoomOut()
        {
            ZoomLevel -= 0.1;
        }

        public void ZoomToFit()
        {
            ZoomLevel = 1.0;
        }

        public void SelectNode(PlanNodeViewModel node)
        {
            SelectedNode = node;
            // Auto-switch to Node Details tab when a node is selected (index 0)
            if (node != null)
            {
                SelectedTabIndex = 0;
            }
        }

        public void NavigateToIssue(IssueViewModel issue)
        {
            if (issue == null) return;

            var node = FindNodeOrAncestor(issue.AffectedNodeId);
            if (node != null)
            {
                SelectedNode = node;
                // Switch to Node Details tab to show the selected node
                SelectedTabIndex = 0;

                // Scroll the node into view
                ScrollNodeIntoView(node);
            }
        }

        /// <summary>
        /// Scrolls the plan view to center on the specified node.
        /// </summary>
        public void ScrollNodeIntoView(PlanNodeViewModel node)
        {
            if (node == null) return;

            // Fire an event that the view can handle to scroll to the node
            NodeScrollRequested?.Invoke(this, new NodeScrollEventArgs(node));
        }

        /// <summary>
        /// Event fired when a node should be scrolled into view.
        /// </summary>
        public event EventHandler<NodeScrollEventArgs> NodeScrollRequested;

        #endregion

        #region ISaveState Implementation

        void ISaveState.Save(string filename)
        {
            var json = GetJson();
            File.WriteAllText(filename + ".visualPlan", json);
        }

        public string GetJson()
        {
            var model = new VisualQueryPlanModel
            {
                PhysicalQueryPlanText = PhysicalQueryPlanText,
                LogicalQueryPlanText = LogicalQueryPlanText,
                ActivityID = ActivityID,
                RequestID = RequestID,
                CommandText = CommandText,
                Parameters = Parameters,
                StartDatetime = StartDatetime,
                TotalDuration = TotalDuration
            };

            return JsonConvert.SerializeObject(model, Formatting.Indented);
        }

        void ISaveState.Load(string filename)
        {
            filename = filename + ".visualPlan";
            if (!File.Exists(filename)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            var data = File.ReadAllText(filename);
            LoadJson(data);
        }

        public void LoadJson(string data)
        {
            var model = JsonConvert.DeserializeObject<VisualQueryPlanModel>(data);
            if (model == null) return;

            PhysicalQueryPlanText = model.PhysicalQueryPlanText;
            LogicalQueryPlanText = model.LogicalQueryPlanText;
            ActivityID = model.ActivityID;
            RequestID = model.RequestID;
            CommandText = model.CommandText;
            Parameters = model.Parameters;
            StartDatetime = model.StartDatetime;
            TotalDuration = model.TotalDuration;

            // Re-parse and enrich the plans
            Task.Run(async () =>
            {
                if (!string.IsNullOrEmpty(PhysicalQueryPlanText))
                {
                    var rows = ParsePhysicalPlan(PhysicalQueryPlanText);
                    _physicalPlan = await _enrichmentService.EnrichPhysicalPlanAsync(
                        rows, null, null, ActivityID);

                    // Set timing values from loaded state (same as ProcessResults)
                    _physicalPlan.TotalDurationMs = TotalDuration;
                    if (TotalDuration > 0)
                    {
                        _physicalPlan.FormulaEngineDurationMs = Math.Max(0, TotalDuration - _physicalPlan.StorageEngineDurationMs);
                    }
                }

                if (!string.IsNullOrEmpty(LogicalQueryPlanText))
                {
                    var rows = ParseLogicalPlan(LogicalQueryPlanText);
                    _logicalPlan = await _enrichmentService.EnrichLogicalPlanAsync(
                        rows, null, null, ActivityID);

                    // Set timing values from loaded state (same as ProcessResults)
                    _logicalPlan.TotalDurationMs = TotalDuration;
                    if (TotalDuration > 0)
                    {
                        _logicalPlan.FormulaEngineDurationMs = Math.Max(0, TotalDuration - _logicalPlan.StorageEngineDurationMs);
                    }
                }

                Execute.OnUIThread(() => UpdateDisplayedPlan());
            });

            NotifyOfPropertyChange(nameof(CanExport));
            NotifyOfPropertyChange(nameof(CanShowTraceDiagnostics));
        }

        public void SavePackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.VisualQueryPlan, UriKind.Relative));
            using (var tw = new StreamWriter(package.CreatePart(uri, "application/json", CompressionOption.Maximum).GetStream(), Encoding.UTF8))
            {
                tw.Write(GetJson());
            }
        }

        public void LoadPackage(Package package)
        {
            var uri = PackUriHelper.CreatePartUri(new Uri(DaxxFormat.VisualQueryPlan, UriKind.Relative));
            if (!package.PartExists(uri)) return;

            _eventAggregator.PublishOnUIThreadAsync(new ShowTraceWindowEvent(this));
            var part = package.GetPart(uri);
            using (var tr = new StreamReader(part.GetStream()))
            {
                LoadJson(tr.ReadToEnd());
            }
        }

        #endregion

        #region ITraceDiagnostics Implementation

        public bool CanShowTraceDiagnostics => CanExport;

        public async void ShowTraceDiagnostics()
        {
            var diagnosticsViewModel = new RequestInformationViewModel(this);
            await WindowManager.ShowDialogBoxAsync(diagnosticsViewModel, settings: new Dictionary<string, object>
            {
                { "WindowStyle", WindowStyle.None },
                { "ShowInTaskbar", false },
                { "ResizeMode", ResizeMode.NoResize },
                { "Background", Brushes.Transparent },
                { "AllowsTransparency", true }
            });
        }

        #endregion

        #region IHaveData Implementation

        public bool HasData => HasPlanData;

        #endregion

        #region Private Methods

        /// <summary>
        /// Extracts estimated rows and KB from xmSQL query text.
        /// Supports two formats:
        /// - Raw xmSQL:  'Estimated size ... : 3655, 458752' or [Estimated size ... : 3655, 458752]
        /// - Formatted:  Estimated size: rows = 3655, bytes = 458752
        /// </summary>
        private void ExtractEstimatedSizeFromQuery(TraceStorageEngineEvent seEvent)
        {
            if (string.IsNullOrEmpty(seEvent.Query)) return;

            // First try raw xmSQL format: 'Estimated size ... : rows, bytes' or [Estimated size ... : rows, bytes]
            var match = System.Text.RegularExpressions.Regex.Match(
                seEvent.Query,
                @"[\'\[]Estimated size[^\]\']*:\s*(\d+),\s*(\d+)[\'\]]",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            if (!match.Success)
            {
                // Fallback to formatted pattern: "Estimated size: rows = X, bytes = Y"
                match = System.Text.RegularExpressions.Regex.Match(
                    seEvent.Query,
                    @"Estimated size:\s*rows\s*=\s*(\d+),\s*bytes\s*=\s*(\d+)",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            if (match.Success)
            {
                if (long.TryParse(match.Groups[1].Value, out var rows))
                {
                    seEvent.EstimatedRows = rows;
                }
                if (long.TryParse(match.Groups[2].Value, out var bytes))
                {
                    seEvent.EstimatedKBytes = bytes / 1024;  // Convert bytes to KB
                }
            }
        }

        private void UpdateDisplayedPlan()
        {
            var plan = ShowPhysicalPlan ? _physicalPlan : _logicalPlan;
            if (plan == null)
            {
                RootNode = null;
                AllNodes.Clear();
                Issues.Clear();
                NotifyOfPropertyChange(nameof(HasIssues));
                NotifyOfPropertyChange(nameof(HasNoIssues));
                NotifyOfPropertyChange(nameof(IssueCount));
                return;
            }

            RootNode = PlanNodeViewModel.BuildTree(plan);
            AllNodes.Clear();

            if (RootNode != null)
            {
                // Collapse simple comparison and arithmetic operators to reduce visual clutter
                CollapseSimpleOperators(RootNode);

                CollectAllNodes(RootNode, AllNodes);
                ResolveMeasureFormulas(AllNodes);
                CalculateLayout();

                // Auto-select the root node to show execution metrics in the details panel
                SelectedNode = RootNode;
            }

            // Update issues from the currently displayed plan
            Issues.Clear();
            if (plan.Issues != null)
            {
                foreach (var issue in plan.Issues)
                {
                    Issues.Add(new IssueViewModel(issue));
                }
            }
            NotifyOfPropertyChange(nameof(HasIssues));
            NotifyOfPropertyChange(nameof(HasNoIssues));
            NotifyOfPropertyChange(nameof(IssueCount));

            NotifyOfPropertyChange(nameof(HasPlanData));

            // Notify view to adjust scroll position if needed
            PlanLayoutUpdated?.Invoke(this, System.EventArgs.Empty);
        }

        /// <summary>
        /// Recursively collapses simple comparison and arithmetic operators with their operand children
        /// into single nodes for cleaner display. E.g., "GreaterThan" with "ColValue" and "Constant"
        /// children becomes "[Amount] > 100", and "Multiply" with "ColValue" children becomes "[Qty] * [Price]".
        /// Also collapses chains of Proxy operators to reduce visual clutter.
        /// </summary>
        private void CollapseSimpleOperators(PlanNodeViewModel node)
        {
            if (node == null) return;

            // First, collapse Proxy chains (remove intermediate Proxy operators)
            CollapseProxyChains(node);

            // Process children first (bottom-up), since we'll be modifying the tree
            foreach (var child in node.Children.ToList())
            {
                CollapseSimpleOperators(child);
            }

            // Now try to collapse this node if it's a simple comparison or arithmetic operation
            node.CollapseIfPossible();
        }

        /// <summary>
        /// Collapses chains of Proxy operators by removing intermediate Proxy nodes.
        /// E.g., Proxy -> Proxy -> Union becomes Proxy -> Union (with collapsed count shown)
        /// </summary>
        private void CollapseProxyChains(PlanNodeViewModel node)
        {
            if (node == null) return;

            // Keep collapsing while this node is a Proxy with a single Proxy child
            while (IsProxyOperator(node) && node.Children.Count == 1 && IsProxyOperator(node.Children[0]))
            {
                var proxyChild = node.Children[0];

                // Track the collapsed operation text
                node.CollapsedProxyOperations ??= new List<string>();
                node.CollapsedProxyOperations.Add(proxyChild.Operation ?? proxyChild.OperatorName);

                // Replace the single Proxy child with its children
                node.Children.Clear();
                foreach (var grandchild in proxyChild.Children)
                {
                    node.Children.Add(grandchild);
                }
            }

            // Recursively process remaining children
            foreach (var child in node.Children.ToList())
            {
                CollapseProxyChains(child);
            }
        }

        /// <summary>
        /// Checks if a node is a Proxy operator (used for collapsing chains).
        /// </summary>
        private bool IsProxyOperator(PlanNodeViewModel node)
        {
            if (node == null) return false;
            var opName = node.OperatorName?.ToLowerInvariant() ?? "";
            return opName.StartsWith("proxy");
        }

        /// <summary>
        /// Resolves DAX measure formulas from model metadata for nodes with measure references.
        /// </summary>
        private void ResolveMeasureFormulas(BindableCollection<PlanNodeViewModel> nodes)
        {
            // Try to get measure expressions from the document/connection
            // This requires access to model metadata - will be populated when available
            var measureExpressions = GetMeasureExpressions();
            if (measureExpressions == null || measureExpressions.Count == 0)
            {
                Log.Debug("VisualQueryPlanViewModel: No measure expressions available for formula resolution");
                return;
            }

            int resolved = 0;
            foreach (var node in nodes)
            {
                if (!node.HasMeasureReference)
                    continue;

                // Strip brackets: "[Internet Sales]" -> "Internet Sales"
                var measureName = node.MeasureReference.Trim('[', ']');

                if (measureExpressions.TryGetValue(measureName, out var formula))
                {
                    node.MeasureFormula = formula;
                    resolved++;
                }
            }

            Log.Debug("VisualQueryPlanViewModel: Resolved {Count} measure formulas", resolved);
        }

        /// <summary>
        /// Gets the dictionary of measure expressions from the model metadata and query-scoped DEFINE MEASURE statements.
        /// </summary>
        private Dictionary<string, string> GetMeasureExpressions()
        {
            var expressions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                // First, parse query-scoped DEFINE MEASURE statements from the query text
                // These take priority as they override model measures
                // Try multiple sources for query text
                var queryText = QueryHistoryEvent?.QueryText;

                // Fallback: try getting from Document if QueryHistoryEvent not yet populated
                if (string.IsNullOrEmpty(queryText) && Document is IQueryTextProvider queryTextProvider)
                {
                    queryText = queryTextProvider.EditorText;
                }

                if (!string.IsNullOrEmpty(queryText))
                {
                    Log.Debug("VisualQueryPlanViewModel: Parsing query text for measures ({Length} chars)", queryText.Length);
                    ParseQueryScopedMeasures(queryText, expressions);
                }
                else
                {
                    Log.Debug("VisualQueryPlanViewModel: No query text available for measure parsing");
                }

                // Then add model measures (won't override query-scoped ones due to ContainsKey check)
                var connManager = Document?.Connection as Model.ConnectionManager;
                if (connManager?.SelectedModel != null)
                {
                    var model = connManager.SelectedModel;

                    // Try the cached MeasureExpressions dictionary first
                    if (model.MeasureExpressions != null && model.MeasureExpressions.Count > 0)
                    {
                        foreach (var kvp in model.MeasureExpressions)
                        {
                            if (!expressions.ContainsKey(kvp.Key))
                            {
                                expressions[kvp.Key] = kvp.Value;
                            }
                        }
                        Log.Debug("VisualQueryPlanViewModel: Added {Count} model measure expressions", model.MeasureExpressions.Count);
                    }
                    // Fallback: Build from Tables/Measures
                    else if (model.Tables != null)
                    {
                        foreach (var table in model.Tables)
                        {
                            if (table.Measures != null)
                            {
                                foreach (var measure in table.Measures)
                                {
                                    if (!string.IsNullOrEmpty(measure.Name) && !string.IsNullOrEmpty(measure.Expression))
                                    {
                                        var name = measure.Name.Trim('[', ']');
                                        if (!expressions.ContainsKey(name))
                                        {
                                            expressions[name] = measure.Expression;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "VisualQueryPlanViewModel: Failed to access measure expressions");
            }

            Log.Debug("VisualQueryPlanViewModel: Total measure expressions available: {Count}", expressions.Count);
            return expressions;
        }

        /// <summary>
        /// Parses DEFINE MEASURE statements from query text to extract query-scoped measures.
        /// </summary>
        private void ParseQueryScopedMeasures(string queryText, Dictionary<string, string> expressions)
        {
            // Pattern: DEFINE MEASURE 'Table'[MeasureName] = <expression>
            // or: DEFINE MEASURE [MeasureName] = <expression>
            // Expression continues until next DEFINE, EVALUATE, VAR, or end of text
            var pattern = @"DEFINE\s+MEASURE\s+(?:'[^']*')?\[([^\]]+)\]\s*=\s*";
            var matches = Regex.Matches(queryText, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline);

            foreach (Match match in matches)
            {
                var measureName = match.Groups[1].Value;
                var expressionStart = match.Index + match.Length;

                // Find where the expression ends (next DEFINE, EVALUATE, or end)
                var remainingText = queryText.Substring(expressionStart);
                var endPatterns = new[] { @"\bDEFINE\b", @"\bEVALUATE\b", @"\bVAR\b" };
                var endIndex = remainingText.Length;

                foreach (var endPattern in endPatterns)
                {
                    var endMatch = Regex.Match(remainingText, endPattern, RegexOptions.IgnoreCase);
                    if (endMatch.Success && endMatch.Index < endIndex)
                    {
                        endIndex = endMatch.Index;
                    }
                }

                var expression = remainingText.Substring(0, endIndex).Trim();

                // Remove trailing comments if any
                var commentIndex = expression.LastIndexOf("/*");
                if (commentIndex > 0 && expression.IndexOf("*/", commentIndex) > commentIndex)
                {
                    // Has a complete comment at end, might need to trim
                }

                if (!string.IsNullOrEmpty(measureName) && !string.IsNullOrEmpty(expression))
                {
                    expressions[measureName] = expression;
                    Log.Debug("VisualQueryPlanViewModel: Parsed query-scoped measure [{MeasureName}]", measureName);
                }
            }
        }

        private void CollectAllNodes(PlanNodeViewModel node, BindableCollection<PlanNodeViewModel> collection)
        {
            collection.Add(node);
            foreach (var child in node.Children)
            {
                CollectAllNodes(child, collection);
            }
        }

        private void CalculateLayout()
        {
            if (RootNode == null)
            {
                ActualContentWidth = 0;
                ActualContentHeight = 0;
                return;
            }

            // Simple top-down tree layout with padding
            const double horizontalSpacing = 30;
            const double verticalSpacing = 60;
            const double paddingLeft = 150;   // Left margin matching bottom for scroll-and-zoom workflow
            const double paddingRight = 150;  // Right margin matching bottom
            const double paddingTop = 20;
            const double paddingBottom = 150; // Extra bottom padding for scroll-and-zoom workflow

            var levelWidths = new Dictionary<int, double>();
            var levelCounts = new Dictionary<int, int>();

            // Calculate level widths
            CalculateLevelInfo(RootNode, 0, levelWidths, levelCounts);

            // Position nodes with initial padding
            PositionNodes(RootNode, 0, paddingLeft, horizontalSpacing, verticalSpacing, levelCounts, paddingTop);

            // Calculate actual content bounds from all positioned nodes
            double maxX = 0;
            double maxY = 0;
            foreach (var node in AllNodes)
            {
                var nodeRight = node.X + node.Width;
                var nodeBottom = node.Y + node.Height;
                if (nodeRight > maxX) maxX = nodeRight;
                if (nodeBottom > maxY) maxY = nodeBottom;
            }

            // Add padding to the bounds
            ActualContentWidth = maxX + paddingRight;
            ActualContentHeight = maxY + paddingBottom;
        }

        private void CalculateLevelInfo(PlanNodeViewModel node, int level,
            Dictionary<int, double> levelWidths, Dictionary<int, int> levelCounts)
        {
            if (!levelCounts.ContainsKey(level))
            {
                levelCounts[level] = 0;
                levelWidths[level] = 0;
            }

            levelCounts[level]++;
            levelWidths[level] += node.Width;

            foreach (var child in node.Children)
            {
                CalculateLevelInfo(child, level + 1, levelWidths, levelCounts);
            }
        }

        /// <summary>
        /// Calculates the width needed by a subtree (including all descendants).
        /// This is used for compact layout of unbalanced trees.
        /// </summary>
        private double CalculateSubtreeWidth(PlanNodeViewModel node, double horizontalSpacing)
        {
            if (node.Children.Count == 0)
            {
                return node.Width;
            }

            // Sum of all children's subtree widths plus spacing between them
            double totalChildWidth = 0;
            foreach (var child in node.Children)
            {
                if (totalChildWidth > 0) totalChildWidth += horizontalSpacing;
                totalChildWidth += CalculateSubtreeWidth(child, horizontalSpacing);
            }

            // Parent node might be wider than all children combined
            return Math.Max(node.Width, totalChildWidth);
        }

        private double PositionNodes(PlanNodeViewModel node, int level, double xOffset,
            double horizontalSpacing, double verticalSpacing, Dictionary<int, int> levelCounts, double paddingTop = 0)
        {
            double y = paddingTop + level * (node.Height + verticalSpacing);

            if (node.Children.Count == 0)
            {
                node.Position = new Point(xOffset, y);
                return xOffset + node.Width + horizontalSpacing;
            }

            // Calculate total width needed for all children
            double totalChildrenWidth = 0;
            var childWidths = new List<double>();
            foreach (var child in node.Children)
            {
                var width = CalculateSubtreeWidth(child, horizontalSpacing);
                childWidths.Add(width);
                if (totalChildrenWidth > 0) totalChildrenWidth += horizontalSpacing;
                totalChildrenWidth += width;
            }

            // If parent is wider than children, start children centered under parent
            double childStartOffset;
            if (node.Width > totalChildrenWidth)
            {
                // Parent is wider - center children under parent
                childStartOffset = xOffset + (node.Width - totalChildrenWidth) / 2;
                node.Position = new Point(xOffset, y);
            }
            else
            {
                // Children are wider - position children from current offset
                childStartOffset = xOffset;
            }

            // Position each child with its allocated subtree width
            double childOffset = childStartOffset;
            double maxChildRight = 0;
            for (int i = 0; i < node.Children.Count; i++)
            {
                var child = node.Children[i];
                var allocatedWidth = childWidths[i];

                // Center this child within its allocated width
                var childSubtreeWidth = CalculateSubtreeWidth(child, horizontalSpacing);
                var childCenterOffset = childOffset + (allocatedWidth - childSubtreeWidth) / 2;

                var rightEdge = PositionNodes(child, level + 1, childCenterOffset, horizontalSpacing, verticalSpacing, levelCounts, paddingTop);
                maxChildRight = Math.Max(maxChildRight, rightEdge);

                childOffset += allocatedWidth + horizontalSpacing;
            }

            // If children are wider, center parent over children
            if (node.Width <= totalChildrenWidth)
            {
                double firstChildX = node.Children.First().X;
                double lastChildX = node.Children.Last().X + node.Children.Last().Width;
                double centerX = (firstChildX + lastChildX) / 2 - node.Width / 2;
                node.Position = new Point(centerX, y);
            }

            return Math.Max(maxChildRight, node.X + node.Width + horizontalSpacing);
        }

        private PlanNodeViewModel FindNodeById(int nodeId)
        {
            return AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
        }

        /// <summary>
        /// Finds a node by ID, or its nearest visible ancestor if the node was folded.
        /// </summary>
        private PlanNodeViewModel FindNodeOrAncestor(int nodeId)
        {
            // First try direct lookup
            var node = FindNodeById(nodeId);
            if (node != null)
                return node;

            // Node was folded - find it in the plan and navigate to parent
            var plan = ShowPhysicalPlan ? _physicalPlan : _logicalPlan;
            if (plan?.AllNodes == null)
                return null;

            var enrichedNode = plan.AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
            while (enrichedNode?.Parent != null)
            {
                enrichedNode = enrichedNode.Parent;
                node = FindNodeById(enrichedNode.NodeId);
                if (node != null)
                {
                    return node;
                }
            }

            return null;
        }

        private List<PhysicalQueryPlanRow> ParsePhysicalPlan(string planText)
        {
            var rows = new List<PhysicalQueryPlanRow>();
            if (string.IsNullOrEmpty(planText)) return rows;

            int rowNumber = 0;
            foreach (var line in planText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var row = new PhysicalQueryPlanRow();
                row.PrepareQueryPlanRow(line, ++rowNumber);
                rows.Add(row);
            }

            return rows;
        }

        private List<LogicalQueryPlanRow> ParseLogicalPlan(string planText)
        {
            var rows = new List<LogicalQueryPlanRow>();
            if (string.IsNullOrEmpty(planText)) return rows;

            int rowNumber = 0;
            foreach (var line in planText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                var row = new LogicalQueryPlanRow();
                row.PrepareQueryPlanRow(line, ++rowNumber);
                rows.Add(row);
            }

            return rows;
        }

        #endregion
    }

    /// <summary>
    /// Model for serializing visual query plan data.
    /// </summary>
    public class VisualQueryPlanModel
    {
        public string PhysicalQueryPlanText { get; set; }
        public string LogicalQueryPlanText { get; set; }
        public string ActivityID { get; set; }
        public string RequestID { get; set; }
        public string CommandText { get; set; }
        public string Parameters { get; set; }
        public DateTime StartDatetime { get; set; }
        public long TotalDuration { get; set; }
    }

    /// <summary>
    /// Event args for requesting a node to be scrolled into view.
    /// </summary>
    public class NodeScrollEventArgs : EventArgs
    {
        public PlanNodeViewModel Node { get; }

        public NodeScrollEventArgs(PlanNodeViewModel node)
        {
            Node = node;
        }
    }
}
