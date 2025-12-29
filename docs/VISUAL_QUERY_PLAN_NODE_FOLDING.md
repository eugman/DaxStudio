# Visual Query Plan Node Folding Design

## Overview

The Visual Query Plan displays DAX query plans as interactive graphs. To reduce visual noise while preserving important information, certain nodes are "folded" (hidden) and their information is rolled up into parent nodes.

The folding algorithm uses **15 sequential passes**, each handling a specific pattern. This document describes each pass and its rationale.

## Core Principles

### 1. Never Fold Across Engine Transitions

**Rule**: A node with a different engine type than its parent should NEVER be folded.

**Rationale**: Engine transitions (SE<->FE or FE<->SE) are critical performance indicators. Users need to see where the Storage Engine (SE) hands off to the Formula Engine (FE) and vice versa.

**Example - DO NOT FOLD**:
```
Filter (FE)
├── Scan_Vertipaq (SE)  <- Keep visible! Engine transition
└── GreaterThan (FE)    <- Can fold into Filter
```

### 2. Preserve Data Source Nodes

**Rule**: Nodes that represent data sources (Scan_Vertipaq, DirectQueryResult, etc.) should never be folded.

**Rationale**: These show where data originates and often carry performance metrics.

### 3. Roll Up Information to Parent

**Rule**: When folding a node, transfer its useful information (records, predicates, column info) to the surviving parent.

**Rationale**: Users shouldn't lose insight when we simplify the display.

---

## The 15-Pass Folding Algorithm

### Pass 1: Column Reference Folding

**Pattern**: Nodes starting with `'Table'[Column]: ScaLogOp` or `'Table'[Column]: RelLogOp`

**Action**: Mark for folding into parent.

**Rationale**: These nodes are pure column references that add visual noise. The column context is already visible in the parent operation string.

**Example**:
```
Before:
  GreaterThan
  ├── 'Sales'[Amount]: ScaLogOp
  └── Constant: 100

After:
  GreaterThan (children folded, predicate extracted)
```

---

### Pass 2: Filter Predicate Collection

**Pattern**: Filter nodes with comparison operator children (GreaterThan, LessThan, Equal, NotEqual, etc.)

**Action**:
1. Identify comparison child and its operands
2. Build predicate expression (e.g., `[Amount] > 100`)
3. Mark comparison subtree for folding
4. Store predicate on Filter node

**Rationale**: A Filter node's purpose is defined by its predicate. Showing `Filter: [Amount] > 100` is more useful than separate nodes.

**Comparison Operators**:
- `GreaterThan` -> `>`
- `LessThan` -> `<`
- `GreaterOrEqualTo` -> `>=`
- `LessOrEqualTo` -> `<=`
- `Equal` -> `=`
- `NotEqual` -> `<>`

**Example**:
```
Before:
  Filter: RelLogOp
  ├── Scan_Vertipaq (SE)
  └── GreaterThan
      ├── 'Sales'[Amount]
      └── Constant: 100

After:
  Filter: [Amount] > 100
  └── Scan_Vertipaq (SE)  <- Preserved (different engine)
```

---

### Pass 3: Physical Plan Comparison Operators

**Pattern**: Physical plan nodes with `LogOp=<comparison>` (e.g., `Extend_Lookup: ... LogOp=GreaterThan`)

**Action**: Extract the logical comparison and build predicate from children.

**Rationale**: Physical plans encode comparisons differently than logical plans. This pass handles that format.

---

### Pass 4: Unary Predicate Functions (ISBLANK, Not Chains)

**Pattern**: Chains like `Filter -> Not -> ISBLANK -> column`

**Action**:
1. For `Not` with single ISBLANK/ISERROR child, fold the chain
2. Build predicate expression (e.g., `NOT ISBLANK([Column])`)
3. Roll up to parent Filter

**Rationale**: These chains are common in DAX (e.g., `FILTER(table, NOT ISBLANK([Col]))`) and should display as a single expression.

**Example**:
```
Before:
  Filter
  └── Not
      └── ISBLANK
          └── 'Sales'[Amount]

After:
  Filter: NOT ISBLANK([Amount])
```

---

### Pass 5: Spool Child Folding

**Pattern**: `Spool_Iterator`, `SpoolLookup`, or hash lookup nodes with spool children like:
- `AggregationSpool<Sum>`
- `ProjectionSpool<ProjectFusion<...>>`
- `Extend_Lookup`
- `Cache`

**Action**:
1. Fold the spool child into parent
2. Store spool type info (e.g., "Sum", "GroupBy") for display
3. Recursively fold any nested spools/caches

**Rationale**: Spool internals (how data is cached) are implementation details. Users care about *what* the spool contains, not the mechanics.

**Example**:
```
Before:
  Spool_Iterator #Records=100
  └── AggregationSpool<Sum> #Records=100
      └── Scan_Vertipaq

After:
  Spool_Iterator [Sum] #Records=100
  └── Scan_Vertipaq  <- Preserved (data source)
```

---

### Pass 6: Arithmetic Chain Folding

**Pattern**: Chains of same arithmetic operator: `Add -> Add -> Add`

**Action**:
1. Count chain length
2. Fold intermediate nodes
3. Display as `Add (3x)`

**Operators**: Add, Subtract, Multiply, Divide, Min, Max, Coalesce, Power, Mod

**Rationale**: Complex DAX expressions create long chains. Collapsing them shows the pattern without overwhelming the user.

**Example**:
```
Before:
  Add
  └── Add
      └── Add
          ├── [A]
          └── [B]

After:
  Add (3x)
```

