# Fixtures Needed for Visual Query Plan Tests

Run each of these in DaxStudio against your test model, then export the Server Timings.

## How to Capture

1. Run the DAX query in DaxStudio
2. Go to Server Timings tab
3. Right-click → Export → Physical Query Plan (TSV)
4. Save as `<Name> Physical Query Plan.tsv`
5. Also save the query as `<Name>.dax`

Place files in: `tests/DaxStudio.Tests/example-reimplementation-tests/Fixtures/`

---

## P0 - Critical Fixtures

### 1. Filter_Comparison

Tests filter predicate extraction (e.g., `[Column] > 100`)

```dax
// Option A: Simple filter
CALCULATE(
    [Sales Amount],
    'Product'[ListPrice] > 100
)

// Option B: Multiple comparisons
CALCULATE(
    [Sales Amount],
    'Product'[ListPrice] > 100,
    'Product'[ListPrice] < 1000
)

// Option C: With FILTER
EVALUATE
CALCULATETABLE(
    SUMMARIZE(Sales, 'Product'[Category], "Total", [Sales Amount]),
    FILTER('Product', 'Product'[ListPrice] > 100)
)
```

**Files needed:**
- `Filter_Comparison Physical Query Plan.tsv`
- `Filter_Comparison.dax`

---

### 2. ISBLANK_NotChain

Tests NOT ISBLANK chain folding (Filter → Not → ISBLANK)

```dax
// Option A: Simple NOT ISBLANK
CALCULATE(
    [Sales Amount],
    FILTER(Sales, NOT ISBLANK(Sales[CustomerKey]))
)

// Option B: With table
EVALUATE
FILTER(
    Sales,
    NOT ISBLANK(Sales[CustomerKey])
)

// Option C: Multiple ISBLANK checks
CALCULATE(
    [Sales Amount],
    FILTER(
        Sales,
        NOT ISBLANK(Sales[CustomerKey]) && NOT ISBLANK(Sales[ProductKey])
    )
)
```

**Files needed:**
- `ISBLANK_NotChain Physical Query Plan.tsv`
- `ISBLANK_NotChain.dax`

---

### 3. Spool_Patterns

Tests Spool_Iterator + AggregationSpool/ProjectionSpool folding

```dax
// Option A: SUMX (creates spool pattern)
EVALUATE
ROW(
    "Total", SUMX(Sales, Sales[Quantity] * Sales[Net Price])
)

// Option B: SUMX with filter
EVALUATE
ROW(
    "Total", SUMX(
        FILTER(Sales, Sales[Quantity] > 0),
        Sales[Quantity] * Sales[Net Price]
    )
)

// Option C: Nested iterator
EVALUATE
ADDCOLUMNS(
    VALUES('Product'[Category]),
    "Total", SUMX(
        RELATEDTABLE(Sales),
        Sales[Quantity] * Sales[Net Price]
    )
)
```

**Files needed:**
- `Spool_Patterns Physical Query Plan.tsv`
- `Spool_Patterns.dax`

---

### 4. Arithmetic_Chain

Tests Add → Add → Add chain folding

```dax
// Option A: Define measures then add them
DEFINE
    MEASURE Sales[M1] = SUM(Sales[Quantity])
    MEASURE Sales[M2] = SUM(Sales[Net Price])
    MEASURE Sales[M3] = COUNTROWS(Sales)
    MEASURE Sales[M4] = DISTINCTCOUNT(Sales[CustomerKey])

EVALUATE
ROW(
    "Total", [M1] + [M2] + [M3] + [M4]
)

// Option B: Direct column additions
EVALUATE
ROW(
    "Total", SUM(Sales[Quantity]) + SUM(Sales[Net Price]) + SUM(Sales[Discount Amount]) + SUM(Sales[Unit Cost])
)

// Option C: More additions for longer chain
EVALUATE
ROW(
    "Sum", [M1] + [M2] + [M3] + [M4] + [M1] + [M2]
)
```

**Files needed:**
- `Arithmetic_Chain Physical Query Plan.tsv`
- `Arithmetic_Chain.dax`

---

## P1 - Important Fixtures

### 5. Engine_Transitions

Tests that SE nodes aren't folded into FE parents

```dax
// Should show clear SE (Scan_Vertipaq) and FE (Filter, AddColumns) nodes
EVALUATE
ADDCOLUMNS(
    FILTER(
        Sales,
        Sales[Quantity] > 0
    ),
    "Calculated", Sales[Quantity] * 2
)

// Alternative: Forces FE iteration over SE scan
EVALUATE
FILTER(
    ADDCOLUMNS(
        Sales,
        "Double", Sales[Quantity] * 2
    ),
    [Double] > 10
)
```

**Files needed:**
- `Engine_Transitions Physical Query Plan.tsv`
- `Engine_Transitions.dax`

---

### 6. CallbackDataID (Optional)

Tests CallbackDataID performance issue detection

```dax
// This pattern often triggers callbacks - measure reference in iterator
DEFINE
    MEASURE Sales[Complex] =
        SUMX(
            VALUES('Date'[Date]),
            CALCULATE([Sales Amount])
        )

EVALUATE
ADDCOLUMNS(
    VALUES('Product'[Category]),
    "Value", [Complex]
)

// Alternative: Nested CALCULATE in iterator
EVALUATE
ADDCOLUMNS(
    VALUES('Product'[Category]),
    "Value", SUMX(
        RELATEDTABLE(Sales),
        CALCULATE(Sales[Quantity] * Sales[Net Price])
    )
)
```

**Files needed:**
- `CallbackDataID Physical Query Plan.tsv`
- `CallbackDataID.dax`

---

## Summary Checklist

| # | Fixture | Status |
|---|---------|--------|
| 1 | Filter_Comparison | ⬜ |
| 2 | ISBLANK_NotChain | ⬜ |
| 3 | Spool_Patterns | ⬜ |
| 4 | Arithmetic_Chain | ⬜ |
| 5 | Engine_Transitions | ⬜ |
| 6 | CallbackDataID | ⬜ (optional) |

---

## Already Have

- ✅ `Evaluate Date.*`
- ✅ `DirectQuery.*`
- ✅ `Large plan.*`
