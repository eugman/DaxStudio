namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Types of performance issues that can be detected in query plans.
    /// </summary>
    public enum IssueType
    {
        /// <summary>
        /// Spool or SpoolLookup operation with high row counts indicating
        /// data being materialized in memory before processing.
        /// </summary>
        ExcessiveMaterialization,

        /// <summary>
        /// CallbackDataID operation indicating row-by-row processing
        /// that can significantly impact performance.
        /// </summary>
        CallbackDataID

        // Future expansions:
        // HighCardinality,
        // UnnecessaryScan,
        // FilterPushdownFailure
    }
}