---

### Pass 7: SingletonTable Folding

**Pattern**: `SingletonTable` nodes

**Action**: Always fold into parent.

**Rationale**: SingletonTable is a technical implementation detail for scalar context. It never provides useful diagnostic information.

---

### Pass 8: Column Info Extraction (Scan_Vertipaq, DirectQueryResult)

**Pattern**: `Scan_Vertipaq`, `DirectQueryResult`, or nodes with `DependOnCols`

**Action**: Extract `RequiredCols` or `DependOnCols` for display (e.g., showing which columns are being scanned).

**Rationale**: This isn't folding - it's enrichment. Knowing which columns are accessed helps optimization.

---

### Pass 9: Identical Parent-Child Folding

**Pattern**: Child node with identical operation string as parent (exact match)

**Action**: Fold child into parent.

**Rationale**: Sometimes the plan contains redundant wrapper nodes. This removes exact duplicates.

---

### Pass 10: Cache Column Inference

**Pattern**: `Cache` nodes without explicit column info

**Action**: Look up the ancestor tree for `Spool_Iterator` with `IterCols` and inherit that column info.

**Rationale**: Cache nodes often don't state what they cache. The ancestor spool context tells us.

---

### Pass 11: Nested Spool_Iterator Chain Grouping

**Pattern**: `Spool_Iterator -> Spool_Iterator` chains with varying `#Records`

**Action**:
1. Track row ranges (e.g., 100-1000 rows)
2. Fold into single node with range display
3. Preserve heterogeneous row info

**Rationale**: Nested spools are common in complex queries. Showing them as ranges is more readable.

**Example**:
```
Before:
  Spool_Iterator #Records=1000
  └── Spool_Iterator #Records=100
      └── Scan_Vertipaq

After:
  Spool_Iterator [100-1000 rows]
  └── Scan_Vertipaq
```

---

### Pass 12: Variant Type Coercion Folding

**Pattern**: `Variant->*` wrapper nodes (e.g., `Variant->Decimal`)

**Action**: Fold into child, as these are just type coercions.

**Rationale**: Type coercion wrappers are implementation details that don't affect performance analysis.

---

### Pass 13: SpoolLookup + Spool_Iterator Folding

**Pattern**: `SpoolLookup` with `Spool_Iterator` child

**Action**:
1. Fold Spool_Iterator into SpoolLookup
2. Track row range if records differ
3. Preserve spool type info

**Rationale**: SpoolLookup and its backing Spool_Iterator are logically one operation.

---

### Pass 14: Proxy Operator Folding (TableVarProxy)

**Pattern**: `Proxy`, `TableVarProxy` nodes with single child

**Action**:
1. Fold proxy into its child
2. For `TableVarProxy`, extract VAR name for display on child

**Rationale**: Proxy nodes are wrappers. The child is what matters. For VAR references, we want to show which variable is being referenced.

**Example**:
```
Before:
  TableVarProxy: __var1
  └── Scan_Vertipaq

After:
  Scan_Vertipaq (VAR: __var1)
```

---

### Pass 15: TableToScalar Folding

**Pattern**: `TableToScalar` with spool children (`AggregationSpool<TableToScalar>`, etc.)

**Action**:
1. Fold TableToScalar into parent
2. Fold associated spool children
3. Transfer `#Records` to first non-spool descendant

**Rationale**: TableToScalar is a conversion operation. The records count belongs on the actual data operation.

---

## Special Cases

### Already-Condensed Predicates

Some predicates come pre-condensed in the plan text (e.g., `'Product'[Color] <> Black`). These are recognized and displayed as-is rather than reconstructing from children.

### Folded Operations Display

After folding, nodes can show a summary of what was folded:
- `FoldedOperations` property lists folded children
- `FoldedOperationsTooltip` shows details on hover

### String vs Numeric Values in Predicates

- String values are quoted: `[Name] > "Bob"`
- Numeric values are not: `[Amount] > 100`
- BLANK is shown as: `[Col] = BLANK`

---

## Test Cases

The test suite (`Phase04_NodeFolding/NodeFoldingTests.cs`) covers:

1. Filter with comparison subtree -> Show predicate in Filter display
2. Filter with already-condensed predicate -> Show predicate in Filter display
3. Filter_Vertipaq with SE scan child -> Keep scan visible, show predicate
4. Filter with FE and SE children -> Keep both visible (no cross-engine folding)
5. Nested filters -> Each filter shows its own predicate
6. Spool_Iterator + AggregationSpool -> Fold spool, show type
7. Arithmetic chains (Add->Add) -> Collapse with count
8. ISBLANK/Not chains -> Fold into parent Filter
9. TableToScalar + spool -> Fold both, transfer records
10. Engine transition -> Never fold across SE/FE boundary
11. SingletonTable -> Always fold
12. Identical operations -> Fold duplicate
13. Union with multiple children -> Preserve all (semantic content)
14. CrossApply patterns -> Context-dependent folding

---

## Implementation Notes

### Performance

- Use `HashSet<int>` for `foldedNodeIds` - O(1) lookup
- Use `Dictionary<int, T>` for collected info - O(1) lookup
- Process passes sequentially to ensure dependencies are met
- Cache `SubtreeWidth` calculations (invalidate on collapse toggle)

### Debugging

Enable verbose logging by setting `VerboseBuildTreeLogging = true` in `PlanNodeViewModel`. This logs every folding decision.

---

*Document updated: 2025-12-28*
*Based on DaxStudio Visual Query Plan implementation (branch: 001-visual-query-plan)*
