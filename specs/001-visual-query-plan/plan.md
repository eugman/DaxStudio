# Implementation Plan: Visual Query Plan

**Branch**: `001-visual-query-plan` | **Date**: 2025-12-22 | **Spec**: [spec.md](./spec.md)
**Input**: Feature specification from `/specs/001-visual-query-plan/spec.md`

## Summary

Implement a graphical query plan visualization feature for DaxStudio that renders DAX physical and logical execution plans as interactive node graphs. The feature will extend the existing QueryPlanTraceViewModel infrastructure, adding graph-based visualization with automatic detection of performance anti-patterns (Excessive Materialization, CallbackDataID), View Metrics correlation, and column ID resolution.

## Technical Context

**Language/Version**: C# (.NET Framework 4.7.2+, matching DaxStudio target)
**Primary Dependencies**: Caliburn.Micro (MVVM), WPF, existing DaxStudio.QueryTrace infrastructure
**Graph Rendering**: MSAGL (Microsoft Automatic Graph Layout) via NuGet for hierarchical DAG layout
**Storage**: JSON serialization via Newtonsoft.Json (consistent with existing .queryPlans format)
**Testing**: xUnit (consistent with DaxStudio.Tests project)
**Target Platform**: Windows Desktop (WPF)
**Project Type**: WPF Desktop Application (existing multi-project solution)
**Performance Goals**: Plan rendering < 2 seconds for 200 nodes, UI responsive during parsing
**Constraints**: < 100MB memory for visualization, must integrate with existing theme system
**Scale/Scope**: Plans with 1-200+ nodes, single-user desktop app

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### I. Visualization Clarity
- [x] Plans displayed as DAGs: **COMPLIANT** - Graph layout algorithm will render as DAG
- [x] Node sizing/coloring reflects cost: **COMPLIANT** - FR-003 requires visual cost encoding
- [x] Unambiguous data flow direction: **COMPLIANT** - Top-to-bottom layout planned
- [x] Readable with 50+ nodes: **COMPLIANT** - FR-015 requires 200+ node support
- [x] Critical path distinguishable: **COMPLIANT** - FR-004 requires distinct critical path

### II. Performance Insight Priority
- [x] Excessive Materialization flagged: **COMPLIANT** - FR-005
- [x] CallbackDataID highlighted: **COMPLIANT** - FR-006
- [x] SE/FE time split displayed: **COMPLIANT** - FR-007
- [x] High cardinality warnings: **DEFERRED** - Constitution updated to SHOULD (v1.0.1) per clarification
- [x] View Metrics correlation: **COMPLIANT** - FR-012

### III. Integration First
- [x] MVVM pattern: **COMPLIANT** - Will extend TraceWatcherBaseViewModel pattern
- [x] Caliburn.Micro conventions: **COMPLIANT** - Same patterns as QueryPlanTraceViewModel
- [x] DaxStudio.QueryTrace infrastructure: **COMPLIANT** - Extends existing DAXQueryPlan event handling
- [x] DaxStudio styling: **COMPLIANT** - Will use existing theme resources
- [x] No conflicting UI frameworks: **COMPLIANT** - Pure WPF implementation

### IV. Interactive Exploration
- [x] Click for node details: **COMPLIANT** - FR-008
- [x] Keyboard navigation: **COMPLIANT** - FR-010
- [x] DAX text correlation: **COMPLIANT** - FR-014 (SHOULD)
- [x] Zoom and pan: **COMPLIANT** - FR-009
- [x] Collapse/expand subtrees: **COMPLIANT** - FR-011

### V. Testability
- [x] Parsing separated from rendering: **COMPLIANT** - Design separates EnrichedPlanNode model from view
- [x] Serializable data structures: **COMPLIANT** - Extends existing JSON serialization
- [x] ViewModels testable without Views: **COMPLIANT** - Standard MVVM practice
- [x] Integration test for end-to-end: **COMPLIANT** - Will add test with sample plan data
- [x] Test coverage for plan structures: **COMPLIANT** - Unit tests for DirectQuery, Import, Composite

**Constitution Status**: All applicable principles PASS.

## Project Structure

### Documentation (this feature)

```text
specs/001-visual-query-plan/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research findings
├── data-model.md        # Phase 1 data model design
├── quickstart.md        # Phase 1 developer quickstart
├── contracts/           # Phase 1 internal interface contracts
└── checklists/          # Quality checklists
    └── requirements.md
```

### Source Code (repository root)

```text
src/DaxStudio.UI/
├── ViewModels/
│   ├── QueryPlanTraceViewModel.cs      # Existing - extend or keep parallel
│   ├── VisualQueryPlanViewModel.cs     # NEW - graph visualization ViewModel
│   └── PlanNodeDetailsViewModel.cs     # NEW - node details panel ViewModel
├── Views/
│   ├── QueryPlanTraceView.xaml         # Existing - text-based view
│   ├── VisualQueryPlanView.xaml        # NEW - graph visualization view
│   └── PlanNodeDetailsView.xaml        # NEW - node details panel view
├── Model/
│   ├── QueryPlanModel.cs               # Existing - extend for enriched data
│   ├── EnrichedPlanNode.cs             # NEW - enriched node with metrics
│   ├── PerformanceIssue.cs             # NEW - detected anti-pattern
│   └── PlanGraphLayout.cs              # NEW - graph layout calculation
├── Controls/
│   └── PlanGraphControl.cs             # NEW - WPF custom control for graph rendering
└── Services/
    ├── PlanEnrichmentService.cs        # NEW - enriches plan with View Metrics
    ├── ColumnNameResolver.cs           # NEW - resolves column IDs to names
    └── PerformanceIssueDetector.cs     # NEW - detects anti-patterns

src/DaxStudio.Interfaces/
└── IQueryPlanRow.cs                    # Existing interface

tests/DaxStudio.Tests/
├── VisualQueryPlan/
│   ├── PlanEnrichmentServiceTests.cs   # NEW
│   ├── ColumnNameResolverTests.cs      # NEW
│   ├── PerformanceIssueDetectorTests.cs # NEW
│   └── PlanGraphLayoutTests.cs         # NEW
└── TestData/
    └── SampleQueryPlans/               # NEW - sample plan JSON for tests
```

**Structure Decision**: Integrate into existing DaxStudio.UI project following established patterns. New ViewModels extend or complement existing QueryPlanTraceViewModel. Services layer handles data enrichment logic separated from UI.

## Complexity Tracking

No constitution violations requiring justification. All design decisions align with existing DaxStudio patterns.
