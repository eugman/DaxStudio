# Visual Query Plan - Reference Guide

Comprehensive reference for DAX query plan operators, terminology, and metrics.

---

## Plan Types

| Subclass | Mode | Description |
|----------|------|-------------|
| DAX VertiPaq Logical Plan | VertiPaq | Logical operators for in-memory storage |
| DAX VertiPaq Physical Plan | VertiPaq | Physical execution plan for in-memory |
| DAX DirectQuery Algebrizer Tree | DirectQuery | Algebrizer tree for SQL passthrough |
| DAX DirectQuery Logical Plan | DirectQuery | Logical plan for SQL passthrough |

A DAX query (EVALUATE statement) triggers exactly two events: a logical plan event and a physical plan event.

---

## Operator Type Suffixes

### Logical Plan

| Suffix | Full Name | Description |
|--------|-----------|-------------|
| `ScaLogOp` | Scalar Logical Operator | Outputs a scalar value (numeric, string, Boolean, etc.) |
| `RelLogOp` | Relational Logical Operator | Outputs a table of columns and rows |

### Physical Plan

| Suffix | Full Name | Description |
|--------|-----------|-------------|
| `LookupPhyOp` | Lookup Physical Operator | Given a current row as input, calculates and returns a scalar value |
| `IterPhyOp` | Iterator Physical Operator | Given a current row as optional input, returns a sequence of rows |

---

## Logical Operators

Logical operators define **what** the query does. Found in the Logical Query Plan.

### Storage Engine (SE) - Logical

| Operator | Suffix | Description |
|----------|--------|-------------|
| `Scan_Vertipaq` | RelLogOp | Joins root table with related tables (many-to-one), applies Vertiscan predicates, groups by output columns |
| `GroupBy_Vertipaq` | RelLogOp | Modifies column names and incorporates rollup columns |
| `Filter_Vertipaq` | RelLogOp | Applies Verticalc predicates (complex filter expressions) |
| `Sum_Vertipaq` | ScaLogOp | SUM aggregation |
| `Count_Vertipaq` | ScaLogOp | COUNT aggregation |
| `DistinctCount_Vertipaq` | ScaLogOp | DISTINCTCOUNT aggregation |
| `Min_Vertipaq` | ScaLogOp | MIN aggregation |
| `Max_Vertipaq` | ScaLogOp | MAX aggregation |
| `Average_Vertipaq` | ScaLogOp | AVERAGE aggregation |
| `Stdev.S_Vertipaq` | ScaLogOp | Sample standard deviation |
| `Stdev.P_Vertipaq` | ScaLogOp | Population standard deviation |
| `Var.S_Vertipaq` | ScaLogOp | Sample variance |
| `Var.P_Vertipaq` | ScaLogOp | Population variance |

### Formula Engine (FE) - Logical

