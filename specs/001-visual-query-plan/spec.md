# Feature Specification: Visual Query Plan

**Feature Branch**: `001-visual-query-plan`
**Created**: 2025-12-22
**Status**: Draft
**Input**: User description: "Visual Query Plan - graphical execution plan visualization for DAX queries similar to SQL Server execution plans, displaying physical and logical DAX execution plans plus View Metrics details to help performance tune and identify issues like Excessive Materialization"

## Clarifications

### Session 2025-12-22

- Q: Where should the query plan visualization appear in DaxStudio's UI? → A: Dockable tool window (like Output, Query History panels)
- Q: How should plan data be sourced and processed? → A: Extend existing QueryPlanModel AND enrich with additional data (View Metrics row counts/timings, resolve column IDs to names, simplify/combine hierarchies where appropriate)
- Q: Which performance anti-patterns should be detected initially? → A: Start with Excessive Materialization + CallbackDataID only; expand to additional patterns (high cardinality, unnecessary scans, etc.) in future iterations

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Query Execution Plan (Priority: P1)

As a DAX developer, I want to see a graphical representation of my query's execution plan after running a query, so I can understand how the engine processes my DAX and identify which operations consume the most resources.

**Why this priority**: This is the core functionality that enables all other features. Without the ability to visualize a query plan, no other performance analysis features can work.

**Independent Test**: Can be fully tested by executing any DAX query and verifying a graphical plan appears showing the query's physical operators as connected nodes.

**Acceptance Scenarios**:

1. **Given** a connected session with a query in the editor, **When** the user executes the query and clicks "View Query Plan", **Then** a dockable tool window opens displaying the physical query plan as a tree/graph of operators
2. **Given** a query plan is displayed, **When** the user hovers over a node, **Then** a tooltip shows the operator's key metrics (timing, rows processed)
3. **Given** a query plan with multiple operators, **When** the plan renders, **Then** node sizes or colors indicate relative cost/duration of each operator
4. **Given** a complex query plan with 50+ nodes, **When** the plan renders, **Then** the layout remains readable with no overlapping nodes

---

### User Story 2 - Identify Performance Issues (Priority: P2)

As a DAX developer, I want the query plan visualization to automatically highlight performance anti-patterns like Excessive Materialization and CallbackDataID operations, so I can quickly find and fix bottlenecks without manually inspecting each node.

**Why this priority**: Once users can view plans (P1), the next most valuable capability is automatic issue detection. This transforms a passive display into an active diagnostic tool.

**Independent Test**: Can be tested by executing a query known to cause Excessive Materialization and verifying the visualization flags the problematic operator with a warning indicator.

**Acceptance Scenarios**:

1. **Given** a query that causes Excessive Materialization, **When** the query plan is displayed, **Then** the affected operator is highlighted with a warning indicator (icon, border, or color)
2. **Given** a query with CallbackDataID operations, **When** the query plan is displayed, **Then** the CallbackDataID operations are flagged as potential bottlenecks
3. **Given** multiple performance issues in one plan, **When** the user views the plan, **Then** a summary panel lists all detected issues with severity and affected nodes
4. **Given** a highlighted issue, **When** the user clicks the warning indicator, **Then** the system displays an explanation of the issue and suggested remediation

---

### User Story 3 - Explore Node Details (Priority: P3)

As a DAX developer, I want to click on any node in the query plan to see its complete details (operator type, timing breakdown, row counts, data source), so I can deeply analyze specific parts of my query execution.

**Why this priority**: After viewing and identifying issues (P1, P2), users need detailed inspection capabilities to understand root causes.

**Independent Test**: Can be tested by clicking any node in a displayed plan and verifying a properties panel shows all available operator metrics.

**Acceptance Scenarios**:

1. **Given** a displayed query plan, **When** the user clicks a node, **Then** a details panel opens showing all operator properties (name, type, duration, rows in/out, memory)
2. **Given** the details panel is open, **When** the user clicks a different node, **Then** the panel updates to show the newly selected node's details
3. **Given** a plan with Storage Engine operations, **When** viewing node details, **Then** the panel shows SE-specific metrics (scan time, cache status, partition counts)
4. **Given** a plan with Formula Engine operations, **When** viewing node details, **Then** the panel shows FE-specific metrics (calculation time, iterator operations)

---

### User Story 4 - View Logical vs Physical Plan (Priority: P4)

As a DAX developer, I want to switch between viewing the logical query plan and the physical query plan, so I can understand both what the engine decided to do (logical) and how it executed (physical).

**Why this priority**: This provides deeper analysis capability for advanced users but is not required for basic performance tuning.

**Independent Test**: Can be tested by executing a query and toggling between Logical and Physical plan views, verifying each displays different operator representations.

**Acceptance Scenarios**:

1. **Given** a query has been executed, **When** the user selects "Logical Plan" view, **Then** the visualization shows the logical operator tree
2. **Given** a query has been executed, **When** the user selects "Physical Plan" view, **Then** the visualization shows the physical operator tree with execution details
3. **Given** the user is viewing one plan type, **When** they switch to the other type, **Then** the visualization updates while maintaining zoom level and scroll position where applicable

