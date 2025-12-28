# Tasks: Visual Query Plan

**Input**: Design documents from `/specs/001-visual-query-plan/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Tests included per Constitution Principle V (Testability) requirement.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **DaxStudio.UI**: `src/DaxStudio.UI/`
- **DaxStudio.Tests**: `tests/DaxStudio.Tests/`
- **DaxStudio.Interfaces**: `src/DaxStudio.Interfaces/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, NuGet dependencies, and shared folder structure

- [ ] T001 Add MSAGL NuGet package reference to src/DaxStudio.UI/DaxStudio.UI.csproj
- [ ] T002 [P] Create folder src/DaxStudio.UI/Services/ if not exists
- [ ] T003 [P] Create folder src/DaxStudio.UI/Controls/ if not exists
- [ ] T004 [P] Create folder tests/DaxStudio.Tests/VisualQueryPlan/
- [ ] T005 [P] Create folder tests/DaxStudio.Tests/TestData/SampleQueryPlans/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, enums, and services that ALL user stories depend on

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Core Enumerations

- [ ] T006 [P] Create PlanType enum (Physical, Logical) in src/DaxStudio.UI/Model/PlanType.cs
- [ ] T007 [P] Create EngineType enum (Unknown, StorageEngine, FormulaEngine) in src/DaxStudio.UI/Model/EngineType.cs
- [ ] T008 [P] Create IssueType enum (ExcessiveMaterialization, CallbackDataID) in src/DaxStudio.UI/Model/IssueType.cs
- [ ] T009 [P] Create IssueSeverity enum (Info, Warning, Error) in src/DaxStudio.UI/Model/IssueSeverity.cs

### Core Models

- [ ] T010 [P] Create PerformanceIssue model class in src/DaxStudio.UI/Model/PerformanceIssue.cs
- [ ] T011 [P] Create EnrichedPlanNode model class in src/DaxStudio.UI/Model/EnrichedPlanNode.cs
- [ ] T012 Create EnrichedQueryPlan model class in src/DaxStudio.UI/Model/EnrichedQueryPlan.cs (depends on T010, T011)

### Core Services

- [ ] T013 [P] Create IColumnNameResolver interface in src/DaxStudio.UI/Services/IColumnNameResolver.cs
- [ ] T014 [P] Create IPerformanceIssueDetector interface in src/DaxStudio.UI/Services/IPerformanceIssueDetector.cs
- [ ] T015 [P] Create IPlanEnrichmentService interface in src/DaxStudio.UI/Services/IPlanEnrichmentService.cs
- [ ] T016 Create ColumnNameResolver implementation in src/DaxStudio.UI/Services/ColumnNameResolver.cs (depends on T013)
- [ ] T017 Create PerformanceIssueDetector implementation in src/DaxStudio.UI/Services/PerformanceIssueDetector.cs (depends on T014)
- [ ] T018 Create PlanEnrichmentService implementation in src/DaxStudio.UI/Services/PlanEnrichmentService.cs (depends on T015, T016, T017)
- [ ] T018a [P] Ensure all plan parsing methods use async/await patterns for FR-016 responsiveness in src/DaxStudio.UI/Services/PlanEnrichmentService.cs

### Test Data

- [ ] T019 [P] Create sample physical query plan JSON in tests/DaxStudio.Tests/TestData/SampleQueryPlans/SimplePhysicalPlan.json
- [ ] T020 [P] Create sample plan with excessive materialization in tests/DaxStudio.Tests/TestData/SampleQueryPlans/ExcessiveMaterializationPlan.json
- [ ] T021 [P] Create sample plan with CallbackDataID in tests/DaxStudio.Tests/TestData/SampleQueryPlans/CallbackDataIdPlan.json

### Unit Tests for Core Services