| Operator | Suffix | Description |
|----------|--------|-------------|
| `AddColumns` | RelLogOp | Adds calculated columns to a table |
| `Calculate` | ScaLogOp | Evaluates expression with modified filter context (CALCULATE) |
| `CalculateTable` | RelLogOp | Evaluates table with modified filter context (CALCULATETABLE) |
| `GroupSemiJoin` | RelLogOp | Join returning matched rows from primary table |
| `TableCtor` | RelLogOp | Inline table from `{ }` constructor. [dax.guide](https://dax.guide/op/table-constructor/) |
| `VarScope` | - | Container for variable definitions |
| `ScalarVarProxy` | ScaLogOp | Returns value of a scalar variable from VarScope |
| `TableVarProxy` | RelLogOp | Returns value of a table variable from VarScope |

---

## Physical Operators

Physical operators define **how** the query executes. Found in the Physical Query Plan.

### Storage Engine (SE) - Physical

| Operator | Suffix | Description |
|----------|--------|-------------|
| `VertipaqResult` | IterPhyOp | Iterates through results from completed SE query |

### Formula Engine (FE) - Physical

| Operator | Suffix | Description |
|----------|--------|-------------|
| `AddColumns` | IterPhyOp | Builds result table with added columns |
| `SingletonTable` | IterPhyOp | Returns single-row table (from ROW function) |
| `TableCtor` | IterPhyOp | Builds inline table from `{ }` constructor syntax. [dax.guide](https://dax.guide/op/table-constructor/) |
| `Spool_Iterator<Spool>` | IterPhyOp | Iterates over spooled/materialized data |
| `Spool` | LookupPhyOp | Searches for value in cached/spooled data |
| `AggregationSpool<Cache>` | SpoolPhyOp | Cached aggregation spool |
| `AggregationSpool<Sum>` | SpoolPhyOp | Sum aggregation spool |
| `AggregationSpool<Count>` | SpoolPhyOp | Count aggregation spool |
| `AggregationSpool<Min>` | SpoolPhyOp | Min aggregation spool |
| `AggregationSpool<GroupBy>` | SpoolPhyOp | Group by aggregation spool |
| `CrossApply` | IterPhyOp | Applies operation to each row (nested loop/cartesian) |
| `Filter` | IterPhyOp | Filters rows based on condition |
| `Extend_Lookup` | IterPhyOp | Extends rows with lookup values |
| `Spool_UniqueHashLookup` | IterPhyOp | Unique hash lookup from spool |
| `Spool_MultiValuedHashLookup` | IterPhyOp | Multi-valued hash lookup from spool |
| `InnerHashJoin` | IterPhyOp | Inner join using hash algorithm |
| `HashLookup` | IterPhyOp | Hash-based value lookup |
| `HashByValue` | SpoolPhyOp | Spools data hashed by value |
| `DatesBetween` | IterPhyOp | Generates date range |
| `ApplyRemap` | IterPhyOp | Applies column remapping |
| `Proxy` | IterPhyOp | References a variable value during execution |
| `Cache` | - | Retrieves cached results |
| `DataPostFilter` | - | Filters data after retrieval |
| `Variant->Numeric/Date` | IterPhyOp | Coerces Variant type to Numeric or Date |
| `Variant->*` | IterPhyOp | Type coercion from Variant to specific type |
| `Median` | IterPhyOp | Calculates median (50th percentile) |
| `Multiply` | LookupPhyOp | Multiplies two values (arithmetic) |

### Comparison Operators (FE)

| Operator | Symbol | Description | Reference |
|----------|--------|-------------|-----------|
| `GreaterThan` | `>` | Greater than comparison | [dax.guide](https://dax.guide/op/greater-than/) |
| `GreaterOrEqualTo` | `>=` | Greater than or equal comparison | [dax.guide](https://dax.guide/op/greater-than-or-equal-to/) |
| `LessThan` | `<` | Less than comparison | [dax.guide](https://dax.guide/op/less-than/) |
| `LessOrEqualTo` | `<=` | Less than or equal comparison | [dax.guide](https://dax.guide/op/less-than-or-equal-to/) |
| `Equal` | `=` | Equality comparison | [dax.guide](https://dax.guide/op/equal-to/) |
| `NotEqual` | `<>` | Inequality comparison | [dax.guide](https://dax.guide/op/not-equal-to/) |

### Arithmetic Operators (FE)

| Operator | Symbol | Description | Reference |
|----------|--------|-------------|-----------|
| `Multiply` | `*` | Multiplication | [dax.guide](https://dax.guide/op/multiplication/) |

---

## xmSQL

xmSQL is the internal query language used by the VertiPaq Storage Engine. It resembles SQL but has implicit GROUP BY behavior - column selections automatically aggregate.

### Syntax Elements

| Element | Syntax | Description |
|---------|--------|-------------|
| Column Reference | `'Table'[Column]` | Bracket notation (not SQL dot notation) |
| SELECT | `SELECT ...` | Retrieves columns and computed values |
| FROM | `FROM 'Table'` | Specifies source table |
| WHERE | `WHERE condition` | Filters data |
| WITH | `WITH $expr := calc` | Defines reusable expressions |

### JOIN Types

| Join | Description |
|------|-------------|
| `LEFT OUTER JOIN` | Many-to-one relationship joins; maintains all rows from primary table |
| `INNER JOIN ... REDUCED BY` | Used for cartesian products with temporary tables |
| `REVERSE HASH JOIN` | Pushes filters from many-side to one-side without explicit relationship |
| `REVERSE BITMAP JOIN` | Bitmap variant of reverse hash join |

Reverse joins auto-selected when: ratio < 20%, many-side has 131,072+ rows, and 16,384+ unique values.

### Aggregation Functions

| Function | Description |
|----------|-------------|
| `SUM(...)` | Sum of values |
| `MIN(...)` / `MAX(...)` | Extreme values |
| `COUNT()` | Row count (no argument needed) |
| `DCOUNT(...)` | Distinct count (`COUNT(DISTINCT...)` equivalent) |

### Callbacks

When VertiPaq cannot compute operations directly, it calls the Formula Engine:

| Callback | Purpose |
|----------|---------|
| `CallbackDataID` | Most common; passes DAX expressions to FE |
| `EncodeCallback` | Handles query-scoped calculated columns |
| `LogAbsValueCallback` | Optimizes PRODUCT/PRODUCTX functions |
| `RoundValueCallback` | Manages type conversions |
| `MinMaxColumnPositionCallback` | Transforms values to sorted positions |
| `Cond` | Evaluates conditional logic for blank row handling |

### Batches and Bitmap Indexing

| Feature | Description |
|---------|-------------|
| `DEFINE TABLE` | Groups multiple xmSQL requests (like SQL subqueries) |
| `SIMPLEINDEXN` | Creates bitmap index for filtering |
| `ININDEX` | Tests membership against bitmap index |

### Datacache and Type Functions

| Function | Description |
|----------|-------------|
| `SET DC_KIND="DENSE"` | Sets datacache kind to dense (no dominant value) |
| `SET DC_KIND="AUTO"` | Sets datacache kind automatically |
| `PFCAST(col AS INT)` | Casts column to integer type |
| `COALESCE(expr)` | Returns first non-null value |
| `PFDATAID(col)` | References column data ID for callbacks |

### WHERE Clause Operators

| Operator | Description |
|----------|-------------|
| `IN (...)` | Value in list |
| `INB (...)` | Value in bitmap-optimized list |
| `VAND` | Virtual AND for combining multiple WHERE conditions |

---

## Column List Format

Column lists use a two-part format: indices in parentheses, then fully-qualified names in parentheses.

```
(1,2)('Table'[Column1], 'Table'[Column2])
```

Empty lists appear as `()()`.

---

## Operator Properties

### Logical Operator Properties

| Property | Applies To | Description |
|----------|------------|-------------|
| `DependOnCols` | ScaLogOp, RelLogOp | Columns from left subtree dependencies or external context |
| `RequiredCols` | RelLogOp | Union of DependOnCols and columns needed by operator |
| `DominantValue` | ScaLogOp | Most common value; `NONE` indicates dense (no dominant value) |
| `Data type` | ScaLogOp | Output type (Integer, String, Currency, Boolean, DateTime, Real) |

### Physical Operator Properties

| Property | Applies To | Description |
|----------|------------|-------------|
| `LookupCols` | LookupPhyOp | Input columns received from parent iterator |
| `IterCols` | IterPhyOp | Output columns produced by this iterator |
| `Data type` | LookupPhyOp | Output value type |

---

## Operation String Metrics

| Metric | Example | Description |
|--------|---------|-------------|
| `#Records=N` | `#Records=1000` | Number of rows processed/output |
| `#KeyCols=N` | `#KeyCols=240` | Number of key columns in spool |
| `#ValueCols=N` | `#ValueCols=0` | Number of value columns in spool |
| `DominantValue=X` | `DominantValue=BLANK` | Most common value; `NONE` = dense |
| `RequiredCols(N, M)` | `RequiredCols(0, 1)` | Column indices required by operator |
| `DependOnCols(N, M)` | `DependOnCols()()` | Column dependencies from context |
| `MeasureRef=[Name]` | `MeasureRef=[Sales]` | Reference to a measure |
| `IterCols(N)(...)` | `IterCols(0)('T'[Col])` | Iterator output columns |
| `LookupCols(N)(...)` | `LookupCols(1)('T'[Col])` | Lookup input columns |
| `Table=N` | `Table=0` | Internal root table ID |
| `+BlankRow` / `-BlankRow` | `+BlankRow` | Whether blank rows are included in scan |
| `JoinCols(...)` | `JoinCols(0)('T'[Key])` | Columns used for natural joins |
| `SemijoinCols(...)` | `SemijoinCols(1)('T'[ID])` | Columns used for semi-join filtering |

---

## Engine Types

| Engine | Badge | Threading | Description |
|--------|-------|-----------|-------------|
| Storage Engine (SE) | `SE` | Multi-threaded | Handles VertiPaq scans and aggregations |
| Formula Engine (FE) | `FE` | Single-threaded | Handles complex DAX logic |

### Storage Engine Implementations

| Implementation | Description |
|----------------|-------------|
| VertiPaq | In-memory compressed columnar storage; data refreshed periodically |
| DirectQuery | Real-time queries to source systems (SQL Server, etc.) |
| Dual | Tables queryable in both VertiPaq and DirectQuery modes |

### Datacache

A **datacache** is an uncompressed temporary in-memory table returned by the Storage Engine. Key characteristics:
- SE always returns uncompressed data regardless of source format
- FE materializes and processes datacaches for joins, filtering, aggregations
- Large datacaches can significantly degrade performance
- FE sends requests to SE sequentially (one at a time), limiting parallelism

---

## Server Timing Metrics

| Metric | Source | Description |
|--------|--------|-------------|
| Total | Query End event | Server processing duration in milliseconds |
| SE | Sum of SE queries | Total Storage Engine duration (multi-threaded) |
| SE CPU | SE events | CPU time in Storage Engine; ratio indicates parallelism |
| FE | Total - SE | Formula Engine duration (single-threaded) |
| SE Queries | Count | Number of Storage Engine queries executed |
| SE Cache | Count | Number of SE cache hits |

**Note**: SE CPU "may not be 100% reliable". FE cannot reach 100% because it orchestrates SE and serializes results.

---

## VertiPaq SE Query End Subclasses

| Subclass | Name | Description |
|----------|------|-------------|
| 0 | VertiPaq Scan | Original query as requested by SSAS engine |
| 10 | VertiPaq Scan Internal | Same query rewritten by VertiPaq for optimization |

Both events represent a single VertiPaq operation. They are usually identical except in rare cases where VertiPaq rewrites the query.

---

## Cache Behavior

| Engine | Caching | Notes |
|--------|---------|-------|
| Storage Engine (SE) | Results cached | VertiPaq query results stored for reuse |
| Formula Engine (FE) | NOT cached | DAX calculations repeat every execution |
| CallbackDataID | NOT cached | Even though computed by SE, results not stored |
| MDX | Calculation cache | MDX has separate calculation cache (DAX does not) |

**VertiPaq SE Query Cache Match** event indicates query resolved from cache without computation.

---

## Performance Indicators

| Indicator | Meaning | Concern Level |
|-----------|---------|---------------|
| `CallbackDataID` | SE calling back to FE during scan | Warning - results not cached |
| High `#Records` on Spool | Excessive materialization | Critical if > 1M rows |
| `VertiPaq Cache exact match` | Query resolved from cache | Good - fast execution |
| CPU Time > Duration | Multi-threaded SE execution | Good - parallelism working |
| Duration > CPU Time | Single-threaded FE work | May need optimization |

### CONTAINS Translation Pattern

The optimizer translates `CONTAINS(Table, Col, Value)` into:
```
NOT ( ISBLANK ( MINX ( FILTER ( Table, Condition ), 1 ) ) )
```
This pattern appears in physical plans when CONTAINS is used.

---

## Type Coercion Operators

When DAX expressions return different types based on conditions (e.g., `IF([measure] > 0, 1, "N/A")`), the result is a **Variant** type. The engine uses coercion operators to convert these to concrete types.

| Operator | Description |
|----------|-------------|
| `Variant->Numeric/Date` | Converts Variant to Numeric or Date type |
| `Variant->String` | Converts Variant to String type |
| `Coerce` | General type coercion operator |

**Variant Type Characteristics:**
- Cannot be used as column data types in tables
- Only valid in DAX measures and expressions
- Required when conditional logic produces varying output types

**Reference:** https://dax.guide/dt/variant/

---

## Visual Display Features

### Node Badges

| Badge | Color | Meaning |
|-------|-------|---------|
| `SE` | Green | Storage Engine operation |
| `FE` | Purple | Formula Engine operation |
| `x16` | Purple | Parallelism factor (16 threads) |
| `Cache` | Blue | Cache hit - query resolved from cache |
| `⚠ CB` | Orange | CallbackDataID - SE calling FE (not cached) |
| `Coerce` | Gray | Type coercion applied (e.g., Variant→Numeric) |

### Row Count Display

| Format | Example | Meaning |
|--------|---------|---------|
| Standard | `1,234 rows` | Single operation row count |
| Range | `1-11 rows` | SpoolLookup/Spool_Iterator folded pair |
| Source annotation | `1,000 (from ServerTiming)` | Row count from timing data |

### Node Folding

The plan tree is simplified by folding related nodes together:

| Fold Type | Description |
|-----------|-------------|
| Comparison folding | `GreaterThan` + `ColValue` + `Constant` → `[Col] > 100` |
| Spool type folding | `Spool_Iterator` + `AggregationSpool<Sum>` → shows spool type |
| Nested spool folding | Multiple `Spool_Iterator` with same #Records → single node with depth |
| Variant folding | `Variant->*` nodes fold into their child, preserving type info |
| SpoolLookup folding | `SpoolLookup` + `Spool_Iterator` → shows row range |

### Detail Pane Sections

The detail pane displays information in order of importance:

1. **Node Header** - Operator name, category, and engine type
2. **Performance Metrics** - Rows, duration, CPU time, data size, cost %
3. **Warnings** - CallbackDataID and other performance concerns
4. **DAX Reference** - Measure name and formula (if applicable)
5. **Data References** - Table and column names
6. **xmSQL Query** - Storage Engine query (if applicable)
7. **Operation String** - Full raw operation text
8. **Issues** - Detected performance issues

---

*Last updated: 2025-12-24*
