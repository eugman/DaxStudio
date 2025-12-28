# Research: Visual Query Plan

**Feature**: Visual Query Plan | **Date**: 2025-12-22

## Research Summary

This document captures research findings for implementing graphical DAX query plan visualization in DaxStudio.

---

## 1. Graph Rendering Library Selection

### Decision: Microsoft Automatic Graph Layout (MSAGL)

**Rationale**: MSAGL is the best fit for DAX execution plan visualization because:
1. Professional-grade hierarchical layout algorithms (Sugiyama) specifically designed for DAGs
2. Proven performance with 15,000+ edges in production
3. Open source with active Microsoft maintenance
4. Built-in WPF control with pan, zoom, tooltips, and node selection
5. Perfect match for SQL Server-style execution plan visualization

### Alternatives Considered

| Library | Pros | Cons | Verdict |
|---------|------|------|---------|
| **MSAGL** | Professional DAG layout, proven scale, WPF control | Heavier weight | **SELECTED** |
| **Nodify** | 60fps at 200+ nodes, excellent MVVM | No auto-layout | Requires separate layout engine |
| **GraphShape** | Multiple layout algorithms | Less optimized for large graphs | Good for layout-only |
| **Custom Canvas** | Full control, no dependencies | High development effort | Fallback option |

### Implementation Approach

```
Primary: MSAGL WpfGraphControl via NuGet
  - AutomaticGraphLayout.WpfGraphControl
  - Use SugiyamaLayoutSettings for hierarchical/tree layout
  - Custom node templates for cost visualization
```

### Integration with DaxStudio

- Extend existing `ZoomableUserControl` pattern
- Use Caliburn.Micro conventions for ViewModel binding
- Apply DaxStudio theme resources to node templates

---

## 2. Column ID Resolution

### Decision: Use existing ADOTabular infrastructure

**Rationale**: DaxStudio already has comprehensive column metadata resolution.

### Key Integration Points

```csharp
// Column resolution flow
ADOTabularColumnCollection.GetByPropertyRef(string columnId)
  → Returns ADOTabularColumn with:
     - Name (logical name)
     - Caption (display name)
     - DaxName (fully qualified)
     - Table reference
     - DataType
```

### Data Structures

- `ADOTabularColumn`: Column metadata with InternalReference ↔ Name mapping
- `ADOTabularColumnCollection`: Dictionary-based lookup by reference ID
- `DaxColumnsRemap`: Column remapping for xmSQL queries

### Resolution Strategy

1. Parse column IDs from plan operation strings using regex
2. Extract patterns like `'TableName'[ColumnName]` or bare column IDs
3. Resolve via `GetByPropertyRef()` to get human-readable names
4. Cache resolved names to avoid repeated lookups

---

## 3. View Metrics / Timing Data Correlation

### Decision: Correlate via ActivityID and time windows

**Rationale**: All trace events share ActivityID for correlation.

### Timing Data Sources

| Source | Data Available | Correlation Method |
|--------|---------------|-------------------|
| `ServerTimesModel` | SE/FE duration split, CPU time | ActivityID |
| `TraceStorageEngineEvent` | Individual SE query timing | ActivityID + ObjectName |
| `ExecutionMetricsTraceEngineEvent` | JSON metrics (Ms, KB, Rows) | ActivityID |
| `QueryEnd` event | Total query duration | ActivityID |

### Key Metrics for Plan Node Enrichment

```csharp
// From TraceStorageEngineEvent
Duration              // Event duration in ms
NetParallelDuration   // Adjusted for parallelism
CpuTime              // CPU time
EstimatedRows        // Row cardinality
StartTime/EndTime    // Timing coordinates

// From ServerTimesModel
StorageEngineDuration           // Total SE time
FormulaEngineDuration           // Total FE time
StorageEngineNetParallelDuration // Parallel-adjusted SE
VertipaqCacheMatches            // Cache hit count
```

### Correlation Flow

```
QueryBegin (ActivityID=ABC, StartTime=T1)
  ↓
DAXQueryPlan (ActivityID=ABC) → Parse plan nodes
  ↓
[VertiPaqSEQueryEnd events] (ActivityID=ABC) → Match to plan nodes via ObjectName
  ↓
ExecutionMetrics (ActivityID=ABC) → Extract JSON metrics
  ↓
QueryEnd (ActivityID=ABC) → Total duration
```

---

## 4. Performance Anti-Pattern Detection

### Decision: Regex-based detection in plan operation strings

### Excessive Materialization Detection