- [ ] T022 [P] Create ColumnNameResolverTests in tests/DaxStudio.Tests/VisualQueryPlan/ColumnNameResolverTests.cs
- [ ] T023 [P] Create PerformanceIssueDetectorTests in tests/DaxStudio.Tests/VisualQueryPlan/PerformanceIssueDetectorTests.cs
- [ ] T024 Create PlanEnrichmentServiceTests in tests/DaxStudio.Tests/VisualQueryPlan/PlanEnrichmentServiceTests.cs

**Checkpoint**: Foundation ready - core models, services, and tests in place. User story implementation can now begin.

---

## Phase 3: User Story 1 - View Query Execution Plan (Priority: P1) 🎯 MVP

**Goal**: Display physical query plans as interactive graphical node diagrams in a dockable tool window

**Independent Test**: Execute any DAX query → Click "Visual Query Plan" → Graphical plan appears with operators as connected nodes

### Tests for User Story 1

- [ ] T025 [P] [US1] Create PlanGraphLayoutTests in tests/DaxStudio.Tests/VisualQueryPlan/PlanGraphLayoutTests.cs
- [ ] T026 [P] [US1] Create VisualQueryPlanViewModelTests in tests/DaxStudio.Tests/VisualQueryPlan/VisualQueryPlanViewModelTests.cs

### Implementation for User Story 1

- [ ] T027 [P] [US1] Create PlanGraphLayout class for DAG layout calculation in src/DaxStudio.UI/Model/PlanGraphLayout.cs
- [ ] T028 [P] [US1] Create PlanNodeViewModel for individual node display in src/DaxStudio.UI/ViewModels/PlanNodeViewModel.cs
- [ ] T029 [US1] Create VisualQueryPlanViewModel extending TraceWatcherBaseViewModel in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T030 [US1] Create PlanGraphControl WPF custom control in src/DaxStudio.UI/Controls/PlanGraphControl.cs
- [ ] T031 [US1] Create VisualQueryPlanView.xaml with graph control and toolbar in src/DaxStudio.UI/Views/VisualQueryPlanView.xaml
- [ ] T032 [US1] Create VisualQueryPlanView.xaml.cs code-behind in src/DaxStudio.UI/Views/VisualQueryPlanView.xaml.cs
- [ ] T033 [US1] Add node tooltip template showing key metrics in src/DaxStudio.UI/Views/VisualQueryPlanView.xaml
- [ ] T034 [US1] Implement cost-based node coloring (color intensity by relative cost) in src/DaxStudio.UI/Controls/PlanGraphControl.cs
- [ ] T035 [US1] Implement zoom and pan support using existing ZoomableUserControl pattern in src/DaxStudio.UI/Views/VisualQueryPlanView.xaml
- [ ] T036 [US1] Register VisualQueryPlanViewModel with MEF export attribute in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T037 [US1] Add "Visual Query Plan" icon resource in src/DaxStudio.UI/Resources/

**Checkpoint**: User Story 1 complete. Can view graphical query plans with cost visualization, tooltips, zoom/pan.

---

## Phase 4: User Story 2 - Identify Performance Issues (Priority: P2)

**Goal**: Automatically detect and highlight Excessive Materialization and CallbackDataID anti-patterns

**Independent Test**: Execute a query causing Excessive Materialization → Plan displays with warning indicator on affected node

### Implementation for User Story 2

- [ ] T038 [US2] Add issue detection integration to PlanEnrichmentService in src/DaxStudio.UI/Services/PlanEnrichmentService.cs
- [ ] T039 [US2] Create IssuesPanelViewModel for issues summary display in src/DaxStudio.UI/ViewModels/IssuesPanelViewModel.cs
- [ ] T040 [US2] Add warning indicator visual style (border/icon) to PlanGraphControl in src/DaxStudio.UI/Controls/PlanGraphControl.cs
- [ ] T041 [US2] Create IssuesPanel.xaml showing list of detected issues in src/DaxStudio.UI/Views/IssuesPanel.xaml
- [ ] T042 [US2] Add issues summary bar to VisualQueryPlanView.xaml in src/DaxStudio.UI/Views/VisualQueryPlanView.xaml
- [ ] T043 [US2] Implement issue click navigation (click issue → select affected node) in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T044 [US2] Add issue explanation and remediation tooltip content in src/DaxStudio.UI/Views/IssuesPanel.xaml

