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

        // Collect SE events in real-time as they arrive via ProcessSingleEvent
        // This ensures we capture them before ProcessResults is triggered by QueryEnd
        private readonly List<TraceStorageEngineEvent> _realtimeSeEvents = new List<TraceStorageEngineEvent>();
        private readonly object _seEventsLock = new object();

        /// <summary>
        /// Event raised after the plan layout is updated, allowing the view to adjust scroll position.
        /// </summary>
        public event System.EventHandler PlanLayoutUpdated;

        /// <summary>
        /// Actual content width based on positioned nodes (not Canvas fixed size).
        /// </summary>
        public double ActualContentWidth { get; private set; }

        /// <summary>
        /// Actual content height based on positioned nodes (not Canvas fixed size).
        /// </summary>
        public double ActualContentHeight { get; private set; }

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

                    // Select new issue and highlight its node
                    if (_selectedIssue != null)
                    {
                        _selectedIssue.IsSelected = true;
                        NavigateToIssue(_selectedIssue);
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
                DaxStudioTraceEventClass.VertiPaqSEQueryEnd  // For timing correlation
            };
        }

        protected override bool IsFinalEvent(DaxStudioTraceEventArgs traceEvent)
        {
            return traceEvent.EventClass == DaxStudioTraceEventClass.QueryEnd ||
                   traceEvent.EventClass == DaxStudioTraceEventClass.Error;
        }

        /// <summary>
        /// Process individual trace events as they arrive in real-time.
        /// This captures SE events before ProcessResults is triggered by QueryEnd.
        /// </summary>
        protected override void ProcessSingleEvent(DaxStudioTraceEventArgs singleEvent)
        {
            base.ProcessSingleEvent(singleEvent);

            // Capture SE events in real-time as they arrive
            if (singleEvent.EventClass == DaxStudioTraceEventClass.VertiPaqSEQueryEnd)
            {
                var seEvent = new TraceStorageEngineEvent
                {
                    Query = singleEvent.TextData,
                    Duration = singleEvent.Duration,
                    CpuTime = singleEvent.CpuTime,
                    NetParallelDuration = singleEvent.NetParallelDuration,
                    Subclass = singleEvent.EventSubclass,
                    StartTime = singleEvent.StartTime,
                    EndTime = singleEvent.EndTime,
                    ObjectName = singleEvent.ObjectName
                };

                // Extract estimated rows and size from xmSQL query text
                ExtractEstimatedSizeFromQuery(seEvent);

                Log.Information(">>> ProcessSingleEvent: Captured SE Event - ObjectName={ObjectName}, Duration={Duration}ms",
                    seEvent.ObjectName, seEvent.Duration);

                lock (_seEventsLock)
                {
                    _realtimeSeEvents.Add(seEvent);
                }
            }
        }

        protected override async void ProcessResults()
        {
            // DEBUG: Log entry point - REMOVE BEFORE RELEASE
            System.Diagnostics.Debug.WriteLine($">>> VisualQueryPlan.ProcessResults() ENTRY - Events.Count={Events.Count}");

            if (HasPlanData)
            {
                // Results have not been cleared, probably from another action
                System.Diagnostics.Debug.WriteLine(">>> VisualQueryPlan: Already has data, skipping");
                return;
            }

            var physicalPlanRows = new List<PhysicalQueryPlanRow>();
            var logicalPlanRows = new List<LogicalQueryPlanRow>();

            // Get SE events captured in real-time via ProcessSingleEvent
            List<TraceStorageEngineEvent> timingEvents;
            lock (_seEventsLock)
            {
                timingEvents = new List<TraceStorageEngineEvent>(_realtimeSeEvents);
            }

            Log.Information(">>> VisualQueryPlan.ProcessResults: Using {Count} SE events captured in real-time", timingEvents.Count);

            int eventCount = 0;
            while (!Events.IsEmpty)
            {
                Events.TryDequeue(out var traceEvent);
                eventCount++;

                // DEBUG: Log each event type - REMOVE BEFORE RELEASE
                Log.Debug(">>> VisualQueryPlan: Processing event {Index} - Class={Class}, Subclass={Subclass}",
                    eventCount, traceEvent.EventClass, traceEvent.EventSubclass);

                if (traceEvent.EventClass == DaxStudioTraceEventClass.DAXQueryPlan)
                {
                    if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.DAXVertiPaqPhysicalPlan)
                    {
                        PhysicalQueryPlanText = traceEvent.TextData;
                        physicalPlanRows.AddRange(ParsePhysicalPlan(traceEvent.TextData));
                        Log.Debug(">>> VisualQueryPlan: Parsed {Count} physical plan rows", physicalPlanRows.Count);
                    }
                    else if (traceEvent.EventSubclass == DaxStudioTraceEventSubclass.DAXVertiPaqLogicalPlan)
                    {
                        LogicalQueryPlanText = traceEvent.TextData;
                        logicalPlanRows.AddRange(ParseLogicalPlan(traceEvent.TextData));
                        Log.Debug(">>> VisualQueryPlan: Parsed {Count} logical plan rows", logicalPlanRows.Count);
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
                    Log.Debug(">>> VisualQueryPlan: QueryEnd - TotalDuration={Duration}ms", TotalDuration);
                }
                // Note: VertiPaqSEQueryEnd events are now captured in ProcessSingleEvent
            }

            // DEBUG: Summary after event processing - REMOVE BEFORE RELEASE
            Log.Information(">>> VisualQueryPlan: Event processing complete - PhysicalRows={Physical}, LogicalRows={Logical}, SEEvents={SE}",
                physicalPlanRows.Count, logicalPlanRows.Count, timingEvents.Count);

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

                    Log.Debug(">>> ProcessResults: Physical plan set - TotalDuration={Total}, SEDuration={SE}, FEDuration={FE}",
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

                    Log.Debug(">>> ProcessResults: Logical plan set - TotalDuration={Total}, SEDuration={SE}, FEDuration={FE}",
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

            // Clear real-time SE events
            lock (_seEventsLock)
            {
                _realtimeSeEvents.Clear();
            }

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
            if (issue == null)
            {
                Log.Debug(">>> NavigateToIssue: issue is null");
                return;
            }

            Log.Debug(">>> NavigateToIssue: Looking for node {NodeId} for issue '{Title}'",
                issue.AffectedNodeId, issue.Title);

            var node = FindNodeById(issue.AffectedNodeId);
            if (node != null)
            {
                Log.Debug(">>> NavigateToIssue: Found node {NodeId}, selecting it", node.NodeId);
                SelectedNode = node;
                // Switch to Node Details tab to show the selected node
                SelectedTabIndex = 0;

                // Scroll the node into view
                ScrollNodeIntoView(node);
            }
            else
            {
                Log.Debug(">>> NavigateToIssue: Node {NodeId} not found in AllNodes (count={Count})",
                    issue.AffectedNodeId, AllNodes.Count);
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
                }

                if (!string.IsNullOrEmpty(LogicalQueryPlanText))
                {
                    var rows = ParseLogicalPlan(LogicalQueryPlanText);
                    _logicalPlan = await _enrichmentService.EnrichLogicalPlanAsync(
                        rows, null, null, ActivityID);
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
        /// </summary>
        private void CollapseSimpleOperators(PlanNodeViewModel node)
        {
            if (node == null) return;

            // Process children first (bottom-up), since we'll be modifying the tree
            foreach (var child in node.Children.ToList())
            {
                CollapseSimpleOperators(child);
            }

            // Now try to collapse this node if it's a simple comparison or arithmetic operation
            node.CollapseIfPossible();
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
            const double horizontalSpacing = 50;
            const double verticalSpacing = 100;
            const double paddingLeft = 30;
            const double paddingTop = 30;

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
            ActualContentWidth = maxX + paddingLeft;
            ActualContentHeight = maxY + paddingTop;
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

        private double PositionNodes(PlanNodeViewModel node, int level, double xOffset,
            double horizontalSpacing, double verticalSpacing, Dictionary<int, int> levelCounts, double paddingTop = 0)
        {
            double y = paddingTop + level * (node.Height + verticalSpacing);

            if (node.Children.Count == 0)
            {
                node.Position = new Point(xOffset, y);
                return xOffset + node.Width + horizontalSpacing;
            }

            double childOffset = xOffset;
            foreach (var child in node.Children)
            {
                childOffset = PositionNodes(child, level + 1, childOffset, horizontalSpacing, verticalSpacing, levelCounts, paddingTop);
            }

            // Center parent over children
            double firstChildX = node.Children.First().X;
            double lastChildX = node.Children.Last().X + node.Children.Last().Width;
            double centerX = (firstChildX + lastChildX) / 2 - node.Width / 2;

            node.Position = new Point(centerX, y);

            return Math.Max(childOffset, centerX + node.Width + horizontalSpacing);
        }

        private PlanNodeViewModel FindNodeById(int nodeId)
        {
            return AllNodes.FirstOrDefault(n => n.NodeId == nodeId);
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