---

### User Story 5 - Correlate Plan with Query Text (Priority: P5)

As a DAX developer, I want to click on a query plan node and have the corresponding DAX expression highlighted in my query editor, so I can understand which part of my query generated each operator.

**Why this priority**: This is an advanced integration feature that connects the plan visualization back to the source query. Valuable but not essential for core functionality.

**Independent Test**: Can be tested by selecting a plan node and verifying the editor highlights the corresponding SUMMARIZECOLUMNS, CALCULATE, or other DAX expression.

**Acceptance Scenarios**:

1. **Given** a displayed query plan and the query text in the editor, **When** the user selects a plan node, **Then** the corresponding DAX expression in the editor is highlighted
2. **Given** a highlighted DAX expression, **When** the user clicks elsewhere in the plan, **Then** the previous highlight clears and new highlight appears (if applicable)

---

### Edge Cases

- What happens when the query returns no results? The plan should still display since query execution occurred
- What happens when the query fails with an error? Display any partial plan available or show a clear message that no plan is available
- What happens when connection is lost mid-capture? Display an error message and any partial data captured
- How does the system handle queries with no Server Timings trace available? Show a message indicating trace data is required and provide guidance to enable it
- What happens with DirectQuery plans that have different operator types? Display DirectQuery-specific operators with appropriate labeling
- How does the system handle very large plans (200+ nodes)? Provide zoom controls, pan navigation, and automatic layout without overlapping

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST capture and display physical query execution plans as graphical node diagrams
- **FR-002**: System MUST capture and display logical query execution plans as graphical node diagrams
- **FR-003**: System MUST visually differentiate operators by relative cost using node size, color intensity, or similar visual encoding
- **FR-004**: System MUST display the critical execution path (longest duration path) distinctly
- **FR-005**: System MUST automatically detect and flag Excessive Materialization events with warning indicators
- **FR-006**: System MUST automatically detect and flag CallbackDataID operations with warning indicators
- **FR-007**: System MUST display Storage Engine (SE) vs Formula Engine (FE) time breakdown
- **FR-008**: Users MUST be able to click any node to view detailed operator properties
- **FR-009**: System MUST support zoom and pan interactions for navigating large plans
- **FR-010**: System MUST support keyboard navigation between nodes (arrow keys)
- **FR-011**: Users MUST be able to collapse and expand subtrees in complex plans
- **FR-012**: System MUST correlate View Metrics data (timing, row counts) with corresponding plan nodes
- **FR-013**: System MUST support toggling between logical and physical plan views
- **FR-014**: System SHOULD highlight DAX query text regions when corresponding plan nodes are selected
- **FR-015**: System MUST render plans with up to 200 nodes without overlapping or unreadable layouts
- **FR-016**: System MUST remain responsive during plan parsing and rendering (async/non-blocking operations)
- **FR-017**: System MUST resolve column numerical IDs to human-readable column names in plan display
- **FR-019**: System MAY simplify or combine plan hierarchy levels where doing so improves readability without losing diagnostic value

### Key Entities

- **EnrichedQueryPlan**: Represents a complete execution plan with metadata (capture time, query text hash, plan type). Contains a hierarchy of EnrichedPlanNodes. Built by extending existing QueryPlanModel with enriched data.
- **EnrichedPlanNode**: Represents a single operator in the plan. Has parent/child relationships, operator type, timing metrics, row counts, and engine type (SE/FE). Column references resolved from numerical IDs to human-readable names.
- **PerformanceIssue**: Represents a detected anti-pattern (issue type, severity, affected EnrichedPlanNode references, description, remediation guidance).
- **PlanMetrics**: Aggregated metrics for the entire plan (total SE time, total FE time, total rows, issue counts). Correlated from View Metrics data.

## Assumptions

- Query trace data (Server Timings) is available from the existing DaxStudio trace infrastructure
- The user has an active connection to an Analysis Services, Power BI, or PowerPivot data source
- Physical and logical plan data is exposed via existing trace events (DirectQuery plans may have different structure)
- View Metrics functionality already captures timing and row count data that can be correlated with plan nodes

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can view a graphical query plan within 3 seconds of query completion for typical queries
- **SC-002**: Users identify the most expensive operation in a query plan within 10 seconds of viewing
- **SC-003**: 90% of Excessive Materialization events are automatically detected and flagged
- **SC-004**: 90% of CallbackDataID operations are automatically detected and flagged
- **SC-005**: Plans with up to 200 nodes render with readable layouts (no overlapping nodes)
- **SC-006**: Time to diagnose query performance issues is reduced by 50% compared to manual Server Timings analysis (baseline: average time for experienced user to identify top 3 bottlenecks using text-based Server Timings view)
- **SC-007**: Users can navigate to any node in a 100-node plan using keyboard within 30 seconds