**Checkpoint**: User Story 2 complete. Performance anti-patterns auto-detected and visually flagged.

---

## Phase 5: User Story 3 - Explore Node Details (Priority: P3)

**Goal**: Click any node to see complete operator details in a properties panel

**Independent Test**: Click any plan node → Details panel shows operator name, duration, rows, engine type

### Implementation for User Story 3

- [ ] T045 [P] [US3] Create PlanNodeDetailsViewModel in src/DaxStudio.UI/ViewModels/PlanNodeDetailsViewModel.cs
- [ ] T046 [US3] Create PlanNodeDetailsView.xaml with properties grid in src/DaxStudio.UI/Views/PlanNodeDetailsView.xaml
- [ ] T047 [US3] Add node selection binding in VisualQueryPlanViewModel in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T048 [US3] Integrate details panel into VisualQueryPlanView layout in src/DaxStudio.UI/Views/VisualQueryPlanView.xaml
- [ ] T049 [US3] Add SE-specific metrics display (scan time, cache status) in src/DaxStudio.UI/ViewModels/PlanNodeDetailsViewModel.cs
- [ ] T050 [US3] Add FE-specific metrics display (calculation time, iterators) in src/DaxStudio.UI/ViewModels/PlanNodeDetailsViewModel.cs
- [ ] T051 [US3] Implement keyboard navigation between nodes (arrow keys) in src/DaxStudio.UI/Controls/PlanGraphControl.cs
- [ ] T052 [US3] Implement collapse/expand subtree functionality in src/DaxStudio.UI/Controls/PlanGraphControl.cs

**Checkpoint**: User Story 3 complete. Full node inspection with SE/FE metrics and keyboard navigation.

---

## Phase 6: User Story 4 - View Logical vs Physical Plan (Priority: P4)

**Goal**: Toggle between logical and physical query plan views

**Independent Test**: Execute query → Toggle "Logical Plan" → View changes to show logical operators

### Implementation for User Story 4

- [ ] T053 [US4] Add PlanType toggle property to VisualQueryPlanViewModel in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T054 [US4] Add plan type toggle buttons to VisualQueryPlanView toolbar in src/DaxStudio.UI/Views/VisualQueryPlanView.xaml
- [ ] T055 [US4] Implement logical plan enrichment in PlanEnrichmentService in src/DaxStudio.UI/Services/PlanEnrichmentService.cs
- [ ] T056 [US4] Preserve zoom/scroll position when toggling plan types in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T057 [US4] Add distinct visual styling for logical plan nodes in src/DaxStudio.UI/Controls/PlanGraphControl.cs

**Checkpoint**: User Story 4 complete. Can toggle between physical and logical plan views.

---

## Phase 7: User Story 5 - Correlate Plan with Query Text (Priority: P5)

**Goal**: Click a plan node to highlight corresponding DAX expression in editor

**Independent Test**: Click a plan node → Corresponding DAX function (SUMMARIZECOLUMNS, etc.) highlights in editor

### Implementation for User Story 5

- [ ] T058 [US5] Create HighlightDaxTextEvent message class in src/DaxStudio.UI/Events/HighlightDaxTextEvent.cs
- [ ] T059 [US5] Add query text correlation logic to EnrichedPlanNode in src/DaxStudio.UI/Model/EnrichedPlanNode.cs
- [ ] T060 [US5] Publish highlight event on node selection in VisualQueryPlanViewModel in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T061 [US5] Handle highlight event in DAXEditor integration in src/DAXEditor/ (or src/DaxStudio.UI/ViewModels/DocumentViewModel.cs)
- [ ] T062 [US5] Clear previous highlight when selecting different node in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs

