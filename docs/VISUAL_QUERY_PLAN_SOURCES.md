# Visual Query Plan - Source Attribution

This document lists the sources consulted during the development of the Visual Query Plan feature improvements.

## SQLBI Documentation

### Logical Query Plan
- **URL**: https://docs.sqlbi.com/dax-internals/vertipaq/logical-query-plan
- **Key concepts**: 11 primary logical operators (GroupBy_Vertipaq, Sum_Vertipaq, Scan_Vertipaq, Filter_Vertipaq, etc.)
- **Used for**: Understanding operator types and their purposes

### Physical Query Plan
- **URL**: https://docs.sqlbi.com/dax-internals/vertipaq/physical-query-plan
- **Key concepts**: Physical operators (Cache, DataPostFilter, CrossApply, SpoolLookup, SpoolIterator, etc.)
- **Used for**: Understanding execution mechanics and performance implications

### xmSQL Reference
- **URL**: https://docs.sqlbi.com/dax-internals/vertipaq/xmSQL
- **Key concepts**: Storage Engine query language, pseudo-SQL syntax
- **Used for**: Understanding SE query display and interpretation

## PDF Documentation

### DAX Query Plans (SQLBI)
- **Author**: Alberto Ferrari / SQLBI
- **Pages**: 29
- **Key concepts**:
  - Formula Engine (FE) vs Storage Engine (SE) architecture
  - FE is single-threaded, SE is multi-threaded
  - CallbackDataID: SE calling back to FE during table scan (NOT cached - performance red flag)
  - #Records metric interpretation
  - Excessive materialization detection (CrossApply generating millions of rows)
  - Query plan optimization strategies

## Blog Posts

### MDX DAX Blog
- **URL**: mdxdax.blogspot.com
- **Topics consulted**:
  - Graphical query plans for DAX
  - Query plan visualization techniques
  - Performance analysis methodologies

## Implementation Guidelines

Based on these sources, the following thresholds and indicators were implemented:

### Row Count Thresholds
| Rows | Color | Status |
|------|-------|--------|
| < 10,000 | Green | Acceptable |
| < 100,000 | Yellow | Concerning |
| 1,000,000+ | Red | Excessive materialization |

### Engine Type Indicators
- **SE (Storage Engine)**: Green badge - Multi-threaded, highly optimized
- **FE (Formula Engine)**: Purple badge - Single-threaded, handles complex DAX

### Performance Concerns
- **CallbackDataID**: Orange "CB" badge - SE calling FE, results not cached
- **Excessive rows**: Red row count - Indicates materialization problems
- **Cache hits**: Blue "Cache" badge - Query used cached data

## License

Sources are attributed to their respective authors. SQLBI documentation is copyrighted by SQLBI.

---
*Generated during Visual Query Plan Phase 2 development*
