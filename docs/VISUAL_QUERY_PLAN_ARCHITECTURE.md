# Visual Query Plan - Architecture Decisions

This document captures key architectural decisions, alternatives considered, and rationale for the Visual Query Plan feature.

---

## ADR-001: Tree Building vs. Metadata Enrichment Separation

**Date:** 2025-12-24
**Status:** Accepted

### Context

Cache nodes need to display column cardinality from VertiPaq Analyzer (VPA) data. The column name is inferred during tree building from ancestor `IterCols`, but cardinality requires access to model metadata that lives in the ViewModel layer.

### Decision

**Separate tree building from metadata enrichment using post-processing.**

- `BuildTree()` remains a pure static method that parses plan structure
- `VisualQueryPlanViewModel` performs a second pass to enrich nodes with metadata (cardinality, etc.)
- Enrichment is optional and gracefully skipped if VPA data unavailable

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Pass metadata service to BuildTree | `BuildTree(plan, IMetadataLookup)` | Mixes concerns; breaks existing signature; harder to test |
| Lazy property resolution | Nodes hold resolver reference, resolve on-demand | Memory overhead; UI stutter risk; harder to test |
| Enriched data types | `CacheColumnDetail` class with column + cardinality | Overkill for single property; more refactoring |

### Consequences

- **Positive:** Clean separation; BuildTree stays testable without mocks; VPA access stays in ViewModel
- **Negative:** Two tree traversals (negligible perf impact for small trees)

---

## ADR-002: Node Folding Strategy (Multi-Pass)

**Date:** 2025-12-24
**Status:** Accepted

### Context

Raw query plans contain many nodes that add visual noise without adding insight:
- Column reference nodes under comparisons
- Spool type nodes under Spool_Iterator
- Identical parent-child nodes (e.g., double Proxy)

### Decision

**Use multiple sequential passes in BuildTree to fold/collapse nodes.**

Current passes:
1. Identify filter predicate nodes to fold
2. Extract filter predicate expressions
3. Collapse comparison operators with column references
4. Fold spool children into Spool_Iterator/SpoolLookup parents
5. Extract single-column info for Scan_Vertipaq nodes
6. Fold identical parent-child nodes
7. Infer Cache column from ancestor IterCols

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Single-pass with complex conditionals | Check all folding rules in one loop | Hard to maintain; order-dependent bugs |
| Visitor pattern | Separate visitor classes per fold type | Over-engineered for current needs |
| Post-build transformation | Build full tree, then transform | Less efficient; harder to skip folded nodes |

### Consequences

- **Positive:** Each pass is simple and focused; easy to add new folding rules; order is explicit
- **Negative:** Multiple iterations over node list (acceptable for plan sizes <1000 nodes)

---

## ADR-003: Engine Type Classification Source

**Date:** 2025-12-24
**Status:** Accepted

### Context

Nodes need to display SE (Storage Engine) or FE (Formula Engine) badges. Engine type can come from:
- Operator dictionary (static mapping)
- Operation string parsing (dynamic)
- Server timing correlation (runtime)

### Decision

**Use operator dictionary as primary source, with operation string as fallback.**

- `DaxOperatorDictionary` defines `EngineType` for known operators
- Unknown operators default to `Unknown`
- Cache operators are SE (per SQLBI documentation - datacaches are SE requests)

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Parse from operation string suffix | ScaLogOp → FE, IterPhyOp → infer | Suffixes don't map cleanly to SE/FE |
| Server timing only | Correlate with SE query events | Not all nodes have timing; plan-only mode |

### Consequences

- **Positive:** Consistent classification; works without server timing
- **Negative:** New operators need dictionary entries; may lag behind engine changes

---

## ADR-004: Property Extraction via Regex

**Date:** 2025-12-24
**Status:** Accepted

### Context

Operation strings contain structured properties like:
- `IterCols(0, 1)('Table'[Col1], 'Table'[Col2])`
- `#Records=1000 #KeyCols=5`
- `RequiredCols(0)()`

### Decision

**Use compiled regex patterns for property extraction.**

- Patterns defined as `private static readonly Regex` with `RegexOptions.Compiled`
- Each property has a dedicated pattern (RequiredColsPattern, IterColsPattern, etc.)
- Extraction methods return null if pattern not found

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| String parsing (IndexOf/Substring) | Manual character-by-character parsing | Error-prone; hard to maintain |
| Full parser/lexer | Formal grammar for operation strings | Over-engineered; operation format undocumented |
| Single mega-regex | One pattern to extract all properties | Unmaintainable; hard to debug |

### Consequences

- **Positive:** Patterns are readable and testable; compiled for performance
- **Negative:** Regex can be fragile if format changes; need to escape special chars

---

## ADR-005: Display Text Strategy

**Date:** 2025-12-24
**Status:** Accepted

