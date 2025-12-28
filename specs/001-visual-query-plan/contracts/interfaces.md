# Interface Contracts: Visual Query Plan

**Feature**: Visual Query Plan | **Date**: 2025-12-22

## Service Interfaces

### IPlanEnrichmentService

Enriches raw query plan data with timing, column names, and issue detection.

```csharp
namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for enriching raw query plan data with timing metrics,
    /// resolved column names, and detected performance issues.
    /// </summary>
    public interface IPlanEnrichmentService
    {
        /// <summary>
        /// Enriches a physical query plan with timing and metadata.
        /// </summary>
        /// <param name="rawPlan">Raw physical plan rows from trace</param>
        /// <param name="timingEvents">Storage engine timing events</param>
        /// <param name="columnResolver">Column name resolver</param>
        /// <param name="activityId">Activity ID for correlation</param>
        /// <returns>Fully enriched query plan</returns>
        Task<EnrichedQueryPlan> EnrichPhysicalPlanAsync(
            IEnumerable<PhysicalQueryPlanRow> rawPlan,
            IEnumerable<TraceStorageEngineEvent> timingEvents,
            IColumnNameResolver columnResolver,
            string activityId);

        /// <summary>
        /// Enriches a logical query plan with metadata.
        /// </summary>
        Task<EnrichedQueryPlan> EnrichLogicalPlanAsync(
            IEnumerable<LogicalQueryPlanRow> rawPlan,
            IColumnNameResolver columnResolver,
            string activityId);
    }
}
```

---

### IColumnNameResolver

Resolves column internal references to human-readable names.

```csharp
namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for resolving column internal IDs to display names.
    /// </summary>
    public interface IColumnNameResolver
    {
        /// <summary>
        /// Resolves a column internal reference to its display name.
        /// </summary>
        /// <param name="columnRef">Internal column reference/ID</param>
        /// <returns>Resolved column name, or original if not found</returns>
        string ResolveColumnName(string columnRef);

        /// <summary>
        /// Resolves all column references in an operation string.
        /// </summary>
        /// <param name="operation">Raw operation string with column IDs</param>
        /// <returns>Operation string with resolved column names</returns>
        string ResolveOperationString(string operation);

        /// <summary>
        /// Initializes resolver with column metadata from current connection.
        /// </summary>
        /// <param name="columns">Column collection from ADOTabular</param>
        void Initialize(ADOTabularColumnCollection columns);

        /// <summary>
        /// Gets whether the resolver has been initialized.
        /// </summary>
        bool IsInitialized { get; }
    }
}
```

---

### IPerformanceIssueDetector

Detects performance anti-patterns in query plans.

```csharp
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
    }
}
```

---

### IPlanLayoutService

Calculates graph layout positions for plan nodes.

```csharp
namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for calculating graph layout positions.
    /// </summary>
    public interface IPlanLayoutService
    {
        /// <summary>
        /// Calculates layout positions for all nodes in a plan.
        /// </summary>
        /// <param name="plan">Plan with nodes to layout</param>
        /// <param name="settings">Layout configuration</param>
        /// <returns>Plan with X, Y, Width, Height populated on nodes</returns>
        Task<EnrichedQueryPlan> CalculateLayoutAsync(
            EnrichedQueryPlan plan,
            PlanLayoutSettings settings);
    }

    /// <summary>
    /// Configuration for plan graph layout.
    /// </summary>
    public class PlanLayoutSettings
    {
        /// <summary>
        /// Direction of data flow. Default: TopToBottom.
        /// </summary>
        public LayoutDirection Direction { get; set; } = LayoutDirection.TopToBottom;

        /// <summary>
        /// Minimum node width.
        /// </summary>
        public double MinNodeWidth { get; set; } = 120;

        /// <summary>
        /// Maximum node width.
        /// </summary>
        public double MaxNodeWidth { get; set; } = 300;

        /// <summary>
        /// Node height.
        /// </summary>
        public double NodeHeight { get; set; } = 40;

        /// <summary>
        /// Horizontal spacing between nodes.
        /// </summary>
        public double HorizontalSpacing { get; set; } = 40;

        /// <summary>
        /// Vertical spacing between levels.
        /// </summary>
        public double VerticalSpacing { get; set; } = 60;
    }

    public enum LayoutDirection
    {
        TopToBottom,
        LeftToRight
    }
}
```

---

## ViewModel Interfaces

### IVisualQueryPlanViewModel

Main ViewModel for the visual query plan tool window.

```csharp
namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// ViewModel for the visual query plan visualization.
    /// </summary>
    public interface IVisualQueryPlanViewModel : ITraceWatcher
    {
        /// <summary>
        /// The currently displayed enriched plan.
        /// </summary>
        EnrichedQueryPlan CurrentPlan { get; }

        /// <summary>
        /// Current plan type being displayed.
        /// </summary>
        PlanType CurrentPlanType { get; set; }

        /// <summary>
        /// Currently selected node.
        /// </summary>
        EnrichedPlanNode SelectedNode { get; set; }

        /// <summary>
        /// All detected performance issues.
        /// </summary>
        IReadOnlyList<PerformanceIssue> Issues { get; }

        /// <summary>
        /// Current zoom level (1.0 = 100%).
        /// </summary>
        double ZoomLevel { get; set; }

        /// <summary>
        /// Whether the plan has any warnings or errors.
        /// </summary>
        bool HasIssues { get; }

        /// <summary>
        /// Toggles between physical and logical plan views.
        /// </summary>
        void TogglePlanType();

        /// <summary>
        /// Zooms to fit the entire plan in view.
        /// </summary>
        void ZoomToFit();

        /// <summary>
        /// Navigates to a specific node.
        /// </summary>
        void NavigateToNode(int nodeId);

        /// <summary>
        /// Expands or collapses a node's subtree.
        /// </summary>
        void ToggleNodeExpansion(EnrichedPlanNode node);
    }
}
```

