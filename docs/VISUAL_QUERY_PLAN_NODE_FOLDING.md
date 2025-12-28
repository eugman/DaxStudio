# Visual Query Plan Node Folding Design

## Overview

The Visual Query Plan displays DAX query plans as interactive graphs. To reduce visual noise while preserving important information, certain nodes are "folded" (hidden) and their information is rolled up into parent nodes.

## Core Principles

### 1. Never Fold Across Engine Transitions

**Rule**: A node with a different engine type than its parent should NEVER be folded.

**Rationale**: Engine transitions (SE→FE or FE→SE) are critical performance indicators. Users need to see where the Storage Engine (SE) hands off to the Formula Engine (FE) and vice versa.

**Example - DO NOT FOLD**:
```
Filter (FE)
├── Scan_Vertipaq (SE)  ← Keep visible! Engine transition
└── GreaterThan (FE)    ← Can fold into Filter
```

### 2. Fold Column Reference Nodes

**Rule**: Nodes that are pure column references (e.g., `'Table'[Column]: ScaLogOp`) should be folded.

**Rationale**: These nodes just provide column context that's already visible in the parent operation string. They add visual noise without adding information.

**Pattern**: Operation starts with `'Table'[Column]` and has no meaningful operator after the colon (just `ScaLogOp` or `RelLogOp`).

### 3. Roll Up Filter Predicates

**Rule**: Filter nodes should absorb their predicate subtree and display the filter expression directly.

**Rationale**: A Filter node's purpose is defined by its predicate. Showing `Filter: [FirstName] > "Bob"` is more useful than showing separate nodes for Filter, GreaterThan, Column, and Constant.

**Structure**:
```
Filter: RelLogOp DependOnCols()()...
├── Scan_Vertipaq: ...           ← Keep visible (data source, SE)
└── GreaterThan: ...             ← Fold into Filter
    ├── 'Customer'[First Name]   ← Fold into Filter
    └── Constant: Bob            ← Fold into Filter
```

**Result**:
```
Filter: [First Name] > "Bob"
└── VertipPaq Scan (SE)
```

### 4. Preserve Already-Condensed Predicates

**Rule**: Some predicates are already condensed in the plan text (e.g., `'Product'[Color] <> Black`). These should be recognized and displayed as-is.

**Example**:
```
Filter_Vertipaq: ...
├── Scan_Vertipaq: ...
└── 'Product'[Color] <> Black    ← Already condensed, fold into Filter
```

## Implementation Details

### Identifying Filter Nodes

A node is a Filter node if its operator name is:
- `Filter`
- `Filter_Vertipaq`
- `DataPostFilter`

### Identifying Predicate Children

For a Filter node, predicate children are:
1. Comparison operators: `GreaterThan`, `LessThan`, `Equal`, `NotEqual`, `GreaterOrEqualTo`, `LessOrEqualTo`
2. Value operators: `Constant`, `ColValue`
3. Column references: `'Table'[Column]: ScaLogOp`
4. Already-condensed expressions: `'Table'[Column] <> Value`

### Building the Predicate Expression

1. Find the comparison child of the Filter
2. Extract left operand (usually a column reference)
3. Extract comparison symbol (>, <, =, <>, >=, <=)
4. Extract right operand (usually a constant value)
5. Format as: `[Column] {symbol} {value}`

### Special Cases

1. **String values**: Quote them → `[Name] > "Bob"`
2. **Numeric values**: No quotes → `[Amount] > 100`
3. **Boolean predicates**: Already condensed → `[Color] <> Black`

## Test Cases

1. Filter with comparison subtree → Show predicate in Filter display
2. Filter with already-condensed predicate → Show predicate in Filter display
3. Filter_Vertipaq with SE scan child → Keep scan visible, show predicate
4. Filter with FE and SE children → Keep both visible (no cross-engine folding)
5. Nested filters → Each filter shows its own predicate
