using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Types of callbacks that can occur in xmSQL queries.
    /// Callbacks indicate the Storage Engine is calling back to the Formula Engine.
    /// </summary>
    public enum XmSqlCallbackType
    {
        /// <summary>No callback detected.</summary>
        None,

        /// <summary>
        /// Most common callback - passes DAX expressions to Formula Engine.
        /// WARNING: Results are NOT cached, can significantly impact performance.
        /// </summary>
        CallbackDataID,

        /// <summary>
        /// Handles query-scoped calculated columns.
        /// Used when DEFINE COLUMN creates temporary columns.
        /// </summary>
        EncodeCallback,

        /// <summary>
        /// Optimizes PRODUCT/PRODUCTX functions using logarithms.
        /// Converts multiplication to addition via log transformation.
        /// </summary>
        LogAbsValueCallback,

        /// <summary>
        /// Manages type conversions and rounding operations.
        /// </summary>
        RoundValueCallback,

        /// <summary>
        /// Transforms values to sorted positions for MIN/MAX operations.
        /// Used for optimizing certain aggregation patterns.
        /// </summary>
        MinMaxColumnPositionCallback,

        /// <summary>
        /// Evaluates conditional logic for blank row handling.
        /// </summary>
        Cond
    }

    /// <summary>
    /// Types of JOIN operations in xmSQL.
    /// </summary>
    public enum XmSqlJoinType
    {
        /// <summary>No join detected.</summary>
        None,

        /// <summary>
        /// Many-to-one relationship joins; maintains all rows from primary table.
        /// </summary>
        LeftOuterJoin,

        /// <summary>
        /// Used for cartesian products with temporary tables.
        /// </summary>
        InnerJoinReducedBy,

        /// <summary>
        /// Pushes filters from many-side to one-side without explicit relationship.
        /// Auto-selected when: ratio less than 20%, many-side has 131,072+ rows, 16,384+ unique values.
        /// </summary>
        ReverseHashJoin,

        /// <summary>
        /// Bitmap variant of reverse hash join.
        /// </summary>
        ReverseBitmapJoin
    }

    /// <summary>
    /// Information extracted from an xmSQL query.
    /// </summary>
    public class XmSqlInfo
    {
        /// <summary>The raw xmSQL text.</summary>
        public string RawQuery { get; set; }

        /// <summary>List of callbacks detected in the query.</summary>
        public List<XmSqlCallbackType> Callbacks { get; set; } = new List<XmSqlCallbackType>();

        /// <summary>Primary callback type (the most significant one).</summary>
        public XmSqlCallbackType PrimaryCallback { get; set; } = XmSqlCallbackType.None;

        /// <summary>Join types detected in the query.</summary>
        public List<XmSqlJoinType> JoinTypes { get; set; } = new List<XmSqlJoinType>();

        /// <summary>Whether the query uses a DEFINE TABLE batch.</summary>
        public bool UsesBatch { get; set; }

        /// <summary>Whether the query uses bitmap indexing (SIMPLEINDEXN/ININDEX).</summary>
        public bool UsesBitmapIndex { get; set; }

        /// <summary>Datacache kind if specified (DENSE, AUTO, etc.).</summary>
        public string DatacacheKind { get; set; }

        /// <summary>Tables referenced in the query.</summary>
        public List<string> ReferencedTables { get; set; } = new List<string>();

        /// <summary>Columns referenced in the query.</summary>
        public List<string> ReferencedColumns { get; set; } = new List<string>();

        /// <summary>Whether the query has performance warnings.</summary>
        public bool HasPerformanceWarning => PrimaryCallback == XmSqlCallbackType.CallbackDataID;

        /// <summary>Performance warning message if applicable.</summary>
        public string PerformanceWarning
        {
            get
            {
                if (PrimaryCallback == XmSqlCallbackType.CallbackDataID)
                    return "CallbackDataID detected: Storage Engine is calling Formula Engine during scan. Results are NOT cached.";
                return null;
            }
        }
    }

    /// <summary>
    /// Parser for xmSQL queries to extract metadata and detect patterns.
    /// </summary>
    public static class XmSqlParser
    {
        // Callback detection patterns
        private static readonly Regex CallbackDataIdPattern = new Regex(
            @"CallbackDataID\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex EncodeCallbackPattern = new Regex(
            @"EncodeCallback\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex LogAbsValueCallbackPattern = new Regex(
            @"LogAbsValueCallback\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex RoundValueCallbackPattern = new Regex(
            @"RoundValueCallback\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex MinMaxCallbackPattern = new Regex(
            @"MinMaxColumnPositionCallback\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CondCallbackPattern = new Regex(
            @"\bCond\s*\(",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Join detection patterns
        private static readonly Regex LeftOuterJoinPattern = new Regex(
            @"LEFT\s+OUTER\s+JOIN",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex InnerJoinReducedByPattern = new Regex(
            @"INNER\s+JOIN\s+.*\s+REDUCED\s+BY",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ReverseHashJoinPattern = new Regex(
            @"REVERSE\s+HASH\s+JOIN",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ReverseBitmapJoinPattern = new Regex(
            @"REVERSE\s+BITMAP\s+JOIN",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Feature detection patterns
        private static readonly Regex DefineTablePattern = new Regex(
            @"DEFINE\s+TABLE",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex BitmapIndexPattern = new Regex(
            @"SIMPLEINDEXN|ININDEX",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex DatacacheKindPattern = new Regex(
            @"SET\s+DC_KIND\s*=\s*""(\w+)""",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Reference extraction patterns
        private static readonly Regex TableReferencePattern = new Regex(
            @"'([^']+)'(?=\[)",
            RegexOptions.Compiled);

        private static readonly Regex ColumnReferencePattern = new Regex(
            @"'([^']+)'\[([^\]]+)\]",
            RegexOptions.Compiled);

        /// <summary>
        /// Parses an xmSQL query and extracts metadata.
        /// </summary>
        /// <param name="xmSql">The xmSQL query text.</param>
        /// <returns>Parsed information about the query.</returns>
        public static XmSqlInfo Parse(string xmSql)
        {
            if (string.IsNullOrWhiteSpace(xmSql))
                return new XmSqlInfo { RawQuery = xmSql };

            var info = new XmSqlInfo { RawQuery = xmSql };

            // Detect callbacks
            DetectCallbacks(xmSql, info);

            // Detect join types
            DetectJoinTypes(xmSql, info);

            // Detect features
            info.UsesBatch = DefineTablePattern.IsMatch(xmSql);
            info.UsesBitmapIndex = BitmapIndexPattern.IsMatch(xmSql);

            // Extract datacache kind
            var dcMatch = DatacacheKindPattern.Match(xmSql);
            if (dcMatch.Success)
                info.DatacacheKind = dcMatch.Groups[1].Value;

            // Extract references
            ExtractReferences(xmSql, info);

            return info;
        }

        /// <summary>
        /// Detects the primary callback type in an xmSQL query.
        /// </summary>
        /// <param name="xmSql">The xmSQL query text.</param>
        /// <returns>The detected callback type.</returns>
        public static XmSqlCallbackType DetectCallbackType(string xmSql)
        {
            if (string.IsNullOrWhiteSpace(xmSql))
                return XmSqlCallbackType.None;

            // Check in order of significance (CallbackDataID is most important)
            if (CallbackDataIdPattern.IsMatch(xmSql))
                return XmSqlCallbackType.CallbackDataID;

            if (EncodeCallbackPattern.IsMatch(xmSql))
                return XmSqlCallbackType.EncodeCallback;

            if (LogAbsValueCallbackPattern.IsMatch(xmSql))
                return XmSqlCallbackType.LogAbsValueCallback;

            if (RoundValueCallbackPattern.IsMatch(xmSql))
                return XmSqlCallbackType.RoundValueCallback;

            if (MinMaxCallbackPattern.IsMatch(xmSql))
                return XmSqlCallbackType.MinMaxColumnPositionCallback;

            if (CondCallbackPattern.IsMatch(xmSql))
                return XmSqlCallbackType.Cond;

            return XmSqlCallbackType.None;
        }

        /// <summary>
        /// Detects all callbacks in an xmSQL query.
        /// </summary>
        private static void DetectCallbacks(string xmSql, XmSqlInfo info)
        {
            if (CallbackDataIdPattern.IsMatch(xmSql))
            {
                info.Callbacks.Add(XmSqlCallbackType.CallbackDataID);
                info.PrimaryCallback = XmSqlCallbackType.CallbackDataID;
            }

            if (EncodeCallbackPattern.IsMatch(xmSql))
            {
                info.Callbacks.Add(XmSqlCallbackType.EncodeCallback);
                if (info.PrimaryCallback == XmSqlCallbackType.None)
                    info.PrimaryCallback = XmSqlCallbackType.EncodeCallback;
            }

            if (LogAbsValueCallbackPattern.IsMatch(xmSql))
            {
                info.Callbacks.Add(XmSqlCallbackType.LogAbsValueCallback);
                if (info.PrimaryCallback == XmSqlCallbackType.None)
                    info.PrimaryCallback = XmSqlCallbackType.LogAbsValueCallback;
            }

            if (RoundValueCallbackPattern.IsMatch(xmSql))
            {
                info.Callbacks.Add(XmSqlCallbackType.RoundValueCallback);
                if (info.PrimaryCallback == XmSqlCallbackType.None)
                    info.PrimaryCallback = XmSqlCallbackType.RoundValueCallback;
            }

            if (MinMaxCallbackPattern.IsMatch(xmSql))
            {
                info.Callbacks.Add(XmSqlCallbackType.MinMaxColumnPositionCallback);
                if (info.PrimaryCallback == XmSqlCallbackType.None)
                    info.PrimaryCallback = XmSqlCallbackType.MinMaxColumnPositionCallback;
            }

            if (CondCallbackPattern.IsMatch(xmSql))
            {
                info.Callbacks.Add(XmSqlCallbackType.Cond);
                if (info.PrimaryCallback == XmSqlCallbackType.None)
                    info.PrimaryCallback = XmSqlCallbackType.Cond;
            }
        }

        /// <summary>
        /// Detects join types in an xmSQL query.
        /// </summary>
        private static void DetectJoinTypes(string xmSql, XmSqlInfo info)
        {
            if (LeftOuterJoinPattern.IsMatch(xmSql))
                info.JoinTypes.Add(XmSqlJoinType.LeftOuterJoin);

            if (InnerJoinReducedByPattern.IsMatch(xmSql))
                info.JoinTypes.Add(XmSqlJoinType.InnerJoinReducedBy);

            if (ReverseHashJoinPattern.IsMatch(xmSql))
                info.JoinTypes.Add(XmSqlJoinType.ReverseHashJoin);

            if (ReverseBitmapJoinPattern.IsMatch(xmSql))
                info.JoinTypes.Add(XmSqlJoinType.ReverseBitmapJoin);
        }

        /// <summary>
        /// Extracts table and column references from an xmSQL query.
        /// </summary>
        private static void ExtractReferences(string xmSql, XmSqlInfo info)
        {
            var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (Match match in ColumnReferencePattern.Matches(xmSql))
            {
                var table = match.Groups[1].Value;
                var column = match.Groups[2].Value;

                tables.Add(table);
                columns.Add($"'{table}'[{column}]");
            }

            info.ReferencedTables.AddRange(tables);
            info.ReferencedColumns.AddRange(columns);
        }

        /// <summary>
        /// Gets a human-readable description for a callback type.
        /// </summary>
        public static string GetCallbackDescription(XmSqlCallbackType callbackType)
        {
            return callbackType switch
            {
                XmSqlCallbackType.CallbackDataID =>
                    "CallbackDataID: Storage Engine calling Formula Engine for DAX expression evaluation. Results are NOT cached - can significantly impact performance.",
                XmSqlCallbackType.EncodeCallback =>
                    "EncodeCallback: Handles query-scoped calculated columns (DEFINE COLUMN).",
                XmSqlCallbackType.LogAbsValueCallback =>
                    "LogAbsValueCallback: Optimizes PRODUCT/PRODUCTX using logarithm transformation.",
                XmSqlCallbackType.RoundValueCallback =>
                    "RoundValueCallback: Manages type conversions and rounding operations.",
                XmSqlCallbackType.MinMaxColumnPositionCallback =>
                    "MinMaxColumnPositionCallback: Transforms values to sorted positions for MIN/MAX optimization.",
                XmSqlCallbackType.Cond =>
                    "Cond: Evaluates conditional logic for blank row handling.",
                _ => null
            };
        }

        /// <summary>
        /// Gets the severity level for a callback type.
        /// </summary>
        public static IssueSeverity GetCallbackSeverity(XmSqlCallbackType callbackType)
        {
            return callbackType switch
            {
                XmSqlCallbackType.CallbackDataID => IssueSeverity.Warning,
                _ => IssueSeverity.Info
            };
        }

        /// <summary>
        /// Gets a human-readable description for a join type.
        /// </summary>
        public static string GetJoinDescription(XmSqlJoinType joinType)
        {
            return joinType switch
            {
                XmSqlJoinType.LeftOuterJoin =>
                    "LEFT OUTER JOIN: Many-to-one relationship join maintaining all rows from primary table.",
                XmSqlJoinType.InnerJoinReducedBy =>
                    "INNER JOIN REDUCED BY: Cartesian product with temporary table reduction.",
                XmSqlJoinType.ReverseHashJoin =>
                    "REVERSE HASH JOIN: Pushes filters from many-side to one-side without explicit relationship.",
                XmSqlJoinType.ReverseBitmapJoin =>
                    "REVERSE BITMAP JOIN: Bitmap variant of reverse hash join for better performance.",
                _ => null
            };
        }
    }
}
