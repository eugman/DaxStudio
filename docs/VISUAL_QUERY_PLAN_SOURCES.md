# Visual Query Plan - Sources

References consulted for DAX query plan parsing and visualization.

---

## SQLBI (Alberto Ferrari, Marco Russo)

- https://docs.sqlbi.com/dax-internals/vertipaq/logical-query-plan
  - Documents 11 logical operators including VarScope, ScalarVarProxy, TableVarProxy, GroupSemiJoin.

- https://docs.sqlbi.com/dax-internals/vertipaq/physical-query-plan
  - Documents physical execution operators and performance implications.

- https://docs.sqlbi.com/dax-internals/vertipaq/xmSQL
  - Comprehensive xmSQL reference: implicit GROUP BY, JOIN types, aggregations, callbacks, batches, bitmap indexing.

- https://www.sqlbi.com/articles/formula-engine-and-storage-engine-in-dax/
  - FE/SE architecture overview: single-threaded FE, multi-threaded SE, datacache materialization, VertiPaq vs DirectQuery storage engines.

## MDX DAX Blog (Alberto Ferrari)

- https://mdxdax.blogspot.com/2011/12/dax-query-plan-part-1-introduction.html
  - Introduces plan types, operator suffixes (ScaLogOp, RelLogOp, LookupPhyOp, IterPhyOp).

- https://mdxdax.blogspot.com/2012/01/dax-query-plan-part-2-operator.html
  - Details operator properties (DependOnCols, RequiredCols, IterCols, LookupCols) and column list format.

- https://mdxdax.blogspot.com/2012/03/dax-query-plan-part-3-vertipaq.html
  - Documents VertiPaq logical operators, Scan_Vertipaq properties (JoinCols, SemijoinCols, BlankRow), and aggregations.

## DaxStudio Documentation

- https://daxstudio.org/docs/features/traces/server-timings-trace/
  - Server Timings metrics: Total, SE CPU, FE, SE, SE Queries, SE Cache; FE calculated as Total minus SE duration.

## PDF Documents

- https://www.sqlbi.com/wp-content/uploads/DAX-Query-Plans.pdf (Alberto Ferrari / SQLBI, 29 pages)
  - Comprehensive optimization guide: FE vs SE architecture, CallbackDataID behavior, cache usage, #Records materialization, CONTAINS translation pattern, xmSQL DC_KIND/PFCAST/COALESCE.

## dax.guide

- https://dax.guide/dt/variant/
  - Documents the Variant data type: used for expressions returning different types based on conditions (e.g., IF returning number or string). Cannot be used for table columns, only in measures/expressions.

- https://dax.guide/op/table-constructor/
  - Documents the table constructor `{ }` syntax. Returns TableCtor operator in query plans.

## Microsoft Learn (Official DAX Reference)

- https://learn.microsoft.com/en-us/dax/table-constructor
  - Documents the table constructor syntax using curly braces `{ }`. Single column: `{ value1, value2 }` creates 'Value' column. Multi-column: `{ (a, b), (c, d) }` creates Value1, Value2 columns. Values are type-coerced when mixed.

---

*Last updated: 2025-12-27*
