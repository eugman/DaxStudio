# Data Model: Visual Query Plan

**Feature**: Visual Query Plan | **Date**: 2025-12-22

## Entity Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        EnrichedQueryPlan                            │
│  - ActivityID, RequestID                                            │
│  - QueryText, Parameters                                            │
│  - TotalDuration, SEDuration, FEDuration                           │
│  - PlanType (Physical/Logical)                                      │
├─────────────────────────────────────────────────────────────────────┤
│                              │ 1                                    │
│                              │                                      │
│                              ▼ *                                    │
│                     EnrichedPlanNode                                │
│  - NodeId, RowNumber, Level                                        │
│  - Operation, ResolvedOperation                                     │
│  - Records, DurationMs, CpuTimeMs                                  │
│  - CostPercentage, EngineType                                       │
│  - X, Y, Width, Height (layout)                                     │
├─────────────────────────────────────────────────────────────────────┤
│               │ *                              │ 0..*               │
│               ▼                                ▼                    │
│     EnrichedPlanNode                  PerformanceIssue             │
│     (Parent/Children)                 - IssueType, Severity        │
│                                       - Description, Remediation   │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Entity Definitions

### EnrichedQueryPlan

Represents a complete enriched execution plan with aggregated metrics.

| Field | Type | Description |
|-------|------|-------------|
| ActivityID | string | Trace correlation identifier |
| RequestID | string | Request correlation identifier |
| QueryText | string | Original DAX query text |
| Parameters | string | Query parameters |
| StartDateTime | DateTime | Query start timestamp |
| PlanType | PlanType | Physical or Logical |
| TotalDurationMs | long | Total query duration |
| StorageEngineDurationMs | long | SE time component |
| FormulaEngineDurationMs | long | FE time component |
| StorageEngineCpuMs | long | SE CPU time |
| CacheHits | int | VertiPaq cache matches |
| RootNode | EnrichedPlanNode | Plan tree root |
| AllNodes | List\<EnrichedPlanNode\> | Flattened node list |
| Issues | List\<PerformanceIssue\> | All detected issues |

**Validation Rules:**
- ActivityID must not be null/empty
- TotalDurationMs >= 0
- StorageEngineDurationMs + FormulaEngineDurationMs <= TotalDurationMs (approximately)

**State Transitions:**
- `Raw` → `Parsed` → `Enriched` → `LayoutComplete`

---

### EnrichedPlanNode

Represents a single operator in the plan with enriched metrics and layout data.

| Field | Type | Description |
|-------|------|-------------|
| NodeId | int | Unique identifier within plan |
| RowNumber | int | Line number in original plan text |
| Level | int | Tree depth (0 = root) |
| Operation | string | Raw operation text |
| ResolvedOperation | string | Operation with column IDs resolved to names |
| Records | long? | Estimated row count |
| DurationMs | long? | Operation duration (from trace correlation) |
| CpuTimeMs | long? | CPU time (from trace correlation) |
| CostPercentage | double? | Percentage of total query cost |
| EngineType | EngineType | StorageEngine, FormulaEngine, or Unknown |
| IsCacheHit | bool | Whether operation hit VertiPaq cache |
| ObjectName | string? | Referenced table/column name |
| Parent | EnrichedPlanNode | Parent node reference |
| Children | List\<EnrichedPlanNode\> | Child node references |
| Issues | List\<PerformanceIssue\> | Issues affecting this node |
| X | double | Layout X position |
| Y | double | Layout Y position |
| Width | double | Node visual width |
| Height | double | Node visual height |
| IsExpanded | bool | Subtree expansion state |
| IsSelected | bool | Selection state |
| NextSiblingRowNumber | int | For tree traversal |

**Computed Properties:**
- `HasIssues` → Issues.Count > 0
- `IsWarning` → Issues.Any(i => i.Severity == Warning)
- `IsError` → Issues.Any(i => i.Severity == Error)
- `DisplayDuration` → Formatted duration string
- `DisplayRecords` → Formatted record count

**Validation Rules:**
- NodeId must be unique within plan
- Level >= 0
- Records >= 0 if not null
- Width and Height > 0 for rendered nodes

---

### PerformanceIssue

Represents a detected performance anti-pattern.

| Field | Type | Description |
|-------|------|-------------|
| IssueId | Guid | Unique identifier |
| IssueType | IssueType | ExcessiveMaterialization, CallbackDataID, etc. |
| Severity | IssueSeverity | Info, Warning, Error |
| AffectedNodeId | int | Node where issue was detected |
| Description | string | Human-readable description |
| Remediation | string | Suggested fix |
| MetricValue | long? | Quantified metric (e.g., row count) |
| Threshold | long? | Threshold that was exceeded |

