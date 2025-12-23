using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Text;
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
    [Export(typeof(ITraceWatcher)), PartCreationPolicy(CreationPolicy.NonShared)]
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
                DaxStudioTraceEventClass.QueryEnd
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
            }

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

                    _physicalPlan.TotalDurationMs = TotalDuration;
                }

                if (logicalPlanRows.Count > 0)
                {
                    _logicalPlan = await _enrichmentService.EnrichLogicalPlanAsync(
                        logicalPlanRows,
                        null,
                        ActivityID);
                }

                // Update the displayed plan
                UpdateDisplayedPlan();

                // Update issues - wrap in IssueViewModel for UI display
                Issues.Clear();
                if (_physicalPlan?.Issues != null)
                {
                    foreach (var issue in _physicalPlan.Issues)
                    {
                        Issues.Add(new IssueViewModel(issue));
                    }
                }

                NotifyOfPropertyChange(nameof(HasIssues));
                NotifyOfPropertyChange(nameof(IssueCount));
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
        }

        public void NavigateToIssue(IssueViewModel issue)
        {
            if (issue == null) return;

            var node = FindNodeById(issue.AffectedNodeId);
            if (node != null)
            {
                SelectedNode = node;
            }
        }

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
                        rows, null, ActivityID);
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

        private void UpdateDisplayedPlan()
        {
            var plan = ShowPhysicalPlan ? _physicalPlan : _logicalPlan;
            if (plan == null)
            {
                RootNode = null;
                AllNodes.Clear();
                return;
            }

            RootNode = PlanNodeViewModel.BuildTree(plan);
            AllNodes.Clear();

            if (RootNode != null)
            {
                CollectAllNodes(RootNode, AllNodes);
                CalculateLayout();
            }

            NotifyOfPropertyChange(nameof(HasPlanData));
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
            if (RootNode == null) return;

            // Simple top-down tree layout
            const double horizontalSpacing = 200;
            const double verticalSpacing = 80;

            var levelWidths = new Dictionary<int, double>();
            var levelCounts = new Dictionary<int, int>();

            // Calculate level widths
            CalculateLevelInfo(RootNode, 0, levelWidths, levelCounts);

            // Position nodes
            PositionNodes(RootNode, 0, 0, horizontalSpacing, verticalSpacing, levelCounts);
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
            double horizontalSpacing, double verticalSpacing, Dictionary<int, int> levelCounts)
        {
            double y = level * (node.Height + verticalSpacing);

            if (node.Children.Count == 0)
            {
                node.Position = new Point(xOffset, y);
                return xOffset + node.Width + horizontalSpacing;
            }

            double childOffset = xOffset;
            foreach (var child in node.Children)
            {
                childOffset = PositionNodes(child, level + 1, childOffset, horizontalSpacing, verticalSpacing, levelCounts);
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
}