### Context

Node display needs to balance information density with readability:
- Full operation strings are too long
- Need to show operator name + relevant detail
- Different node types have different relevant details

### Decision

**Priority-based detail selection with XAML word wrapping.**

Display format: `OperatorName: Detail`

Detail priority (first match wins):
1. Filter predicate expression (for Filter nodes)
2. Spool type info (for Spool_Iterator)
3. Scan column info (for Scan_Vertipaq)
4. Cache column info (for Cache)
5. None (just operator name)

Word wrapping handled by XAML, not code truncation.

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Fixed truncation (25 chars) | Truncate with "..." | Lost important info; arbitrary limit |
| Tooltip only | Short name, full detail on hover | Required mouse interaction for basic info |
| Multiple lines always | Always show 2-3 lines per node | Cluttered; wasted space for simple nodes |

### Consequences

- **Positive:** Most relevant info shown; flexible per node type
- **Negative:** Detail selection logic spread across properties; priority order is implicit

---

## ADR-006: Test Fixture Strategy

**Date:** 2025-12-24
**Status:** Accepted

### Context

Visual Query Plan tests need realistic plan data. Options:
- Hardcoded strings in tests
- Embedded resources
- External fixture files

### Decision

**Use external fixture files with dynamic discovery.**

- Fixtures in `tests/DaxStudio.Tests/VisualQueryPlan/Fixtures/`
- Each fixture is a folder with `.dax`, `.queryPlans`, `.serverTimings`, `.visualPlan` files
- Tests discover fixtures dynamically via `Directory.GetDirectories()`
- Fixture-specific tests can target individual fixtures by name

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Hardcoded strings | Plan text inline in test methods | Hard to read; duplicated; no real data |
| Embedded resources | .resx files with plan content | Harder to update; not visible in folder |
| Single JSON file | All fixtures in one file | Harder to manage; no query/timing separation |

### Consequences

- **Positive:** Easy to add fixtures from real queries; files are inspectable; clean test code
- **Negative:** Tests depend on file system; fixture format must be maintained

---

## ADR-007: Operator Dictionary Structure

**Date:** 2025-12-24
**Status:** Accepted

### Context

The operator dictionary needs to map operator names to metadata (category, description, engine type). Operator names have variations:
- Exact matches (e.g., `Scan_Vertipaq`)
- Parameterized operators (e.g., `AggregationSpool<Sum>`, `AggregationSpool<Count>`)
- Operators with similar names but different semantics

### Decision

**Use case-insensitive dictionary with partial matching support.**

- Primary storage: `Dictionary<string, OperatorInfo>` with case-insensitive comparer
- Exact match first, then prefix match for parameterized operators
- Each operator entry contains: Category, Description, EngineType
- Missing operators return "Unknown" category, not null

Implementation:
```csharp
public static OperatorInfo GetOperatorInfo(string operatorName)
{
    if (_operators.TryGetValue(operatorName, out var info))
        return info; // Exact match

    // Try prefix match for AggregationSpool<*>
    var prefix = operatorName.Split('<')[0];
    if (_operators.TryGetValue(prefix, out info))
        return info;

    return new OperatorInfo { Category = "Unknown", ... };
}
```

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Regex-based matching | Use patterns to match operator variants | Slower; harder to maintain; overkill |
| Separate dictionary per category | Dictionary per category (Filter, Aggregation, etc.) | Harder to query; category is metadata |
| Inheritance hierarchy | Class per operator type | Over-engineered; need for simple lookup |

### Consequences

- **Positive:** Fast lookup; handles parameterized operators; extensible
- **Negative:** Prefix matching only works for `<` delimited params; may need refinement for other patterns

---

## ADR-008: Property Extraction Location

**Date:** 2025-12-24
**Status:** Accepted

### Context

Operation strings contain structured properties that need extraction (RequiredCols, JoinCols, BlankRow, etc.). Where should extraction logic live?
- In `PlanNodeViewModel` (currently)
- In dedicated parser service
- In model classes

### Decision

**Keep extraction in PlanNodeViewModel with option for future parser service.**

Current approach:
- Regex patterns defined as `private static readonly` in `PlanNodeViewModel`
- Extraction methods are private instance methods
- If complexity grows, refactor to `OperationStringParser` service

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Separate parser service immediately | Create `OperationStringParser` upfront | Premature abstraction; current needs are simple |
| Extension methods on string | `operation.ExtractRequiredCols()` | Pollutes string namespace; less discoverable |
| Model-level extraction | Extract in `PlanNode` model | Mixes parsing with data model; harder to test ViewModel |

### Consequences

- **Positive:** Keeps related code together; easy to refactor later; no additional service dependencies
- **Negative:** ViewModel grows larger; harder to reuse extraction if needed elsewhere

---

## ADR-009: xmSQL Callback Detection Strategy

