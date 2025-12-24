# Visual Query Plan - Parsing Patterns

This document describes the patterns used to parse and interpret DAX query plan operation strings.

## Overview

DAX query plans contain operation strings that encode information about:
- Operator type (e.g., `Sum_Vertipaq`, `Scan_Vertipaq`, `AddColumns`)
- Engine type (Storage Engine vs Formula Engine)
- Measure references
- Column references
- Row counts and metadata

---

## MeasureReference Extraction

The `MeasureReference` property extracts measure names from operation strings using these patterns (in order of priority):

### 1. MeasureRef Pattern
```regex
MeasureRef=['\[]([^'\]]+)['\]]
```
**Matches**: `MeasureRef=[Filtered Margin]` or `MeasureRef='Filtered Margin'`
**Example**: `Calculate: ScaLogOp MeasureRef=[Filtered Margin] DependOnCols()()`

### 2. Physical Plan Quoted Measure
```regex
\(''\[([^\]]+)\]\)
```
**Matches**: `(''[MeasureName])` format common in physical plans
**Example**: `AddColumns: IterPhyOp LogOp=AddColumns IterCols(0)(''[Total Revenue])`

### 3. Aggregation Operators
```regex
LogOp=(Sum|Count|Min|Max|Average|Avg)_Vertipaq\s+([A-Za-z]\w*)\b
```
**Matches**: Vertipaq aggregation operators followed by measure names
**Example**: `Sum_Vertipaq: LogOp=Sum_Vertipaq TotalSales DependOnCols()`

**Note**: `Scan_Vertipaq` is explicitly excluded as it doesn't reference measures.

---

## Operation String Formats

### Physical Plan Format
```
OperatorName: IterPhyOp LogOp=XXX IterCols(N)(references) #Records=N
```
**Example**:
```
Spool_Iterator<SpoolIterator>: IterPhyOp LogOp=Scan_Vertipaq IterCols(0)('Customer'[First Name]) #Records=670
```

### Logical Plan Format
```
OperatorName: ScaLogOp/RelLogOp MeasureRef=[Name] DependOnCols()() Type DominantValue=XXX
```
**Example**:
```
Calculate: ScaLogOp MeasureRef=[Filtered Margin] DependOnCols()() Currency DominantValue=BLANK
```

---

## Patterns That Are NOT Measures

These patterns should NOT be extracted as measure references:

### Column References
Format: `'TableName'[ColumnName]`
**Example**: `'Customer'[First Name]`, `'Product'[Color]`

### Iterator Expressions
Format: `IterCols(N)(...)`
**Example**: `IterCols(0)('Customer'[First Name])`

### Dependency Lists
Format: `DependOnCols(N, M)(...)`
**Example**: `DependOnCols(0, 1)('Date'[Year], 'Date'[Month])`

---

## Query-Scoped Measures

Measures defined with `DEFINE MEASURE` in the query text are parsed separately:

```regex
DEFINE\s+MEASURE\s+(?:'[^']*')?\[([^\]]+)\]\s*=\s*
```

**Matches**:
- `DEFINE MEASURE 'Table'[MeasureName] = expression`
- `DEFINE MEASURE [MeasureName] = expression`

The expression continues until the next `DEFINE`, `EVALUATE`, `VAR`, or end of query.

---

## Unit Tests

See `tests/DaxStudio.Tests/VisualQueryPlan/PlanNodeViewModelTests.cs` for comprehensive test cases covering:
- `MeasureReference_WithMeasureRefEquals_ExtractsMeasureName`
- `MeasureReference_WithDoubleQuoteBrackets_ExtractsMeasureName`
- `MeasureReference_ScanVertipaq_WithIterCols_NotExtracted`
- `MeasureReference_ColumnReference_NotExtracted`
- `MeasureReference_LogOpSumVertipaq_ExtractsSimpleName`
- And more...

---

*Last updated: 2025-12-23*
