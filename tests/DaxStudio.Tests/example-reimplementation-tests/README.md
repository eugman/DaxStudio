# Visual Query Plan - Reimplementation Test Suite

This folder contains a modular test suite organized by the phases in the reimplementation guide. Each phase builds on the previous, allowing you to test your implementation incrementally.

## Test Organization

```
example-reimplementation-tests/
├── README.md                           # This file
├── Fixtures/                           # Shared test fixtures
│   └── (copy from VisualQueryPlan/Fixtures)
│
├── Phase01_DataModels/                 # Phase 1: Data Models & Basic Parsing
│   ├── OperatorNameExtractionTests.cs  # Extract operator names from operation strings
│   └── PropertyExtractionTests.cs      # Extract #Records, RequiredCols, etc.
│
├── Phase02_OperatorDictionary/         # Phase 2: Operator Dictionary & Engine Classification
│   └── DaxOperatorDictionaryTests.cs   # Operator lookup, display names, engine types
│
├── Phase03_TreeBuilding/               # Phase 3: Basic Tree Building
│   └── BasicTreeBuildingTests.cs       # BuildTree, parent-child relationships
│
├── Phase04_NodeFolding/                # Phase 4: Node Folding (Visual Simplification)
│   └── NodeFoldingTests.cs             # 15-pass folding algorithm tests
│
├── Phase05_TreeLayout/                 # Phase 5: Tree Layout Algorithm
│   └── TreeLayoutTests.cs              # SubtreeWidth, zoom calculations
│
├── Phase07_ServerTimingCorrelation/    # Phase 7: Server Timing Correlation
│   └── ServerTimingCorrelationTests.cs # Match timings to nodes, enrichment
│
└── Phase08_PerformanceIssueDetection/  # Phase 8: Performance Issue Detection
    └── PerformanceIssueDetectionTests.cs # CallbackDataID, materialization warnings
```

## Phase Descriptions

### Phase 1: Data Models & Basic Parsing
- **What to implement**: `EnrichedPlanNode`, `EnrichedQueryPlan`, basic regex patterns
- **Tests verify**: Operator name extraction, property extraction (#Records, RequiredCols, etc.)
- **Prerequisites**: None

### Phase 2: Operator Dictionary & Engine Classification
- **What to implement**: `DaxOperatorDictionary`, `EngineType` enum
- **Tests verify**: Operator lookup, display names, engine classification
- **Prerequisites**: Phase 1

### Phase 3: Basic Tree Building
- **What to implement**: `PlanNodeViewModel`, `BuildTree()` method (basic version)
- **Tests verify**: Parent-child relationships, column reference folding
- **Prerequisites**: Phases 1-2

### Phase 4: Node Folding (Visual Simplification)
- **What to implement**: 15-pass folding algorithm in `BuildTree()`
- **Tests verify**: Filter predicates, spool folding, arithmetic chains, engine transitions
- **Prerequisites**: Phases 1-3

### Phase 5: Tree Layout Algorithm
- **What to implement**: `SubtreeWidth`, `VisibleSubtreeWidth`, `ZoomHelper`
- **Tests verify**: Width calculations, zoom-to-cursor behavior, collapse handling
- **Prerequisites**: Phases 1-4

### Phase 6: WPF Visualization (Not Unit Tested)
- **What to implement**: `VisualQueryPlanView.xaml`, node templates, edge templates
- **Tests**: Manual/integration testing recommended
- **Prerequisites**: Phases 1-5

### Phase 7: Server Timing Correlation
- **What to implement**: `PlanEnrichmentService`, timing correlation logic
- **Tests verify**: Timing assignment, records from traces, engine type detection
- **Prerequisites**: Phases 1-6

### Phase 8: Performance Issue Detection
- **What to implement**: `PerformanceIssueDetector`, issue types
- **Tests verify**: CallbackDataID detection, excessive materialization, deduplication
- **Prerequisites**: Phases 1-7

### Phase 9: Interactive Features (Not Unit Tested)
- **What to implement**: Expand/collapse, zoom controls, selection
- **Tests**: Manual/integration testing recommended

### Phase 10: Polish & Animation (Not Unit Tested)
- **What to implement**: `CanvasPositionAnimation`, smooth transitions
- **Tests**: Manual/integration testing recommended

## Running Tests

Each phase can be run independently:

```powershell
# Run Phase 1 tests
vstest.console.exe DaxStudio.Tests.dll --Tests:Phase01

# Run Phase 2 tests
vstest.console.exe DaxStudio.Tests.dll --Tests:Phase02

# Run all reimplementation tests
vstest.console.exe DaxStudio.Tests.dll --Tests:ExampleReimplementation
```

## Cross-Reference

Each test file includes a reference to the corresponding section in:
- `docs/VISUAL_QUERY_PLAN_REIMPLEMENTATION_GUIDE.md`
- `docs/VISUAL_QUERY_PLAN_NODE_FOLDING.md`

## Fixtures

Copy the fixtures from `VisualQueryPlan/Fixtures/` to `example-reimplementation-tests/Fixtures/`:
- `Large plan.dax` - Complex DAX query
- `Large plan Physical Query Plan.tsv` - Physical plan output
- `Large plan Logical Query Plan.tsv` - Logical plan output
- `Large plan Server Timings.tsv` - Server timing events
- `Evaluate Date.dax.*` - Simple query fixtures

## Notes

- Tests are designed to be self-contained within each phase
- Each test file documents its prerequisites
- Some phases (6, 9, 10) are UI-focused and better tested manually
- The folding tests (Phase 4) are the most comprehensive and complex