**Date:** 2025-12-24
**Status:** Accepted

### Context

xmSQL strings can contain callback functions that have performance implications:
- `CallbackDataID` - SE calling FE (not cached, performance warning)
- `EncodeCallback` - Query-scoped calculated columns
- Others are informational

Need to detect these for display and warnings.

### Decision

**Use simple string containment checks, not full parsing.**

Implementation:
```csharp
public enum XmSqlCallbackType
{
    None,
    CallbackDataID,      // Warning severity
    EncodeCallback,      // Info severity
    LogAbsValueCallback,
    RoundValueCallback,
    MinMaxColumnPositionCallback,
    Cond
}

public static XmSqlCallbackType DetectCallbackType(string xmSql)
{
    if (xmSql.Contains("CallbackDataID")) return XmSqlCallbackType.CallbackDataID;
    if (xmSql.Contains("EncodeCallback")) return XmSqlCallbackType.EncodeCallback;
    // ... etc
}
```

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Full xmSQL parser | Parse entire xmSQL grammar | xmSQL format undocumented; too complex |
| Regex patterns | Use patterns to extract callback names | Overkill; simple contains check sufficient |
| Dictionary of keywords | Map keywords to callback types | Same complexity as switch/if; less readable |

### Consequences

- **Positive:** Simple and reliable; fast; easy to extend with new callback types
- **Negative:** May have false positives if callback name appears in string literal (unlikely)

---

## ADR-010: Performance Issue Detection Thresholds

**Date:** 2025-12-24
**Status:** Accepted

### Context

Performance issue detector needs to identify problematic patterns. Thresholds need to be:
- Meaningful (catch real issues)
- Not too noisy (avoid false positives)
- Potentially configurable

### Decision

**Use fixed thresholds initially, with comments noting they may become configurable.**

Defined thresholds:
- High FE ratio: FE time > 50% of total (Warning)
- High parallelism ratio: CPU time / Duration > 32 (Info)
- Excessive records: #Records > 1,000,000 for certain operators (Warning)

Implementation includes comments like:
```csharp
// TODO: Consider making this threshold configurable
if (plan.FormulaEngineDurationMs > plan.TotalDurationMs * 0.5)
```

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| User-configurable thresholds | Settings UI for all thresholds | Premature; don't know which thresholds matter yet |
| Adaptive thresholds | Learn from query history | Too complex; need baseline first |
| Single severity level | All issues are warnings | Loses nuance; users want to prioritize |

### Consequences

- **Positive:** Simple to implement and test; can gather feedback on appropriateness
- **Negative:** May need tuning based on real-world usage; hardcoded values

---

## ADR-011: Correlation Data Structure

**Date:** 2025-12-24
**Status:** Accepted

### Context

Need to correlate server timing events with plan nodes. Correlation can be:
- 1:1 (one event per node)
- 1:N (one node matches multiple events)
- N:1 (multiple nodes share one event)
- 0:1 (node has no timing)

### Decision

**Use node-centric enrichment with lists for multiple events.**

Structure:
```csharp
public class PlanNodeViewModel
{
    public long DurationMs { get; set; }
    public long CpuTimeMs { get; set; }
    public bool IsCacheHit { get; set; }
    public string CacheHitSource { get; set; }
    public List<string> RelatedEventIds { get; set; } // For debugging
}
```

Correlation logic in `PlanEnrichmentService`:
- Match by ObjectName (sanitized for case differences)
- Aggregate timings if multiple events match
- Track cache hits separately

### Alternatives Considered

| Option | Description | Rejected Because |
|--------|-------------|------------------|
| Event-centric mapping | Dictionary<EventId, NodeId> | Harder to query from node perspective |
| Bidirectional references | Nodes point to events, events point to nodes | Circular references; memory overhead |
| Separate correlation object | Independent correlation graph | More complex; harder to serialize |

### Consequences

- **Positive:** Simple to query from node; aggregation is straightforward
- **Negative:** May lose event-level detail if multiple events aggregate

---

## Future Considerations

### Pending Decisions

1. **Nested Spool Grouping** - How to present collapsed spool chains (artificial node vs. badge)
2. **Parallelism Indicators** - Badge vs. color vs. icon for SE parallelism
3. **Documentation Links** - Embedded vs. external links to dax.guide/SQLBI
4. **Threshold Configurability** - Which performance thresholds should be user-configurable
5. **Parser Service Extraction** - When complexity justifies separate `OperationStringParser` service

### Technical Debt

1. Column references regex may match system properties (IterCols showing as DAX reference)
2. Some operators missing from dictionary (add as discovered)
3. Test assertions use older patterns (Assert.IsTrue vs Assert.Contains)
4. Prefix matching for parameterized operators only handles `<` delimiter

---

*Last updated: 2025-12-24*