---

### IPlanNodeDetailsViewModel

ViewModel for the node details panel.

```csharp
namespace DaxStudio.UI.ViewModels
{
    /// <summary>
    /// ViewModel for displaying selected node details.
    /// </summary>
    public interface IPlanNodeDetailsViewModel
    {
        /// <summary>
        /// The node being displayed.
        /// </summary>
        EnrichedPlanNode Node { get; set; }

        /// <summary>
        /// Formatted operation name.
        /// </summary>
        string OperationName { get; }

        /// <summary>
        /// Formatted duration string.
        /// </summary>
        string FormattedDuration { get; }

        /// <summary>
        /// Formatted row count.
        /// </summary>
        string FormattedRecords { get; }

        /// <summary>
        /// Cost percentage display.
        /// </summary>
        string FormattedCost { get; }

        /// <summary>
        /// Engine type display (SE/FE).
        /// </summary>
        string EngineTypeDisplay { get; }

        /// <summary>
        /// Issues affecting this node.
        /// </summary>
        IReadOnlyList<PerformanceIssue> NodeIssues { get; }

        /// <summary>
        /// Whether to show cache hit indicator.
        /// </summary>
        bool ShowCacheHit { get; }

        /// <summary>
        /// Full operation text for detailed view.
        /// </summary>
        string FullOperationText { get; }
    }
}
```

---

## Event Contracts

### Plan Events

```csharp
namespace DaxStudio.UI.Events
{
    /// <summary>
    /// Published when a new enriched plan is ready for display.
    /// </summary>
    public class EnrichedPlanReadyEvent
    {
        public EnrichedQueryPlan Plan { get; set; }
        public string ActivityId { get; set; }
    }

    /// <summary>
    /// Published when a plan node is selected.
    /// </summary>
    public class PlanNodeSelectedEvent
    {
        public EnrichedPlanNode Node { get; set; }
        public int NodeId { get; set; }
    }

    /// <summary>
    /// Request to highlight DAX text corresponding to a node.
    /// </summary>
    public class HighlightDaxTextEvent
    {
        public string DaxExpression { get; set; }
        public int StartOffset { get; set; }
        public int Length { get; set; }
    }
}
```

---

## Data Contracts

### Serialization Format

```csharp
namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Serializable model for enriched query plans.
    /// </summary>
    [JsonObject]
    public class EnrichedQueryPlanModel
    {
        [JsonProperty("fileFormatVersion")]
        public int FileFormatVersion { get; set; } = 4;

        [JsonProperty("planType")]
        public PlanType PlanType { get; set; }

        [JsonProperty("activityId")]
        public string ActivityId { get; set; }

        [JsonProperty("requestId")]
        public string RequestId { get; set; }

        [JsonProperty("queryText")]
        public string QueryText { get; set; }

        [JsonProperty("parameters")]
        public string Parameters { get; set; }

        [JsonProperty("startDateTime")]
        public DateTime StartDateTime { get; set; }

        [JsonProperty("totalDurationMs")]
        public long TotalDurationMs { get; set; }

        [JsonProperty("storageEngineDurationMs")]
        public long StorageEngineDurationMs { get; set; }

        [JsonProperty("formulaEngineDurationMs")]
        public long FormulaEngineDurationMs { get; set; }

        [JsonProperty("nodes")]
        public List<EnrichedPlanNodeModel> Nodes { get; set; }

        [JsonProperty("issues")]
        public List<PerformanceIssueModel> Issues { get; set; }
    }

    /// <summary>
    /// Serializable model for plan nodes.
    /// </summary>
    [JsonObject]
    public class EnrichedPlanNodeModel
    {
        [JsonProperty("nodeId")]
        public int NodeId { get; set; }

        [JsonProperty("rowNumber")]
        public int RowNumber { get; set; }

        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("operation")]
        public string Operation { get; set; }

        [JsonProperty("resolvedOperation")]
        public string ResolvedOperation { get; set; }

        [JsonProperty("records")]
        public long? Records { get; set; }

        [JsonProperty("durationMs")]
        public long? DurationMs { get; set; }

        [JsonProperty("cpuTimeMs")]
        public long? CpuTimeMs { get; set; }

        [JsonProperty("costPercentage")]
        public double? CostPercentage { get; set; }

        [JsonProperty("engineType")]
        public EngineType EngineType { get; set; }

        [JsonProperty("isCacheHit")]
        public bool IsCacheHit { get; set; }

        [JsonProperty("parentNodeId")]
        public int? ParentNodeId { get; set; }

        [JsonProperty("childNodeIds")]
        public List<int> ChildNodeIds { get; set; }

        [JsonProperty("issueIds")]
        public List<Guid> IssueIds { get; set; }
    }
}
```