**Pattern indicators in physical plan:**
- Look for `SpoolLookup` or `Spool` operations with high `#Records`
- Check for repeated scans of same table
- Detect `CallbackDataID` operations

```csharp
// Regex patterns for detection
const string ExcessiveMaterializationPattern = @"SpoolLookup.*#Records=(\d+)";
const string CallbackDataIdPattern = @"CallbackDataID";
const string HighCardinalityThreshold = 100000; // Configurable
```

### Detection Strategy

1. **Pass 1**: Parse all plan nodes, extract metrics
2. **Pass 2**: Apply detection rules to identify anti-patterns
3. **Pass 3**: Annotate affected nodes with issue details

### Issue Severity Levels

| Level | Indicator | Example |
|-------|-----------|---------|
| Error | Excessive Materialization > 1M rows | Red border |
| Warning | CallbackDataID present | Yellow border |
| Info | High cardinality (future) | Blue indicator |

---

## 5. Plan Node Data Model

### Decision: Create EnrichedPlanNode extending existing QueryPlanRow

### Proposed Structure

```csharp
public class EnrichedPlanNode : PhysicalQueryPlanRow
{
    // Inherited: Operation, Level, RowNumber, Records, HighlightRow

    // Enrichment data
    public string ResolvedOperation { get; set; }    // Column IDs resolved
    public long? DurationMs { get; set; }            // From trace correlation
    public long? CpuTimeMs { get; set; }
    public double? CostPercentage { get; set; }      // Relative to total
    public EngineType Engine { get; set; }           // SE or FE

    // Anti-pattern flags
    public List<PerformanceIssue> Issues { get; set; }
    public bool HasWarning => Issues?.Any() ?? false;

    // Graph layout
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    // Relationships
    public EnrichedPlanNode Parent { get; set; }
    public List<EnrichedPlanNode> Children { get; set; }
}
```

---

## 6. UI Layout Decisions

### Decision: Dockable tool window with tabbed Physical/Logical views

**Layout Structure:**
```
┌─────────────────────────────────────────────────────────────┐
│ [Record] [Pause] [Stop] [Clear] [Export] [Info] │ [Phys▼] │
├─────────────────────────────────────────────────────────────┤
│                                                             │
│                   Graph Visualization Area                  │
│                   (Pan/Zoom/Select)                         │
│                                                             │
├─────────────────────────────────────────────────────────────┤
│ Issues: ⚠ 2 warnings detected                              │
├─────────────────────────────────────────────────────────────┤
│ Node Details Panel (collapsible)                            │
│ - Operator: [name]                                          │
│ - Duration: [X ms] (Y% of total)                           │
│ - Rows: [count]                                             │
│ - Issues: [list]                                            │
└─────────────────────────────────────────────────────────────┘
```

### Interaction Patterns

- **Click node**: Select and show details
- **Double-click**: Expand/collapse subtree
- **Ctrl+MouseWheel**: Zoom (existing ZoomableUserControl)
- **Middle-drag** or **Space+drag**: Pan
- **Arrow keys**: Navigate between nodes
- **Escape**: Clear selection

---

## 7. Open Questions Resolved

| Question | Resolution |
|----------|------------|
| Graph library | MSAGL WpfGraphControl |
| Column resolution | ADOTabularColumnCollection.GetByPropertyRef() |
| Timing correlation | ActivityID-based matching |
| Anti-pattern detection | Regex on operation strings + threshold rules |
| UI location | Dockable tool window (per spec clarification) |
| Initial scope | Excessive Materialization + CallbackDataID only |

---

## 8. Dependencies

### NuGet Packages Required

```xml
<PackageReference Include="AutomaticGraphLayout.WpfGraphControl" Version="1.1.12" />
<!-- May also need: -->
<PackageReference Include="AutomaticGraphLayout" Version="1.1.12" />
```

### Existing DaxStudio Dependencies (no changes needed)

- Caliburn.Micro (MVVM)
- Newtonsoft.Json (serialization)
- ModernWpf (theming)

---

## References

- [MSAGL GitHub](https://github.com/microsoft/automatic-graph-layout)
- [Nodify GitHub](https://github.com/miroiu/nodify)
- [GraphShape GitHub](https://github.com/KeRNeLith/GraphShape)
- DaxStudio source: `src/DaxStudio.UI/ViewModels/QueryPlanTraceViewModel.cs`
- DaxStudio source: `src/ADOTabular/ADOTabularColumn.cs`
- DaxStudio source: `src/DaxStudio.UI/ViewModels/ServerTimesViewModel.cs`
