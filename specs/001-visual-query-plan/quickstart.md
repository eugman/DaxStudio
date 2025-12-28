# Developer Quickstart: Visual Query Plan

**Feature**: Visual Query Plan | **Date**: 2025-12-22

## Prerequisites

- Visual Studio 2022
- .NET Framework 4.7.2+ SDK
- DaxStudio source code cloned
- NuGet restore completed

## Quick Setup

### 1. Add NuGet Package

Add MSAGL to `DaxStudio.UI.csproj`:

```xml
<PackageReference Include="AutomaticGraphLayout.WpfGraphControl" Version="1.1.12" />
```

### 2. Create Core Files

Create these files in `src/DaxStudio.UI/`:

```
ViewModels/
  VisualQueryPlanViewModel.cs
  PlanNodeDetailsViewModel.cs

Views/
  VisualQueryPlanView.xaml
  VisualQueryPlanView.xaml.cs
  PlanNodeDetailsView.xaml
  PlanNodeDetailsView.xaml.cs

Model/
  EnrichedPlanNode.cs
  EnrichedQueryPlan.cs
  PerformanceIssue.cs

Services/
  PlanEnrichmentService.cs
  ColumnNameResolver.cs
  PerformanceIssueDetector.cs
  PlanLayoutService.cs
```

### 3. Register with MEF

In `VisualQueryPlanViewModel.cs`, add export attribute:

```csharp
[Export(typeof(ITraceWatcher))]
[PartCreationPolicy(CreationPolicy.NonShared)]
public class VisualQueryPlanViewModel : TraceWatcherBaseViewModel
{
    // ...
}
```

### 4. Build & Run

```bash
cd src
msbuild DaxStudio.sln /t:Rebuild
```

Launch `DaxStudio.Standalone` and look for "Visual Query Plan" in trace windows.

---

## Key Implementation Patterns

### Following Existing TraceWatcher Pattern

Reference `QueryPlanTraceViewModel.cs` for the established pattern:

```csharp
public class VisualQueryPlanViewModel : TraceWatcherBaseViewModel,
    ISaveState,
    ITraceDiagnostics,
    IHaveData
{
    [ImportingConstructor]
    public VisualQueryPlanViewModel(
        IEventAggregator eventAggregator,
        IGlobalOptions globalOptions,
        IWindowManager windowManager)
        : base(eventAggregator, globalOptions, windowManager)
    {
    }

    protected override List<DaxStudioTraceEventClass> GetMonitoredEvents()
    {
        return new List<DaxStudioTraceEventClass>
        {
            DaxStudioTraceEventClass.DAXQueryPlan,
            DaxStudioTraceEventClass.QueryBegin,
            DaxStudioTraceEventClass.QueryEnd,
            DaxStudioTraceEventClass.VertiPaqSEQueryEnd // For timing
        };
    }

    protected override void ProcessResults()
    {
        // Build enriched plan from captured events
    }

    // IToolWindow properties
    public override string Title => "Visual Query Plan";
    public override string ContentId => "visual-query-plan";
}
```

### Using MSAGL for Graph Layout

```csharp
using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;

// Create graph
var graph = new Graph();

// Add nodes
foreach (var node in plan.AllNodes)
{
    var n = graph.AddNode(node.NodeId.ToString());
    n.LabelText = node.OperationName;
    n.Attr.FillColor = GetColorForCost(node.CostPercentage);
}

// Add edges
foreach (var node in plan.AllNodes.Where(n => n.Parent != null))
{
    graph.AddEdge(node.Parent.NodeId.ToString(), node.NodeId.ToString());
}

// Configure layout
graph.LayoutAlgorithmSettings = new SugiyamaLayoutSettings
{
    Transformation = PlaneTransformation.Rotation(Math.PI / 2), // Top-to-bottom
    NodeSeparation = 40,
    LayerSeparation = 60
};

// Display in WPF control
graphControl.Graph = graph;
```

### Column Name Resolution