**Issue Types (initial scope):**
- `ExcessiveMaterialization` - Spool/SpoolLookup with high row counts
- `CallbackDataID` - CallbackDataID operation detected

**Severity Levels:**
- `Info` - Informational, may not require action
- `Warning` - Potential issue, should investigate
- `Error` - Definite problem, requires attention

---

### Enumerations

```csharp
public enum PlanType
{
    Physical,
    Logical
}

public enum EngineType
{
    Unknown,
    StorageEngine,
    FormulaEngine
}

public enum IssueType
{
    ExcessiveMaterialization,
    CallbackDataID
    // Future: HighCardinality, UnnecessaryScan, FilterPushdownFailure
}

public enum IssueSeverity
{
    Info,
    Warning,
    Error
}

public enum PlanState
{
    Raw,
    Parsed,
    Enriched,
    LayoutComplete
}
```

---

## Relationships

### EnrichedQueryPlan ← 1:N → EnrichedPlanNode
- Plan contains hierarchy of nodes
- Nodes reference parent plan via back-pointer
- Tree structure: each node has 0-1 parent, 0-N children

### EnrichedPlanNode ← 0:N → PerformanceIssue
- Node can have zero or more issues
- Issue references single affected node
- Issues also aggregated at plan level

---

## Data Flow

### Enrichment Pipeline

```
1. RAW PLAN INPUT
   PhysicalQueryPlanRow[] (existing)
   LogicalQueryPlanRow[] (existing)
   ↓
2. PARSE & BUILD TREE
   Convert flat list to tree structure
   Calculate parent/child relationships
   ↓
3. COLUMN RESOLUTION
   Extract column IDs from Operation strings
   Resolve via ADOTabularColumnCollection
   Update ResolvedOperation
   ↓
4. TIMING CORRELATION
   Match ActivityID to TraceStorageEngineEvents
   Assign DurationMs, CpuTimeMs to nodes
   Calculate CostPercentage
   ↓
5. ISSUE DETECTION
   Apply regex patterns for anti-patterns
   Create PerformanceIssue instances
   Attach to affected nodes
   ↓
6. LAYOUT CALCULATION
   Run MSAGL Sugiyama algorithm
   Assign X, Y, Width, Height
   ↓
7. ENRICHED OUTPUT
   EnrichedQueryPlan ready for visualization
```

---

## Serialization

### JSON Format (extends existing .queryPlans)

```json
{
  "FileFormatVersion": 4,
  "PlanType": "Physical",
  "ActivityID": "abc-123",
  "RequestID": "def-456",
  "QueryText": "EVALUATE ...",
  "TotalDurationMs": 1250,
  "StorageEngineDurationMs": 800,
  "FormulaEngineDurationMs": 450,
  "Nodes": [
    {
      "NodeId": 1,
      "RowNumber": 1,
      "Level": 0,
      "Operation": "AddColumns: RelLogOp DependOnCols()(...",
      "ResolvedOperation": "AddColumns: RelLogOp DependOnCols(Sales[Amount])...",
      "Records": 50000,
      "DurationMs": 120,
      "CostPercentage": 9.6,
      "EngineType": "FormulaEngine",
      "ChildNodeIds": [2, 3],
      "Issues": []
    }
  ],
  "Issues": [
    {
      "IssueId": "guid",
      "IssueType": "ExcessiveMaterialization",
      "Severity": "Warning",
      "AffectedNodeId": 5,
      "Description": "Spool operation materialized 1.2M rows",
      "Remediation": "Consider restructuring query to avoid intermediate materialization"
    }
  ]
}
```

### Backward Compatibility

- FileFormatVersion 4 indicates enriched format
- Versions 1-3 remain supported (existing QueryPlanModel)
- Deserializer detects version and loads appropriately

---

## Integration Points

### Input Sources

| Source | Data Type | Usage |
|--------|-----------|-------|
| `QueryPlanTraceViewModel` | PhysicalQueryPlanRow, LogicalQueryPlanRow | Raw plan data |
| `ServerTimesViewModel` | TraceStorageEngineEvent | Timing data |
| `ADOTabularColumnCollection` | ADOTabularColumn | Column resolution |
| `ExecutionMetricsTraceEngineEvent` | JSON metrics | Additional metrics |

### Output Consumers

| Consumer | Data Type | Usage |
|----------|-----------|-------|
| `VisualQueryPlanViewModel` | EnrichedQueryPlan | Graph visualization |
| `PlanNodeDetailsViewModel` | EnrichedPlanNode | Details panel |
| File export | JSON | Persistence |
