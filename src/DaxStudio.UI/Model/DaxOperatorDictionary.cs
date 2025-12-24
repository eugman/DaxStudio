using System.Collections.Generic;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Information about a DAX query plan operator.
    /// </summary>
    public class DaxOperatorInfo
    {
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Category { get; set; }
        public EngineType Engine { get; set; }
    }

    /// <summary>
    /// Provides human-readable names and explanations for DAX query plan operators.
    /// Based on SQLBI DAX Query Plans documentation.
    /// </summary>
    public static class DaxOperatorDictionary
    {
        private static readonly Dictionary<string, DaxOperatorInfo> _operators = new Dictionary<string, DaxOperatorInfo>
        {
            // Iteration Operators (Physical Plan)
            ["AddColumns"] = new DaxOperatorInfo
            {
                DisplayName = "Add Columns",
                Description = "Adds calculated columns to each row of an input table. Iterates over rows and evaluates expressions for each.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["SingletonTable"] = new DaxOperatorInfo
            {
                DisplayName = "Singleton Table",
                Description = "Creates a single-row table, typically used as the starting point for scalar calculations.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["CrossApply"] = new DaxOperatorInfo
            {
                DisplayName = "Cross Apply",
                Description = "Performs a cross join between two tables, evaluating the right side for each row of the left side. Can be expensive with large tables.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["Filter"] = new DaxOperatorInfo
            {
                DisplayName = "Filter",
                Description = "Filters rows from an input table based on a condition. Only rows satisfying the predicate are passed through.",
                Category = "Iterator",
                Engine = EngineType.FormulaEngine
            },
            ["Cache"] = new DaxOperatorInfo
            {
                DisplayName = "Cache",
                Description = "Caches intermediate results for reuse. Helps avoid redundant calculations.",
                Category = "Cache",
                Engine = EngineType.FormulaEngine
            },

            // Spool Operators
            ["Spool_Iterator"] = new DaxOperatorInfo
            {
                DisplayName = "Spool Iterator",
                Description = "Iterates over cached/spooled data. Reads from a previously computed result set stored in memory.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["SpoolLookup"] = new DaxOperatorInfo
            {
                DisplayName = "Spool Lookup",
                Description = "Looks up values in a spooled (cached) result set using key columns. Efficient for repeated lookups.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["ProjectionSpool"] = new DaxOperatorInfo
            {
                DisplayName = "Projection Spool",
                Description = "Stores projected columns from a table scan for later use. Reduces redundant data retrieval.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },
            ["AggregationSpool"] = new DaxOperatorInfo
            {
                DisplayName = "Aggregation Spool",
                Description = "Caches aggregated results for reuse. Stores pre-computed aggregations to avoid recalculation.",
                Category = "Spool",
                Engine = EngineType.FormulaEngine
            },

            // Storage Engine Operators
            ["Scan_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Scan",
                Description = "Scans data from the in-memory VertiPaq storage engine. This is a Storage Engine operation - fast columnar scan.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["Sum_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Sum",
                Description = "Performs SUM aggregation directly in the VertiPaq storage engine. Highly optimized for columnar data.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["Count_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Count",
                Description = "Performs COUNT aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["Min_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Min",
                Description = "Performs MIN aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["Max_Vertipaq"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Max",
                Description = "Performs MAX aggregation directly in the VertiPaq storage engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },
            ["VertipaqResult"] = new DaxOperatorInfo
            {
                DisplayName = "VertiPaq Result",
                Description = "Returns results from a VertiPaq storage engine query. Data is passed back to the Formula Engine.",
                Category = "Storage Engine",
                Engine = EngineType.StorageEngine
            },

            // Logical Operators
            ["Calculate"] = new DaxOperatorInfo
            {
                DisplayName = "Calculate",
                Description = "Evaluates an expression in a modified filter context. Core DAX operation for context transition.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine
            },
            ["SumX"] = new DaxOperatorInfo
            {
                DisplayName = "Sum X (Iterator)",
                Description = "Iterates over a table and sums the result of an expression evaluated for each row. Row-by-row aggregation.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine
            },
            ["CountRows"] = new DaxOperatorInfo
            {
                DisplayName = "Count Rows",
                Description = "Counts the number of rows in a table.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine
            },
            ["DependOnCols"] = new DaxOperatorInfo
            {
                DisplayName = "Column Dependency",
                Description = "Indicates which columns the operation depends on. Used for query optimization.",
                Category = "Metadata",
                Engine = EngineType.Unknown
            },
            ["RequiredCols"] = new DaxOperatorInfo
            {
                DisplayName = "Required Columns",
                Description = "Specifies columns required by the operation. Used for query optimization and data retrieval.",
                Category = "Metadata",
                Engine = EngineType.Unknown
            },

            // Time Intelligence
            ["PreviousQuarter"] = new DaxOperatorInfo
            {
                DisplayName = "Previous Quarter",
                Description = "Time intelligence function that shifts the filter context to the previous quarter.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine
            },
            ["PreviousMonth"] = new DaxOperatorInfo
            {
                DisplayName = "Previous Month",
                Description = "Time intelligence function that shifts the filter context to the previous month.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine
            },
            ["PreviousYear"] = new DaxOperatorInfo
            {
                DisplayName = "Previous Year",
                Description = "Time intelligence function that shifts the filter context to the previous year.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine
            },
            ["SamePeriodLastYear"] = new DaxOperatorInfo
            {
                DisplayName = "Same Period Last Year",
                Description = "Time intelligence function that returns the same period in the previous year.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine
            },
            ["DatesBetween"] = new DaxOperatorInfo
            {
                DisplayName = "Dates Between",
                Description = "Returns a table of dates between two specified dates.",
                Category = "Time Intelligence",
                Engine = EngineType.FormulaEngine
            },

            // Join and Relationship Operators
            ["LookupValue"] = new DaxOperatorInfo
            {
                DisplayName = "Lookup Value",
                Description = "Retrieves a value from a related table based on matching criteria. Similar to VLOOKUP.",
                Category = "Lookup",
                Engine = EngineType.FormulaEngine
            },
            ["RelatedTable"] = new DaxOperatorInfo
            {
                DisplayName = "Related Table",
                Description = "Returns a table of related rows from the many side of a relationship.",
                Category = "Relationship",
                Engine = EngineType.FormulaEngine
            },
            ["Related"] = new DaxOperatorInfo
            {
                DisplayName = "Related",
                Description = "Returns a related value from the one side of a relationship.",
                Category = "Relationship",
                Engine = EngineType.FormulaEngine
            },

            // Physical Operator Types
            ["IterPhyOp"] = new DaxOperatorInfo
            {
                DisplayName = "Iterator Physical Op",
                Description = "Physical operator that iterates over data. Processes rows one at a time.",
                Category = "Physical",
                Engine = EngineType.FormulaEngine
            },
            ["LookupPhyOp"] = new DaxOperatorInfo
            {
                DisplayName = "Lookup Physical Op",
                Description = "Physical operator that performs key-based lookups into cached data.",
                Category = "Physical",
                Engine = EngineType.FormulaEngine
            },
            ["SpoolPhyOp"] = new DaxOperatorInfo
            {
                DisplayName = "Spool Physical Op",
                Description = "Physical operator that stores intermediate results in memory for reuse.",
                Category = "Physical",
                Engine = EngineType.FormulaEngine
            },
            ["ScaLogOp"] = new DaxOperatorInfo
            {
                DisplayName = "Scalar Logical Op",
                Description = "Logical operator that produces a scalar (single) value.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine
            },
            ["RelLogOp"] = new DaxOperatorInfo
            {
                DisplayName = "Relational Logical Op",
                Description = "Logical operator that produces a table (relational) result.",
                Category = "Logical",
                Engine = EngineType.FormulaEngine
            },

            // Other Common Operators
            ["Union"] = new DaxOperatorInfo
            {
                DisplayName = "Union",
                Description = "Combines multiple tables into one, appending rows.",
                Category = "Set",
                Engine = EngineType.FormulaEngine
            },
            ["Except"] = new DaxOperatorInfo
            {
                DisplayName = "Except",
                Description = "Returns rows from the first table that don't exist in the second table.",
                Category = "Set",
                Engine = EngineType.FormulaEngine
            },
            ["Intersect"] = new DaxOperatorInfo
            {
                DisplayName = "Intersect",
                Description = "Returns rows that exist in both tables.",
                Category = "Set",
                Engine = EngineType.FormulaEngine
            },
            ["Distinct"] = new DaxOperatorInfo
            {
                DisplayName = "Distinct",
                Description = "Returns unique rows from a table, removing duplicates.",
                Category = "Set",
                Engine = EngineType.FormulaEngine
            },
            ["Values"] = new DaxOperatorInfo
            {
                DisplayName = "Values",
                Description = "Returns distinct values from a column, including blank if present in the data.",
                Category = "Set",
                Engine = EngineType.FormulaEngine
            },
            ["All"] = new DaxOperatorInfo
            {
                DisplayName = "All",
                Description = "Removes filters from a table or column. Returns all rows ignoring any filters.",
                Category = "Filter Modifier",
                Engine = EngineType.FormulaEngine
            },
            ["AllSelected"] = new DaxOperatorInfo
            {
                DisplayName = "All Selected",
                Description = "Removes filters from a table while keeping external filters from slicers/visuals.",
                Category = "Filter Modifier",
                Engine = EngineType.FormulaEngine
            },
            ["TopN"] = new DaxOperatorInfo
            {
                DisplayName = "Top N",
                Description = "Returns the top N rows from a table based on a specified expression.",
                Category = "Filter",
                Engine = EngineType.FormulaEngine
            },
            ["GroupBy"] = new DaxOperatorInfo
            {
                DisplayName = "Group By",
                Description = "Groups rows by specified columns and allows aggregation calculations.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine
            },
            ["Summarize"] = new DaxOperatorInfo
            {
                DisplayName = "Summarize",
                Description = "Creates a summary table grouped by specified columns with optional aggregations.",
                Category = "Aggregation",
                Engine = EngineType.FormulaEngine
            },

            // Copy and Data Movement
            ["Copy"] = new DaxOperatorInfo
            {
                DisplayName = "Copy",
                Description = "Copies data from one location to another within the query execution.",
                Category = "Data Movement",
                Engine = EngineType.FormulaEngine
            },
            ["ProjectFusion"] = new DaxOperatorInfo
            {
                DisplayName = "Project Fusion",
                Description = "Optimizes projection operations by fusing multiple projections together.",
                Category = "Optimization",
                Engine = EngineType.FormulaEngine
            },

            // Comparison Operators (can be collapsed for simpler display)
            ["GreaterThan"] = new DaxOperatorInfo
            {
                DisplayName = ">",
                Description = "Compares if the left operand is greater than the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine
            },
            ["GreaterOrEqualTo"] = new DaxOperatorInfo
            {
                DisplayName = ">=",
                Description = "Compares if the left operand is greater than or equal to the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine
            },
            ["LessThan"] = new DaxOperatorInfo
            {
                DisplayName = "<",
                Description = "Compares if the left operand is less than the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine
            },
            ["LessOrEqualTo"] = new DaxOperatorInfo
            {
                DisplayName = "<=",
                Description = "Compares if the left operand is less than or equal to the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine
            },
            ["Equal"] = new DaxOperatorInfo
            {
                DisplayName = "=",
                Description = "Compares if the left operand equals the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine
            },
            ["NotEqual"] = new DaxOperatorInfo
            {
                DisplayName = "<>",
                Description = "Compares if the left operand does not equal the right operand.",
                Category = "Comparison",
                Engine = EngineType.FormulaEngine
            },

            // Value/Reference Operators (typically children of comparisons)
            ["Constant"] = new DaxOperatorInfo
            {
                DisplayName = "Constant",
                Description = "A constant literal value used in an expression.",
                Category = "Value",
                Engine = EngineType.FormulaEngine
            },
            ["ColValue"] = new DaxOperatorInfo
            {
                DisplayName = "Column Value",
                Description = "References the value of a column in the current row context.",
                Category = "Value",
                Engine = EngineType.FormulaEngine
            },
            ["Coerce"] = new DaxOperatorInfo
            {
                DisplayName = "Type Coercion",
                Description = "Converts a value from one data type to another.",
                Category = "Value",
                Engine = EngineType.FormulaEngine
            }
        };

        /// <summary>
        /// Gets information about a DAX operator by its name.
        /// </summary>
        /// <param name="operatorName">The operator name (e.g., "AddColumns", "Scan_Vertipaq")</param>
        /// <returns>Operator info if found, null otherwise</returns>
        public static DaxOperatorInfo GetOperatorInfo(string operatorName)
        {
            if (string.IsNullOrEmpty(operatorName))
                return null;

            // Try exact match first
            if (_operators.TryGetValue(operatorName, out var info))
                return info;

            // Try case-insensitive match
            foreach (var kvp in _operators)
            {
                if (string.Equals(kvp.Key, operatorName, System.StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            // Try partial match for composite operators like "ProjectionSpool<ProjectFusion<Copy>>"
            foreach (var kvp in _operators)
            {
                if (operatorName.StartsWith(kvp.Key, System.StringComparison.OrdinalIgnoreCase))
                    return kvp.Value;
            }

            return null;
        }

        /// <summary>
        /// Gets a human-readable display name for an operator.
        /// </summary>
        /// <param name="operatorName">The raw operator name</param>
        /// <returns>Human-readable name or the original name if not found</returns>
        public static string GetDisplayName(string operatorName)
        {
            var info = GetOperatorInfo(operatorName);
            return info?.DisplayName ?? FormatUnknownOperator(operatorName);
        }

        /// <summary>
        /// Gets a description for an operator.
        /// </summary>
        /// <param name="operatorName">The raw operator name</param>
        /// <returns>Description or a generic message if not found</returns>
        public static string GetDescription(string operatorName)
        {
            var info = GetOperatorInfo(operatorName);
            return info?.Description ?? "DAX query plan operator.";
        }

        /// <summary>
        /// Gets the category for an operator.
        /// </summary>
        /// <param name="operatorName">The raw operator name</param>
        /// <returns>Category name or "Unknown" if not found</returns>
        public static string GetCategory(string operatorName)
        {
            var info = GetOperatorInfo(operatorName);
            return info?.Category ?? "Unknown";
        }

        /// <summary>
        /// Formats an unknown operator name into a more readable form.
        /// </summary>
        private static string FormatUnknownOperator(string operatorName)
        {
            if (string.IsNullOrEmpty(operatorName))
                return "Unknown";

            // Remove template parameters like <ProjectFusion<Copy>>
            var angleBracketIndex = operatorName.IndexOf('<');
            if (angleBracketIndex > 0)
                operatorName = operatorName.Substring(0, angleBracketIndex);

            // Convert underscores to spaces
            operatorName = operatorName.Replace("_", " ");

            // Add spaces before capitals (camelCase to Title Case)
            var result = new System.Text.StringBuilder();
            for (int i = 0; i < operatorName.Length; i++)
            {
                if (i > 0 && char.IsUpper(operatorName[i]) && !char.IsUpper(operatorName[i - 1]))
                    result.Append(' ');
                result.Append(operatorName[i]);
            }

            return result.ToString();
        }
    }
}
