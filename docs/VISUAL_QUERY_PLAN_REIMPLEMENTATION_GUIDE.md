# Visual Query Plan - Reimplementation Guide

This guide provides a comprehensive roadmap for implementing the Visual Query Plan feature from scratch. It's designed for a developer who wants to understand the architecture, logic, and implementation details without using AI-generated code.

Each phase builds upon the previous one, delivering incremental value. You can stop at any phase and have a working feature.

---

## Table of Contents

1. [Overview](#overview)
2. [Phase 1: Data Models & Basic Parsing](#phase-1-data-models--basic-parsing)
3. [Phase 2: Operator Dictionary & Engine Classification](#phase-2-operator-dictionary--engine-classification)
4. [Phase 3: Basic Tree Building](#phase-3-basic-tree-building)
5. [Phase 4: Node Folding (Visual Simplification)](#phase-4-node-folding-visual-simplification)
6. [Phase 5: Tree Layout Algorithm](#phase-5-tree-layout-algorithm)
7. [Phase 6: WPF Visualization](#phase-6-wpf-visualization)
8. [Phase 7: Server Timing Correlation](#phase-7-server-timing-correlation)
9. [Phase 8: Performance Issue Detection](#phase-8-performance-issue-detection)
10. [Phase 9: Interactive Features](#phase-9-interactive-features)
11. [Phase 10: Polish & Animation](#phase-10-polish--animation)
12. [Appendix: Key Algorithms](#appendix-key-algorithms)
13. [Appendix: Regex Patterns](#appendix-regex-patterns)
14. [Appendix: Color Schemes](#appendix-color-schemes)

---

## Overview

### What This Feature Does

The Visual Query Plan displays DAX query execution plans as interactive tree diagrams. It shows:

- **Operator hierarchy**: Which operations call which sub-operations
- **Engine classification**: Storage Engine (SE) vs Formula Engine (FE) operations
- **Performance metrics**: Row counts, durations, CPU time from server timings
- **Performance issues**: Callbacks, excessive materialization, high FE ratio

### Architecture Summary

```
┌─────────────────────────────────────────────────────────────────┐
│                    VisualQueryPlanViewModel                      │
│  - Manages trace recording state (Record/Pause/Stop)             │
│  - Holds list of captured plans                                  │
│  - Delegates to BuildTree() for tree construction                │
│  - Delegates to PlanEnrichmentService for timing correlation     │
│  - Delegates to PerformanceIssueDetector for issue detection     │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     PlanNodeViewModel                            │
│  - Wraps EnrichedPlanNode for MVVM binding                       │
│  - Contains BuildTree() static method (15-pass algorithm)        │
│  - Exposes display properties (DisplayText, colors, etc.)        │
│  - Manages expand/collapse state                                 │
│  - Calculates edge geometry for bezier curves                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                        EnrichedPlanNode                          │
│  - Pure data model for plan nodes                                │
│  - Contains: NodeId, Operation, Records, DurationMs, etc.        │
│  - No UI concerns                                                │
└─────────────────────────────────────────────────────────────────┘
```

### Key Files

| File | Purpose |
|------|---------|
| `Model/EnrichedPlanNode.cs` | Data model for plan nodes |
| `Model/EnrichedQueryPlan.cs` | Container for full plan with metrics |
| `Model/DaxOperatorDictionary.cs` | Operator metadata (category, description, engine) |
| `ViewModels/PlanNodeViewModel.cs` | ViewModel wrapping nodes + BuildTree algorithm |
| `ViewModels/VisualQueryPlanViewModel.cs` | Main ViewModel for the feature |
| `Services/PlanEnrichmentService.cs` | Correlates server timings with plan nodes |
| `Services/PerformanceIssueDetector.cs` | Detects performance anti-patterns |
| `Views/VisualQueryPlanView.xaml` | WPF view with Canvas-based rendering |
| `AttachedProperties/CanvasPositionAnimation.cs` | Smooth position animations |

---

## Phase 1: Data Models & Basic Parsing

**Value delivered**: Raw plan data can be captured and stored.

### Step 1.1: Create EnrichedPlanNode

This is the core data model representing a single operator in the plan.

```csharp
public class EnrichedPlanNode
{
    // Identity
    public int NodeId { get; set; }
    public int RowNumber { get; set; }    // Line number in original plan text
    public int Level { get; set; }         // Tree depth (0 = root)

    // Operation data
    public string Operation { get; set; }  // Raw operation text
    public string ResolvedOperation { get; set; }  // With column IDs resolved

    // Metrics (populated later from server timings)
    public long? Records { get; set; }
    public string RecordsSource { get; set; } = "Plan";  // "Plan", "ServerTiming", "Physical", "Inherited"
    public long? DurationMs { get; set; }
    public long? CpuTimeMs { get; set; }
    public long? NetParallelDurationMs { get; set; }
    public int? Parallelism { get; set; }
    public long? EstimatedKBytes { get; set; }
    public double? CostPercentage { get; set; }

    // Classification
    public EngineType EngineType { get; set; } = EngineType.Unknown;
    public bool IsCacheHit { get; set; }

    // xmSQL (for SE operations)
    public string XmSql { get; set; }
    public string ResolvedXmSql { get; set; }

    // Tree structure
    public EnrichedPlanNode Parent { get; set; }
    public List<EnrichedPlanNode> Children { get; set; } = new List<EnrichedPlanNode>();

    // Issues (populated by PerformanceIssueDetector)
    public List<PerformanceIssue> Issues { get; set; } = new List<PerformanceIssue>();

    // Layout (populated by layout algorithm)
    public double X { get; set; }
    public double Y { get; set; }
}

public enum EngineType { Unknown, StorageEngine, FormulaEngine, DirectQuery }
```

### Step 1.2: Create EnrichedQueryPlan

Container for the entire plan with aggregate metrics.

```csharp
public class EnrichedQueryPlan
{
    public string QueryText { get; set; }
    public PlanType PlanType { get; set; }  // Physical or Logical
    public EnrichedPlanNode RootNode { get; set; }
    public List<EnrichedPlanNode> AllNodes { get; set; } = new List<EnrichedPlanNode>();

    // Aggregate metrics
    public long TotalDurationMs { get; set; }
    public long StorageEngineDurationMs { get; set; }
    public long FormulaEngineDurationMs { get; set; }
    public long StorageEngineCpuMs { get; set; }
    public int StorageEngineQueryCount { get; set; }
    public int CacheHits { get; set; }
}

public enum PlanType { Physical, Logical }
```

### Step 1.3: Parse Raw Plan Text

The query plan comes from Analysis Services as indented text. Each line has:
- Indentation (4 spaces per level)
- Row number prefix (e.g., "0: ", "1: ")
- Operation text

**Algorithm**:
1. Split plan text into lines
2. For each line, calculate level from leading spaces (÷ 4)
3. Extract row number before first colon
4. Extract operation text after row number
5. Build parent-child relationships using a stack

**Example plan text**:
```
0: Sum_Vertipaq: IterPhyOp...
    1: Spool_Iterator<SpoolIterator>: IterPhyOp #Records=11...
        2: AggregationSpool<Sum>: SpoolPhyOp #Records=11...
            3: Scan_Vertipaq: IterPhyOp...
```

**Parsing logic**:
```csharp
public EnrichedQueryPlan ParsePlan(string planText, PlanType planType)
{
    var lines = planText.Split('\n');
    var plan = new EnrichedQueryPlan { PlanType = planType };
    var nodeStack = new Stack<EnrichedPlanNode>();

    foreach (var line in lines)
    {
        if (string.IsNullOrWhiteSpace(line)) continue;

        // Calculate indentation level
        int spaces = line.Length - line.TrimStart().Length;
        int level = spaces / 4;

        // Extract row number and operation
        var trimmed = line.TrimStart();
        var colonIdx = trimmed.IndexOf(':');
        var rowNumber = int.Parse(trimmed.Substring(0, colonIdx));
        var operation = trimmed.Substring(colonIdx + 1).Trim();

        var node = new EnrichedPlanNode
        {
            NodeId = plan.AllNodes.Count,
            RowNumber = rowNumber,
            Level = level,
            Operation = operation
        };

        // Pop stack until we find parent at level - 1
        while (nodeStack.Count > 0 && nodeStack.Peek().Level >= level)
            nodeStack.Pop();

        if (nodeStack.Count > 0)
        {
            var parent = nodeStack.Peek();
            node.Parent = parent;
            parent.Children.Add(node);
        }
        else
        {
            plan.RootNode = node;
        }

        nodeStack.Push(node);
        plan.AllNodes.Add(node);
    }

    return plan;
}
```

### Step 1.4: Extract #Records from Operation String

The operation string often contains `#Records=N`. Extract this:

```csharp
private static readonly Regex RecordsPattern = new Regex(
    @"#Records=([0-9,]+)", RegexOptions.Compiled);

public static long? ExtractRecords(string operation)
{
    var match = RecordsPattern.Match(operation ?? "");
    if (match.Success)
    {
        var value = match.Groups[1].Value.Replace(",", "");
        return long.Parse(value);
    }
    return null;
}
```

---

## Phase 2: Operator Dictionary & Engine Classification

**Value delivered**: Operators have meaningful names, categories, and SE/FE classification.

### Step 2.1: Create OperatorInfo Class

```csharp
public class OperatorInfo
{
    public string Category { get; set; }      // e.g., "Aggregation", "Filter", "Scan"
    public string Description { get; set; }   // Human-readable explanation
    public EngineType EngineType { get; set; } // SE, FE, or Unknown
}
```

### Step 2.2: Build Operator Dictionary

Create a static dictionary mapping operator names to metadata. Key operators to include:

**Storage Engine (SE) operators**:
| Operator | Category | Description |
|----------|----------|-------------|
| Scan_Vertipaq | Scan | Scans columns from VertiPaq storage |
| AggregationSpool | Aggregation | Buffers aggregation results |
| Sum_Vertipaq | Aggregation | SE-native SUM |
| Filter_Vertipaq | Filter | SE-native filtering |
| Cache | Cache | VertiPaq cache operation |
| DirectQueryResult | DirectQuery | Result from external query |

**Formula Engine (FE) operators**:
| Operator | Category | Description |
|----------|----------|-------------|
| Spool_Iterator | Iterator | Iterates over spooled data |
| SpoolLookup | Lookup | Looks up values in spool |
| AddColumns | Transform | Adds calculated columns |
| Filter | Filter | FE filtering operation |
| CrossApply | Join | Cross-apply join |
| Union | Set | Combines result sets |
| GreaterThan, LessThan, Equal | Comparison | Comparison operators |
| Add, Multiply, Divide | Arithmetic | Arithmetic operators |

**Implementation**:
```csharp
public static class DaxOperatorDictionary
{
    private static readonly Dictionary<string, OperatorInfo> _operators =
        new Dictionary<string, OperatorInfo>(StringComparer.OrdinalIgnoreCase)
    {
        ["Scan_Vertipaq"] = new OperatorInfo
        {
            Category = "Scan",
            Description = "Scans columns from VertiPaq storage engine",
            EngineType = EngineType.StorageEngine
        },
        ["Spool_Iterator"] = new OperatorInfo
        {
            Category = "Iterator",
            Description = "Iterates over materialized spool data",
            EngineType = EngineType.FormulaEngine
        },
        // ... add more operators
    };

    public static OperatorInfo GetOperatorInfo(string operatorName)
    {
        // Try exact match first
        if (_operators.TryGetValue(operatorName, out var info))
            return info;

        // Try prefix match for parameterized operators like AggregationSpool<Sum>
        var prefixEnd = operatorName.IndexOf('<');
        if (prefixEnd > 0)
        {
            var prefix = operatorName.Substring(0, prefixEnd);
            if (_operators.TryGetValue(prefix, out info))
                return info;
        }

        // Return unknown
        return new OperatorInfo
        {
            Category = "Unknown",
            Description = "Unknown operator",
            EngineType = EngineType.Unknown
        };
    }
}
```

### Step 2.3: Extract Operator Name from Operation String

Operation strings have various formats. Extract the operator name:

```csharp
public static string GetOperatorName(string operation)
{
    if (string.IsNullOrWhiteSpace(operation))
        return string.Empty;

    // Format 1: "'Table'[Column]: OperatorName ..."
    if (operation.StartsWith("'"))
    {
        var colonIdx = FindColonAfterColumnRef(operation);
        if (colonIdx > 0)
        {
            var afterColon = operation.Substring(colonIdx + 1).TrimStart();
            var spaceIdx = afterColon.IndexOf(' ');
            return spaceIdx > 0 ? afterColon.Substring(0, spaceIdx) : afterColon;
        }
    }

    // Format 2: "OperatorName: Details..."
    var idx = operation.IndexOf(':');
    if (idx > 0)
    {
        var firstSpace = operation.IndexOf(' ');
        if (firstSpace > 0 && firstSpace < idx)
            return operation.Substring(0, firstSpace);
        return operation.Substring(0, idx);
    }

    // Format 3: "OperatorName Details..."
    var spaceIndex = operation.IndexOf(' ');
    return spaceIndex > 0 ? operation.Substring(0, spaceIndex) : operation;
}

// Helper to find colon after column reference like 'Table'[Column]:
private static int FindColonAfterColumnRef(string op)
{
    int bracketDepth = 0;
    bool inQuote = false;

    for (int i = 0; i < op.Length; i++)
    {
        char c = op[i];
        if (c == '\'' && bracketDepth == 0)
            inQuote = !inQuote;
        else if (!inQuote)
        {
            if (c == '[') bracketDepth++;
            else if (c == ']') bracketDepth--;
            else if (c == ':' && bracketDepth == 0)
                return i;
        }
    }
    return -1;
}
```

---

## Phase 3: Basic Tree Building

**Value delivered**: Plan data is converted to a tree of ViewModels suitable for display.

### Step 3.1: Create PlanNodeViewModel

The ViewModel wraps EnrichedPlanNode and adds:
- Display properties (computed from node data)
- MVVM property change notification
- Layout properties (position, size)
- Parent/child relationships as ViewModels

```csharp
public class PlanNodeViewModel : PropertyChangedBase  // Caliburn.Micro base
{
    private readonly EnrichedPlanNode _node;
    private Point _position;
    private bool _isSelected;

    public PlanNodeViewModel(EnrichedPlanNode node)
    {
        _node = node;
        Children = new BindableCollection<PlanNodeViewModel>();
    }

    // Identity
    public int NodeId => _node.NodeId;
    public int RowNumber => _node.RowNumber;
    public int Level => _node.Level;

    // Display text
    public string OperatorName => GetOperatorName(_node.Operation);
    public string DisplayText => BuildDisplayText();
    public string FullOperation => _node.Operation;

    // Metrics
    public long? Records => _node.Records;
    public string RecordsDisplay => Records.HasValue ? $"{Records.Value:N0}" : "";

    // Engine classification
    public EngineType EngineType => _node.EngineType;

    // Layout
    public double X => _position.X;
    public double Y => _position.Y;
    public double Width { get; set; } = 200;
    public double Height { get; set; } = 70;

    public Point Position
    {
        get => _position;
        set { _position = value; NotifyOfPropertyChange(); }
    }

    // Tree structure
    public PlanNodeViewModel Parent { get; set; }
    public BindableCollection<PlanNodeViewModel> Children { get; }

    // Selection
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; NotifyOfPropertyChange(); }
    }
}
```

### Step 3.2: Implement BuildTree Method

The `BuildTree` method converts EnrichedQueryPlan to a tree of PlanNodeViewModels:

```csharp
public static PlanNodeViewModel BuildTree(EnrichedQueryPlan plan)
{
    if (plan?.RootNode == null)
        return null;

    var nodeMap = new Dictionary<int, PlanNodeViewModel>();

    // Create ViewModels for all nodes
    foreach (var node in plan.AllNodes)
    {
        nodeMap[node.NodeId] = new PlanNodeViewModel(node);
    }

    // Build parent-child relationships
    foreach (var node in plan.AllNodes)
    {
        var vm = nodeMap[node.NodeId];

        if (node.Parent != null && nodeMap.TryGetValue(node.Parent.NodeId, out var parentVm))
        {
            vm.Parent = parentVm;
            parentVm.Children.Add(vm);
        }
    }

    return nodeMap[plan.RootNode.NodeId];
}
```

---

## Phase 4: Node Folding (Visual Simplification)

**Value delivered**: Clean, readable tree without visual noise from intermediate nodes.

This is the most complex part of the implementation. Raw query plans contain many nodes that add no insight:
- Column reference nodes
- Intermediate spool types
- Wrapper operators (Proxy, Variant)
- Chained arithmetic operators

### Step 4.1: Understand the Multi-Pass Architecture

The folding algorithm uses 15 sequential passes. Each pass identifies and processes a specific pattern. **Order matters** - some passes depend on earlier passes having completed.

| Pass | Name | Pattern | Action | Example |
|------|------|---------|--------|---------|
| 1 | Column Reference | `'Table'[Column]: ScaLogOp` | Fold into parent | `'Sales'[Amount]: ScaLogOp` -> hidden |
| 2 | Filter Predicate | Filter + GreaterThan/LessThan/etc. | Build predicate, fold comparison tree | `Filter: [Amount] > 100` |
| 3 | Physical Comparison | `LogOp=GreaterThan` in operation | Extract logical comparison | Physical plan encoding |
| 4 | Unary Predicates | Filter → Not → ISBLANK | Build `NOT ISBLANK([Col])` | ISBLANK/ISERROR chains |
| 5 | Spool Children | Spool_Iterator + AggregationSpool | Fold spool, keep type info | `[Sum]`, `[GroupBy]` |
| 6 | Arithmetic Chains | Add → Add → Add | Collapse to `Add (3x)` | Long math expressions |
| 7 | SingletonTable | `SingletonTable` anywhere | Always fold | Scalar context wrapper |
| 8 | Column Info | Scan_Vertipaq, DirectQueryResult | Extract RequiredCols/DependOnCols | Column info for display |
| 9 | Identical Children | Parent.Operation == Child.Operation | Fold duplicate | Redundant wrappers |
| 10 | Cache Column | Cache without column info | Inherit from ancestor IterCols | Spool context lookup |
| 11 | Nested Spool | Spool_Iterator chains | Track row ranges, fold | `[100-1000 rows]` |
| 12 | Type Coercion | `Variant->Decimal` etc. | Fold into child | Type conversion wrappers |
| 13 | SpoolLookup+Iterator | SpoolLookup + Spool_Iterator | Fold with row range | Lookup backing spool |
| 14 | Proxy Operators | TableVarProxy, Proxy | Fold into child, extract VAR name | `(VAR: __var1)` |
| 15 | TableToScalar | TableToScalar + AggregationSpool | Fold, transfer #Records | Records to data source |

**Critical Rule**: Never fold across engine boundaries (SE ↔ FE). Check `EngineType` before every fold decision.

### Step 4.2: Implement Pass 1 - Column Reference Folding

Nodes starting with `'Table'[Column]` followed by ScaLogOp/RelLogOp are just column references:

```csharp
private static bool ShouldFoldNode(EnrichedPlanNode node)
{
    var op = node.Operation;
    if (string.IsNullOrWhiteSpace(op) || !op.StartsWith("'"))
        return false;

    // Find operator after column reference
    var colonIdx = FindColonAfterColumnRef(op);
    if (colonIdx > 0)
    {
        var afterColon = op.Substring(colonIdx + 1).TrimStart();
        var spaceIdx = afterColon.IndexOf(' ');
        var opName = spaceIdx > 0 ? afterColon.Substring(0, spaceIdx) : afterColon;

        // ScaLogOp and RelLogOp are type indicators, not real operators
        if (opName == "ScaLogOp" || opName == "RelLogOp")
            return true;
    }

    return false;
}
```

### Step 4.3: Implement Pass 2 - Filter Predicate Collection

Collect comparison operators under Filter nodes and build expression:

```csharp
private static string BuildFilterPredicateExpression(EnrichedPlanNode filterNode,
    IEnumerable<EnrichedPlanNode> allNodes)
{
    var children = allNodes.Where(n => n.Parent?.NodeId == filterNode.NodeId).ToList();

    // Find comparison child (GreaterThan, LessThan, etc.)
    var comparison = children.FirstOrDefault(c =>
        ComparisonOperators.Contains(GetOperatorName(c.Operation)));

    if (comparison == null)
        return null;

    // Get operands (children of comparison)
    var operands = allNodes.Where(n => n.Parent?.NodeId == comparison.NodeId).ToList();
    if (operands.Count != 2)
        return null;

    var left = ExtractValueDisplay(operands[0].Operation);
    var right = ExtractValueDisplay(operands[1].Operation);
    var symbol = GetComparisonSymbol(GetOperatorName(comparison.Operation));

    return $"{left} {symbol} {right}";
}

private static string GetComparisonSymbol(string opName)
{
    return opName switch
    {
        "GreaterThan" => ">",
        "LessThan" => "<",
        "GreaterOrEqualTo" => ">=",
        "LessOrEqualTo" => "<=",
        "Equal" => "=",
        "NotEqual" => "<>",
        _ => "?"
    };
}
```

### Step 4.4: Implement Pass 5 - Spool Child Folding

Fold AggregationSpool, ProjectionSpool into parent Spool_Iterator:

```csharp
private static bool IsFoldableSpoolChild(string opName)
{
    if (string.IsNullOrEmpty(opName)) return false;

    var isSpool = (opName.Contains("Spool<") ||
                   opName.EndsWith("Spool", StringComparison.OrdinalIgnoreCase)) &&
                  !opName.StartsWith("Spool_Iterator") &&
                  opName != "SpoolLookup";

    return isSpool || opName == "Extend_Lookup";
}

// Recursively fold spool chain
private void FoldSpoolChainRecursively(EnrichedPlanNode parent,
    EnrichedPlanNode nodeToFold, HashSet<int> foldedNodeIds)
{
    if (foldedNodeIds.Contains(nodeToFold.NodeId))
        return;

    // Never fold across engine boundaries
    if (nodeToFold.EngineType != EngineType.Unknown &&
        parent.EngineType != EngineType.Unknown &&
        nodeToFold.EngineType != parent.EngineType)
        return;

    var childOpName = GetOperatorName(nodeToFold.Operation);
    if (!IsFoldableSpoolChild(childOpName))
        return;

    // Fold this node
    foldedNodeIds.Add(nodeToFold.NodeId);

    // Store info for display (e.g., "AggregationSpool<Sum>")
    spoolTypeInfos[parent.NodeId] = GetSimplifiedSpoolType(childOpName);

    // Recursively fold children
    foreach (var grandchild in nodeToFold.Children)
    {
        FoldSpoolChainRecursively(parent, grandchild, foldedNodeIds);
    }
}
```

### Step 4.5: Implement Pass 6 - Arithmetic Chain Folding

Collapse chains like Add→Add→Add into "Add (3x)":

```csharp
var arithmeticOperators = new HashSet<string>
{
    "Add", "Subtract", "Multiply", "Divide", "Min", "Max", "Coalesce"
};

bool progress;
do
{
    progress = false;
    foreach (var node in plan.AllNodes)
    {
        if (foldedNodeIds.Contains(node.NodeId))
            continue;

        var opName = GetOperatorName(node.Operation);
        if (!arithmeticOperators.Contains(opName))
            continue;

        // Find children with same operator
        var children = plan.AllNodes
            .Where(n => n.Parent?.NodeId == node.NodeId && !foldedNodeIds.Contains(n.NodeId))
            .ToList();

        var sameOpChildren = children.Where(c => GetOperatorName(c.Operation) == opName).ToList();

        // If exactly one child has same operator, fold it
        if (sameOpChildren.Count == 1)
        {
            var child = sameOpChildren[0];

            // Update chain count
            var childCount = chainedCounts.GetValueOrDefault(child.NodeId, 1);
            var parentCount = chainedCounts.GetValueOrDefault(node.NodeId, 1);
            chainedCounts[node.NodeId] = parentCount + childCount;

            // Fold child
            foldedNodeIds.Add(child.NodeId);

            // Re-parent grandchildren
            foreach (var grandchild in child.Children)
            {
                grandchild.Parent = node;
            }

            progress = true;
        }
    }
} while (progress);  // Iterate until no more folding possible
```

### Step 4.6: Build Final ViewModel Tree with Folding

After all passes, create ViewModels only for non-folded nodes:

```csharp
// Create ViewModels for non-folded nodes only
foreach (var node in plan.AllNodes)
{
    if (!foldedNodeIds.Contains(node.NodeId))
    {
        var vm = new PlanNodeViewModel(node);

        // Apply collected info
        if (filterPredicates.TryGetValue(node.NodeId, out var predicate))
            vm.FilterPredicateExpression = predicate;

        if (spoolTypeInfos.TryGetValue(node.NodeId, out var spoolType))
            vm.SpoolTypeInfo = spoolType;

        if (chainedCounts.TryGetValue(node.NodeId, out var count))
            vm.ChainedOperatorCount = count;

        nodeMap[node.NodeId] = vm;
    }
}

// Build relationships, skipping folded nodes
foreach (var node in plan.AllNodes)
{
    if (foldedNodeIds.Contains(node.NodeId))
        continue;

    var vm = nodeMap[node.NodeId];

    // Find nearest non-folded ancestor
    var parent = node.Parent;
    while (parent != null && foldedNodeIds.Contains(parent.NodeId))
    {
        parent = parent.Parent;
    }

    if (parent != null && nodeMap.TryGetValue(parent.NodeId, out var parentVm))
    {
        vm.Parent = parentVm;
        parentVm.Children.Add(vm);
    }
}
```

---

## Phase 5: Tree Layout Algorithm

**Value delivered**: Nodes are positioned in a readable tree arrangement.

### Step 5.1: Calculate Subtree Widths

Each node needs to know how "wide" its subtree is for positioning:

```csharp
public int SubtreeWidth
{
    get
    {
        if (_cachedSubtreeWidth.HasValue)
            return _cachedSubtreeWidth.Value;

        if (Children.Count == 0)
        {
            _cachedSubtreeWidth = 1;  // Leaf counts as 1
        }
        else
        {
            int width = 0;
            foreach (var child in Children)
                width += child.SubtreeWidth;
            _cachedSubtreeWidth = width;
        }
        return _cachedSubtreeWidth.Value;
    }
}

public void InvalidateSubtreeWidth()
{
    _cachedSubtreeWidth = null;
    Parent?.InvalidateSubtreeWidth();
}
```

### Step 5.2: Implement Layout Algorithm

Use a top-down recursive layout:

```csharp
public void ApplyLayout(PlanNodeViewModel root)
{
    const double NodeWidth = 200;
    const double NodeHeight = 70;
    const double HorizontalSpacing = 20;
    const double VerticalSpacing = 50;

    LayoutNode(root, 0, 0, NodeWidth, NodeHeight, HorizontalSpacing, VerticalSpacing);

    // Calculate content bounds
    var allNodes = GetAllNodes(root);
    ContentWidth = allNodes.Max(n => n.X + n.Width) + HorizontalSpacing;
    ContentHeight = allNodes.Max(n => n.Y + n.Height) + VerticalSpacing;
}

private double LayoutNode(PlanNodeViewModel node, double x, double y,
    double nodeWidth, double nodeHeight, double hSpacing, double vSpacing)
{
    // Calculate total width needed for children
    var visibleChildren = node.VisibleChildrenForLayout.ToList();

    if (visibleChildren.Count == 0)
    {
        // Leaf node - position directly
        node.Position = new Point(x, y);
        return nodeWidth;
    }

    // Layout children first
    double childX = x;
    double childY = y + nodeHeight + vSpacing;
    double totalChildWidth = 0;

    foreach (var child in visibleChildren)
    {
        double childWidth = LayoutNode(child, childX, childY,
            nodeWidth, nodeHeight, hSpacing, vSpacing);
        childX += childWidth + hSpacing;
        totalChildWidth += childWidth;
    }
    totalChildWidth += (visibleChildren.Count - 1) * hSpacing;

    // Center this node above its children
    double nodeX = x + (totalChildWidth - nodeWidth) / 2;
    node.Position = new Point(nodeX, y);

    return totalChildWidth;
}
```

### Step 5.3: Handle Collapsed Subtrees

When a subtree is collapsed, treat it as a leaf:

```csharp
public IEnumerable<PlanNodeViewModel> VisibleChildrenForLayout =>
    (IsCollapsed || IsSubtreeCollapsed)
        ? Enumerable.Empty<PlanNodeViewModel>()
        : Children;

public int VisibleSubtreeWidth
{
    get
    {
        if (IsSubtreeCollapsed)
            return 1;  // Collapsed subtree counts as 1

        var visible = VisibleChildrenForLayout.ToList();
        if (visible.Count == 0)
            return 1;

        return visible.Sum(c => c.VisibleSubtreeWidth);
    }
}
```

---

## Phase 6: WPF Visualization

**Value delivered**: Visual tree display with nodes and connecting edges.

### Step 6.1: Canvas-Based Layout

Use a Canvas inside a ScrollViewer with scale transform for zoom:

```xml
<ScrollViewer HorizontalScrollBarVisibility="Auto" VerticalScrollBarVisibility="Auto">
    <Canvas Width="{Binding ActualContentWidth}"
            Height="{Binding ActualContentHeight}">
        <Canvas.LayoutTransform>
            <ScaleTransform ScaleX="{Binding ZoomLevel}" ScaleY="{Binding ZoomLevel}"/>
        </Canvas.LayoutTransform>

        <!-- Edges first (behind nodes) -->
        <ItemsControl ItemsSource="{Binding AllNodes}">
            <!-- Edge template -->
        </ItemsControl>

        <!-- Nodes second (in front) -->
        <ItemsControl ItemsSource="{Binding AllNodes}">
            <!-- Node template -->
        </ItemsControl>
    </Canvas>
</ScrollViewer>
```

### Step 6.2: Node Template

Each node is a styled Border with text:

```xml
<DataTemplate>
    <Border Background="{Binding BackgroundBrush}"
            BorderBrush="{Binding BorderBrush}"
            BorderThickness="2"
            CornerRadius="4"
            Width="{Binding Width}"
            Height="{Binding Height}">
        <Grid Margin="6,4">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>  <!-- Operator name -->
                <RowDefinition Height="Auto"/>  <!-- Detail -->
                <RowDefinition Height="Auto"/>  <!-- Records -->
            </Grid.RowDefinitions>

            <!-- Engine badge + Operator name -->
            <StackPanel Grid.Row="0" Orientation="Horizontal">
                <Border Background="{Binding EngineBadgeBackground}"
                        CornerRadius="2" Padding="3,1">
                    <TextBlock Text="{Binding EngineLabel}"
                               Foreground="{Binding EngineBadgeForeground}"
                               FontSize="9"/>
                </Border>
                <TextBlock Text="{Binding OperatorDisplayName}"
                           FontWeight="SemiBold"
                           Margin="4,0,0,0"/>
            </StackPanel>

            <!-- Detail line (filter predicate, column, etc.) -->
            <TextBlock Grid.Row="1"
                       Text="{Binding DetailText}"
                       FontSize="10"
                       Foreground="Gray"
                       TextTrimming="CharacterEllipsis"/>

            <!-- Records count -->
            <TextBlock Grid.Row="2"
                       Text="{Binding RecordsDisplay}"
                       FontSize="10"
                       Foreground="{Binding RowCountColor}"/>
        </Grid>
    </Border>
</DataTemplate>
```

### Step 6.3: Edge Template with Bezier Curves

Connect parent bottom to child top with curved lines:

```xml
<DataTemplate>
    <ItemsControl ItemsSource="{Binding VisibleChildrenForLayout}">
        <ItemsControl.ItemTemplate>
            <DataTemplate>
                <Path Stroke="{Binding EdgeColor}"
                      StrokeThickness="{Binding EdgeThickness}">
                    <Path.Data>
                        <PathGeometry>
                            <PathFigure StartPoint="{Binding Parent.EdgeBottom}">
                                <BezierSegment
                                    Point1="{Binding Parent.EdgeBottomControl}"
                                    Point2="{Binding EdgeTopControl}"
                                    Point3="{Binding EdgeTop}"/>
                            </PathFigure>
                        </PathGeometry>
                    </Path.Data>
                </Path>
            </DataTemplate>
        </ItemsControl.ItemTemplate>
    </ItemsControl>
</DataTemplate>
```

### Step 6.4: Edge Geometry Properties

Add properties to PlanNodeViewModel for bezier control points:

```csharp
public Point EdgeTop => new Point(CenterX, Y);
public Point EdgeBottom => new Point(CenterX, Y + Height);
public Point EdgeTopControl => new Point(CenterX, Y - 30);
public Point EdgeBottomControl => new Point(CenterX, Y + Height + 30);

public double CenterX => Position.X + Width / 2;
public double CenterY => Position.Y + Height / 2;
```

### Step 6.5: Logarithmic Edge Thickness

Vary edge thickness based on row count (like SSMS):

```csharp
public double EdgeThickness
{
    get
    {
        if (!Records.HasValue || Records.Value <= 0)
            return 1.0;

        // Log scale: 1px at 0 rows, ~10px at 100K+ rows
        var rows = Records.Value;
        return Math.Min(10, Math.Max(1, Math.Log10(rows + 1) * 2));
    }
}
```

---

## Phase 7: Server Timing Correlation

**Value delivered**: Nodes show actual execution time and row counts from traces.

### Step 7.1: Create PlanEnrichmentService Interface

```csharp
public interface IPlanEnrichmentService
{
    EnrichedQueryPlan Enrich(
        EnrichedQueryPlan plan,
        IEnumerable<ServerTimingEvent> timings);
}

public class ServerTimingEvent
{
    public string EventClass { get; set; }
    public string ObjectName { get; set; }  // Often matches operator context
    public long Duration { get; set; }
    public long CpuTime { get; set; }
    public long? RowCount { get; set; }
    public long? EstimatedKBytes { get; set; }
    public bool CacheHit { get; set; }
    public string Query { get; set; }  // xmSQL for SE queries
}
```

### Step 7.2: Implement Correlation Logic

Match timing events to plan nodes by analyzing operation strings and xmSQL:

```csharp
public EnrichedQueryPlan Enrich(EnrichedQueryPlan plan,
    IEnumerable<ServerTimingEvent> timings)
{
    var timingList = timings.ToList();

    // Build lookup of SE queries by xmSQL hash
    var seQueryLookup = timingList
        .Where(t => t.EventClass == "ExecutionMetrics" && !string.IsNullOrEmpty(t.Query))
        .ToDictionary(t => NormalizeXmSql(t.Query), t => t);

    foreach (var node in plan.AllNodes)
    {
        // Try to match by xmSQL if node has it
        if (!string.IsNullOrEmpty(node.XmSql))
        {
            var normalizedSql = NormalizeXmSql(node.XmSql);
            if (seQueryLookup.TryGetValue(normalizedSql, out var timing))
            {
                node.DurationMs = timing.Duration;
                node.CpuTimeMs = timing.CpuTime;
                node.EstimatedKBytes = timing.EstimatedKBytes;
                node.IsCacheHit = timing.CacheHit;

                if (timing.RowCount.HasValue)
                {
                    node.Records = timing.RowCount;
                    node.RecordsSource = "ServerTiming";
                }
            }
        }
    }

    // Calculate parallelism from duration vs CPU time
    foreach (var node in plan.AllNodes.Where(n => n.DurationMs > 0 && n.CpuTimeMs > 0))
    {
        node.NetParallelDurationMs = node.DurationMs;
        if (node.CpuTimeMs > node.DurationMs)
        {
            node.Parallelism = (int)(node.CpuTimeMs / node.DurationMs);
        }
    }

    // Aggregate totals
    plan.StorageEngineDurationMs = timingList
        .Where(t => t.EventClass == "ExecutionMetrics")
        .Sum(t => t.Duration);

    plan.CacheHits = timingList.Count(t => t.CacheHit);
    plan.StorageEngineQueryCount = timingList.Count(t => t.EventClass == "ExecutionMetrics");

    return plan;
}
```

### Step 7.3: Infer Records from Physical Plan

When logical plan nodes lack #Records, try to infer from physical plan:

```csharp
public void InferRecordsFromPhysicalPlan(EnrichedQueryPlan logicalPlan,
    EnrichedQueryPlan physicalPlan)
{
    foreach (var logicalNode in logicalPlan.AllNodes.Where(n => !n.Records.HasValue))
    {
        // Find matching physical node by operation pattern
        var opName = GetOperatorName(logicalNode.Operation);
        var matchingPhysical = physicalPlan.AllNodes
            .FirstOrDefault(p => GetOperatorName(p.Operation) == opName && p.Records.HasValue);

        if (matchingPhysical != null)
        {
            logicalNode.Records = matchingPhysical.Records;
            logicalNode.RecordsSource = "Physical";  // Mark as approximate
        }
    }
}
```

---

## Phase 8: Performance Issue Detection

**Value delivered**: Automatic identification of performance problems.

### Step 8.1: Define Issue Types

```csharp
public class PerformanceIssue
{
    public string Title { get; set; }
    public string Description { get; set; }
    public IssueSeverity Severity { get; set; }
    public string Recommendation { get; set; }
}

public enum IssueSeverity { Info, Warning, Error }
```

### Step 8.2: Create PerformanceIssueDetector

```csharp
public class PerformanceIssueDetector
{
    public void DetectIssues(EnrichedQueryPlan plan)
    {
        foreach (var node in plan.AllNodes)
        {
            DetectCallbackDataId(node);
            DetectExcessiveMaterialization(node);
        }

        DetectHighFormulaEngineRatio(plan);
    }

    private void DetectCallbackDataId(EnrichedPlanNode node)
    {
        if (node.Operation?.Contains("CallbackDataID") == true)
        {
            node.Issues.Add(new PerformanceIssue
            {
                Title = "CallbackDataID Detected",
                Description = "Storage Engine is calling back to Formula Engine during scan. " +
                              "This runs in parallel but results are NOT cached.",
                Severity = IssueSeverity.Warning,
                Recommendation = "Consider restructuring the measure to avoid SE→FE callbacks."
            });
        }
    }

    private void DetectExcessiveMaterialization(EnrichedPlanNode node)
    {
        // Only flag Spool operations (FE materialization)
        var opName = GetOperatorName(node.Operation);
        var isSpool = opName.Contains("Spool") || opName == "Cache";

        if (isSpool && node.Records > 1_000_000)
        {
            node.Issues.Add(new PerformanceIssue
            {
                Title = "Excessive Materialization",
                Description = $"Node materializes {node.Records:N0} rows in Formula Engine. " +
                              "Consider reducing data volume before materialization.",
                Severity = IssueSeverity.Warning,
                Recommendation = "Apply filters earlier, or use SUMMARIZE/GROUPBY to reduce rows."
            });
        }
    }

    private void DetectHighFormulaEngineRatio(EnrichedQueryPlan plan)
    {
        if (plan.TotalDurationMs == 0) return;

        var feRatio = (double)plan.FormulaEngineDurationMs / plan.TotalDurationMs;
        if (feRatio > 0.5 && plan.FormulaEngineDurationMs > 500)
        {
            plan.RootNode.Issues.Add(new PerformanceIssue
            {
                Title = "High Formula Engine Ratio",
                Description = $"FE accounts for {feRatio:P0} of query time ({plan.FormulaEngineDurationMs}ms). " +
                              "FE is single-threaded and may be a bottleneck.",
                Severity = IssueSeverity.Info,
                Recommendation = "Look for complex DAX calculations that could be simplified."
            });
        }
    }
}
```

### Step 8.3: Row Count Severity Colors

Apply color coding based on materialization thresholds:

```csharp
public string RowCountSeverity
{
    get
    {
        if (!Records.HasValue) return "None";

        // Only flag Spool operations (FE materialization)
        var isSpool = OperatorName.Contains("Spool") || OperatorName == "Cache";
        if (!isSpool) return "Fine";  // SE scans are optimized

        var records = Records.Value;
        if (records < 100_000) return "Fine";       // Green
        if (records < 1_000_000) return "Warning";  // Yellow
        return "Critical";                           // Red
    }
}

public Brush RowCountColor => RowCountSeverity switch
{
    "Warning" => new SolidColorBrush(Color.FromRgb(200, 120, 0)),   // Orange
    "Critical" => new SolidColorBrush(Color.FromRgb(180, 40, 40)),  // Red
    _ => new SolidColorBrush(Color.FromRgb(80, 80, 80))             // Gray
};
```

---

## Phase 9: Interactive Features

**Value delivered**: Users can navigate and explore the plan.

### Step 9.1: Node Selection

```csharp
private PlanNodeViewModel _selectedNode;

public void SelectNode(PlanNodeViewModel node)
{
    if (_selectedNode != null)
        _selectedNode.IsSelected = false;

    _selectedNode = node;
    node.IsSelected = true;

    NotifyOfPropertyChange(nameof(SelectedNode));
}
```

### Step 9.2: Expand/Collapse Subtrees

```csharp
// In PlanNodeViewModel
public bool IsSubtreeCollapsed
{
    get => _isSubtreeCollapsed;
    set
    {
        if (_isSubtreeCollapsed != value)
        {
            _isSubtreeCollapsed = value;
            NotifyOfPropertyChange();
            InvalidateSubtreeWidth();
            OnSubtreeToggled?.Invoke();  // Trigger re-layout
        }
    }
}

public bool CanToggleSubtree =>
    Children.Count > 1 && SubtreeWidth >= 6;

public void ToggleSubtree() => IsSubtreeCollapsed = !IsSubtreeCollapsed;

// In VisualQueryPlanViewModel
public void ExpandAll()
{
    foreach (var node in GetAllNodes())
        node.IsSubtreeCollapsed = false;
    ApplyLayout();
}

public void CollapseAll()
{
    foreach (var node in GetAllNodes().Where(n => n.CanToggleSubtree))
        node.IsSubtreeCollapsed = true;
    ApplyLayout();
}

public void ExpandIssueNodes()
{
    foreach (var node in GetAllNodes().Where(n => n.HasIssues))
        node.ExpandPathToRoot();
    ApplyLayout();
}
```

### Step 9.3: Zoom Controls

```csharp
private double _zoomLevel = 1.0;

public double ZoomLevel
{
    get => _zoomLevel;
    set
    {
        _zoomLevel = Math.Max(0.25, Math.Min(2.0, value));
        NotifyOfPropertyChange();
        NotifyOfPropertyChange(nameof(ZoomPercentage));
    }
}

public string ZoomPercentage => $"{ZoomLevel * 100:F0}%";

public void ZoomIn() => ZoomLevel += 0.1;
public void ZoomOut() => ZoomLevel -= 0.1;
public void ZoomToFit() => ZoomLevel = 1.0;
```

---

## Phase 10: Polish & Animation

**Value delivered**: Smooth, professional user experience.

### Step 10.1: Create CanvasPositionAnimation Attached Property

Animate node positions when layout changes:

```csharp
public static class CanvasPositionAnimation
{
    public static bool SuspendAnimations { get; set; } = false;

    public static readonly DependencyProperty AnimatedLeftProperty =
        DependencyProperty.RegisterAttached(
            "AnimatedLeft", typeof(double), typeof(CanvasPositionAnimation),
            new PropertyMetadata(0.0, OnAnimatedLeftChanged));

    private static void OnAnimatedLeftChanged(DependencyObject d,
        DependencyPropertyChangedEventArgs e)
    {
        if (d is FrameworkElement element)
        {
            double newValue = (double)e.NewValue;
            double oldValue = Canvas.GetLeft(element);

            // Skip animation if suspended or first set
            if (SuspendAnimations || double.IsNaN(oldValue) ||
                Math.Abs(oldValue - newValue) < 0.5)
            {
                Canvas.SetLeft(element, newValue);
                return;
            }

            var animation = new DoubleAnimation
            {
                From = oldValue,
                To = newValue,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(Canvas.LeftProperty, animation);
        }
    }

    // Similar for AnimatedTop...
}
```

### Step 10.2: Suspend Animations During Bulk Operations

```csharp
public void ExpandAll()
{
    try
    {
        CanvasPositionAnimation.SuspendAnimations = true;

        foreach (var node in GetAllNodes())
            node.IsSubtreeCollapsed = false;

        ApplyLayout();
    }
    finally
    {
        CanvasPositionAnimation.SuspendAnimations = false;
    }
}
```

### Step 10.3: Color Scheme

Use consistent colors matching Server Timings:

```csharp
// Engine badges
public Brush EngineBadgeBackground => EngineType switch
{
    EngineType.StorageEngine => new SolidColorBrush(Color.FromRgb(95, 142, 214)),   // Blue
    EngineType.FormulaEngine => new SolidColorBrush(Color.FromRgb(254, 187, 76)),   // Orange
    EngineType.DirectQuery => new SolidColorBrush(Color.FromRgb(100, 100, 100)),    // Gray
    _ => new SolidColorBrush(Color.FromRgb(158, 158, 158))
};

// Node backgrounds (subtle tints)
public Brush BackgroundBrush => EngineType switch
{
    EngineType.StorageEngine => new SolidColorBrush(Color.FromRgb(240, 247, 255)),  // Light blue
    EngineType.FormulaEngine => new SolidColorBrush(Color.FromRgb(255, 250, 240)),  // Light orange
    _ => new SolidColorBrush(Color.FromRgb(248, 248, 248))
};
```

---

## Appendix: Performance Considerations

### Regex Compilation

**Always use `RegexOptions.Compiled`** for patterns used during tree building:

```csharp
// GOOD - compiled once, fast on every call
private static readonly Regex RecordsPattern = new Regex(
    @"#Records=([0-9,]+)", RegexOptions.Compiled);

// BAD - recompiles every call, ~10x slower
var match = Regex.Match(operation, @"#Records=([0-9,]+)");
```

**Why**: The folding algorithm processes every node multiple times across 15 passes. Uncompiled regex adds ~100ms+ overhead on large plans (200+ nodes).

### Avoid O(n²) Patterns

**Problem**: Scanning the node list repeatedly to find relationships creates O(n²) performance.

```csharp
// BAD - O(n²): Scans all nodes for every node
foreach (var node in plan.AllNodes)
{
    var children = plan.AllNodes.Where(n => n.Parent?.NodeId == node.NodeId).ToList();
}

// GOOD - O(n): Build lookup once, use many times
var childrenByParent = plan.AllNodes
    .Where(n => n.Parent != null)
    .GroupBy(n => n.Parent.NodeId)
    .ToDictionary(g => g.Key, g => g.ToList());

foreach (var node in plan.AllNodes)
{
    var children = childrenByParent.GetValueOrDefault(node.NodeId, new List<EnrichedPlanNode>());
}
```

**Apply this pattern to**:
- `foldedNodeIds` - use `HashSet<int>` not `List<int>.Contains()`
- Parent/child lookups - use `Dictionary<int, List<Node>>`
- Spool type info, filter predicates, chain counts - all use `Dictionary<int, T>`

### Cache Computed Values

**SubtreeWidth** is called repeatedly during layout. Cache it:

```csharp
private int? _cachedSubtreeWidth;

public int SubtreeWidth
{
    get
    {
        if (!_cachedSubtreeWidth.HasValue)
            _cachedSubtreeWidth = CalculateSubtreeWidth();
        return _cachedSubtreeWidth.Value;
    }
}

public void InvalidateSubtreeWidth()
{
    _cachedSubtreeWidth = null;
    Parent?.InvalidateSubtreeWidth();  // Propagate up
}
```

### Animation Suspension

During bulk operations (ExpandAll, CollapseAll), suspend animations:

```csharp
try
{
    CanvasPositionAnimation.SuspendAnimations = true;
    foreach (var node in GetAllNodes())
        node.IsSubtreeCollapsed = false;
    ApplyLayout();
}
finally
{
    CanvasPositionAnimation.SuspendAnimations = false;
}
```

**Why**: Animating 200+ nodes simultaneously causes UI jank. Suspend during bulk changes, then do a single layout.

---

## Appendix: Key Algorithms

### A.1: Display Text Generation

Priority-based detail selection:

```csharp
public string DisplayText
{
    get
    {
        var name = OperatorDisplayName;

        // Priority 1: Filter predicate
        if (HasFilterPredicate)
            return $"{name}: {FilterPredicateExpression}";

        // Priority 2: Spool type info
        if (HasSpoolTypeInfo)
            return $"{name}: {SpoolTypeInfo}";

        // Priority 3: Scan column
        if (HasScanColumnInfo)
            return $"{name}: {ScanColumnInfo}";

        // Priority 4: Chain count
        if (ChainedOperatorCount > 1)
            return $"{name} ({ChainedOperatorCount}x)";

        return name;
    }
}
```

### A.2: Normalize Operator for Grouping

Handle parameterized operators:

```csharp
public static string NormalizeOperatorForGrouping(string opName)
{
    if (string.IsNullOrEmpty(opName)) return opName;

    // Remove generic parameters: Spool_Iterator<SpoolIterator> -> Spool_Iterator
    var genericIdx = opName.IndexOf('<');
    if (genericIdx > 0)
        return opName.Substring(0, genericIdx);

    return opName;
}
```

---

## Appendix: Regex Patterns

Compile these patterns at class level for performance:

```csharp
// Records extraction
private static readonly Regex RecordsPattern = new Regex(
    @"#Records=([0-9,]+)", RegexOptions.Compiled);

// Column lists
private static readonly Regex RequiredColsPattern = new Regex(
    @"RequiredCols\(([^)]*)\)\(([^)]*)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

private static readonly Regex IterColsPattern = new Regex(
    @"IterCols\(([^)]*)\)\(([^)]*)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

private static readonly Regex DependOnColsPattern = new Regex(
    @"DependOnCols\(([^)]*)\)\(([^)]*)\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

// BlankRow indicator
private static readonly Regex BlankRowPattern = new Regex(
    @"([+-])BlankRow", RegexOptions.Compiled);

// Table ID
private static readonly Regex TableIdPattern = new Regex(
    @"Table=(\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

// Callback detection
private static readonly Regex CallbackDataIdPattern = new Regex(
    @"CallbackDataID=(\d+)", RegexOptions.Compiled);

// Column reference: 'TableName'[ColumnName]
private static readonly Regex ColumnRefPattern = new Regex(
    @"'([^']+)'\[([^\]]+)\]", RegexOptions.Compiled);
```

---

## Appendix: Color Schemes

### WCAG AAA Compliant Text Colors

For text on gray background (#F3F2F1):

| Engine | Text Color | Hex | Contrast Ratio |
|--------|------------|-----|----------------|
| Storage Engine | Dark blue | #1E3A8A | 7:1+ |
| Formula Engine | Dark orange | #9A3412 | 7:1+ |
| DirectQuery | Dark gray | #373737 | 7:1+ |

### Row Count Severity Colors

Only apply severity coloring to **Spool operations** (Formula Engine materialization). Storage Engine scans are optimized and don't need warnings.

| Severity | Row Count | Color | Hex | Apply To |
|----------|-----------|-------|-----|----------|
| Fine | < 100K | Dark gray | #505050 | Spools only |
| Warning | 100K - 1M | Muted orange | #C87800 | Spools only |
| Critical | > 1M | Muted red | #B42828 | Spools only |

```csharp
public string RowCountSeverity
{
    get
    {
        if (!Records.HasValue) return "None";

        // Only flag Spool operations (FE materialization)
        var isSpool = OperatorName.Contains("Spool") || OperatorName == "Cache";
        if (!isSpool) return "Fine";  // SE scans are optimized

        var records = Records.Value;
        if (records < 100_000) return "Fine";
        if (records < 1_000_000) return "Warning";
        return "Critical";
    }
}
```

### Data Size Severity Colors

| Severity | Size | Color | Hex |
|----------|------|-------|-----|
| Fine | < 100MB | Dark gray | #505050 |
| Warning | 100MB - 1GB | Muted orange | #C87800 |
| Critical | > 1GB | Muted red | #B42828 |

### Records Source Tracking

Track where `#Records` value came from for transparency in the UI:

| Source | Meaning | Display Style |
|--------|---------|---------------|
| `Plan` | Extracted from plan text `#Records=N` | Normal |
| `ServerTiming` | From Server Timing trace events | Normal |
| `Physical` | Inferred from physical plan (approximate) | Italic, prefix with "~" |
| `Inherited` | Inherited from parent/child node | Italic, prefix with "~" |

```csharp
public string RecordsSource { get; set; } = "Plan";

public string RecordsDisplay =>
    Records.HasValue
        ? (RecordsSource is "Physical" or "Inherited" ? "~" : "") + $"{Records.Value:N0}"
        : "";

public FontStyle RecordsFontStyle =>
    RecordsSource is "Physical" or "Inherited"
        ? FontStyles.Italic
        : FontStyles.Normal;
```

---

## Summary

This guide covers all major aspects of the Visual Query Plan implementation:

1. **Phases 1-2**: Core data structures and operator knowledge base
2. **Phases 3-4**: Tree building with visual simplification (15-pass folding)
3. **Phases 5-6**: Layout and WPF rendering
4. **Phases 7-8**: Server timing enrichment and issue detection
5. **Phases 9-10**: Interactivity and polish

Each phase delivers value independently. The most complex part is Phase 4 (node folding), which significantly improves readability but can be implemented incrementally.

Key architectural principles:
- **Separation of concerns**: Data model vs ViewModel vs View
- **Immutable passes**: Each folding pass modifies only `foldedNodeIds` set
- **MVVM pattern**: All UI binding through ViewModels
- **Performance**: Compiled regex, cached subtree widths

---

*Document created: 2025-12-27*
*Last updated: 2025-12-28*
*Based on DaxStudio Visual Query Plan implementation (branch: 001-visual-query-plan)*