```csharp
public class ColumnNameResolver : IColumnNameResolver
{
    private readonly Dictionary<string, string> _cache = new();
    private ADOTabularColumnCollection _columns;

    public string ResolveColumnName(string columnRef)
    {
        if (_cache.TryGetValue(columnRef, out var name))
            return name;

        try
        {
            var column = _columns.GetByPropertyRef(columnRef);
            name = $"'{column.Table.Name}'[{column.Name}]";
            _cache[columnRef] = name;
            return name;
        }
        catch
        {
            return columnRef; // Return original if not found
        }
    }
}
```

### Issue Detection

```csharp
public class PerformanceIssueDetector : IPerformanceIssueDetector
{
    private static readonly Regex SpoolPattern =
        new(@"Spool(Lookup)?.*#Records=(\d+)", RegexOptions.Compiled);

    private static readonly Regex CallbackPattern =
        new(@"CallbackDataID", RegexOptions.Compiled);

    public IReadOnlyList<PerformanceIssue> DetectNodeIssues(EnrichedPlanNode node)
    {
        var issues = new List<PerformanceIssue>();

        // Check for excessive materialization
        var spoolMatch = SpoolPattern.Match(node.Operation);
        if (spoolMatch.Success && long.TryParse(spoolMatch.Groups[2].Value, out var rows))
        {
            if (rows > Settings.ExcessiveMaterializationErrorThreshold)
            {
                issues.Add(new PerformanceIssue
                {
                    IssueType = IssueType.ExcessiveMaterialization,
                    Severity = IssueSeverity.Error,
                    Description = $"Spool materialized {rows:N0} rows",
                    MetricValue = rows
                });
            }
        }

        // Check for CallbackDataID
        if (CallbackPattern.IsMatch(node.Operation))
        {
            issues.Add(new PerformanceIssue
            {
                IssueType = IssueType.CallbackDataID,
                Severity = IssueSeverity.Warning,
                Description = "CallbackDataID operation may cause row-by-row processing"
            });
        }

        return issues;
    }
}
```

---

## Testing

### Unit Test Setup

```csharp
public class PlanEnrichmentServiceTests
{
    private readonly IPlanEnrichmentService _service;

    public PlanEnrichmentServiceTests()
    {
        _service = new PlanEnrichmentService(
            new ColumnNameResolver(),
            new PerformanceIssueDetector());
    }

    [Fact]
    public async Task EnrichPhysicalPlan_WithValidData_ReturnsEnrichedPlan()
    {
        // Arrange
        var rawPlan = LoadTestPlan("SamplePhysicalPlan.json");

        // Act
        var result = await _service.EnrichPhysicalPlanAsync(
            rawPlan, timingEvents: [], resolver: null, activityId: "test");

        // Assert
        Assert.NotNull(result);
        Assert.True(result.AllNodes.Count > 0);
    }
}
```

### Sample Test Data

Create `tests/DaxStudio.Tests/TestData/SampleQueryPlans/`:

```json
// SimplePhysicalPlan.json
{
  "PhysicalQueryPlanRows": [
    { "RowNumber": 1, "Level": 0, "Operation": "AddColumns: RelLogOp" },
    { "RowNumber": 2, "Level": 1, "Operation": "Scan_Vertipaq: #Records=1000" }
  ]
}
```

---

## Debug Tips

1. **Enable trace logging**: Set Serilog to Debug level in `App.config`
2. **Capture raw plans**: Use existing Query Plan pane to see raw data
3. **Test with known queries**: Use SSAS sample databases (AdventureWorks)
4. **Check ActivityID matching**: Log ActivityIDs to verify correlation

## Common Issues

| Issue | Solution |
|-------|----------|
| Graph not rendering | Check MSAGL NuGet package version compatibility |
| Column names not resolving | Verify ADOTabular connection is active |
| No timing data | Ensure Server Timings trace is also enabled |
| Layout too wide | Adjust `SugiyamaLayoutSettings.NodeSeparation` |

---

## Next Steps

1. Implement `VisualQueryPlanViewModel` following TraceWatcher pattern
2. Create XAML view with MSAGL `GraphControl`
3. Add enrichment pipeline services
4. Write unit tests for each service
5. Manual testing with sample DAX queries