**Checkpoint**: User Story 5 complete. Plan nodes correlate with DAX query text.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T063 [P] Add JSON serialization support for enriched plans (export/import) in src/DaxStudio.UI/Model/EnrichedQueryPlan.cs
- [ ] T064 [P] Implement ISaveState interface in VisualQueryPlanViewModel for .daxx file support in src/DaxStudio.UI/ViewModels/VisualQueryPlanViewModel.cs
- [ ] T065 [P] Add integration test with full plan capture and display in tests/DaxStudio.Tests/VisualQueryPlan/IntegrationTests.cs
- [ ] T066 Performance optimization: virtualize rendering for 200+ node plans in src/DaxStudio.UI/Controls/PlanGraphControl.cs
- [ ] T067 Add critical path highlighting (longest duration path) in src/DaxStudio.UI/Controls/PlanGraphControl.cs
- [ ] T068 Add DirectQuery plan support with appropriate operator labels in src/DaxStudio.UI/Services/PlanEnrichmentService.cs
- [ ] T069 Manual validation: test with sample AdventureWorks queries
- [ ] T070 Run all existing DaxStudio tests to ensure no regressions

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational - MVP delivery target
- **User Story 2 (Phase 4)**: Can start after Foundational; benefits from US1 for visual integration
- **User Story 3 (Phase 5)**: Can start after Foundational; benefits from US1 for context
- **User Story 4 (Phase 6)**: Can start after Foundational; depends on US1 for view infrastructure
- **User Story 5 (Phase 7)**: Can start after Foundational; depends on US1+US3 for selection
- **Polish (Phase 8)**: Depends on desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: After Foundational - No dependencies on other stories ✓
- **User Story 2 (P2)**: After Foundational - Integrates with US1 but independently testable ✓
- **User Story 3 (P3)**: After Foundational - Integrates with US1 but independently testable ✓
- **User Story 4 (P4)**: After Foundational - Requires US1 infrastructure
- **User Story 5 (P5)**: After Foundational - Requires US1+US3 selection infrastructure

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002-T005)
- All enum definitions can run in parallel (T006-T009)
- Core model creation for PerformanceIssue and EnrichedPlanNode in parallel (T010-T011)
- Service interfaces can be created in parallel (T013-T015)
- Test data files can be created in parallel (T019-T021)
- Test classes for core services in parallel (T022-T023)

---

## Parallel Example: Foundational Phase

```bash
# Launch all enums in parallel:
Task: "Create PlanType enum in src/DaxStudio.UI/Model/PlanType.cs"
Task: "Create EngineType enum in src/DaxStudio.UI/Model/EngineType.cs"
Task: "Create IssueType enum in src/DaxStudio.UI/Model/IssueType.cs"
Task: "Create IssueSeverity enum in src/DaxStudio.UI/Model/IssueSeverity.cs"

# Launch all service interfaces in parallel:
Task: "Create IColumnNameResolver interface"
Task: "Create IPerformanceIssueDetector interface"
Task: "Create IPlanEnrichmentService interface"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T005)
2. Complete Phase 2: Foundational (T006-T024)
3. Complete Phase 3: User Story 1 (T025-T037)
4. **STOP and VALIDATE**: Test with real DAX queries
5. Demo graphical query plan visualization

### Incremental Delivery

1. Setup + Foundational → Core infrastructure ready
2. Add User Story 1 → MVP: Graphical plan viewing
3. Add User Story 2 → Auto issue detection
4. Add User Story 3 → Node details panel
5. Add User Story 4 → Logical/Physical toggle
6. Add User Story 5 → DAX text correlation
7. Each story adds capability without breaking previous stories

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together
2. Once Foundational is done:
   - Developer A: User Story 1 (core visualization)
   - Developer B: User Story 2 (issue detection - can prototype while A works)
   - Developer C: User Story 3 (details panel - can prototype while A works)
3. Stories integrate via shared models and events

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Constitution Principle V requires testability - tests included for core services and ViewModels
