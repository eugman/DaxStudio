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
        CallbackDataID,

        /// <summary>
        /// High Formula Engine ratio - FE time exceeds threshold of total.
        /// Indicates single-threaded FE work dominating query time.
        /// </summary>
        HighFormulaEngineRatio,

        /// <summary>
        /// CrossApply operation with high row counts indicating
        /// expensive row-by-row iteration (nested loop/cartesian).
        /// </summary>
        ExcessiveCrossApply,

        /// <summary>
        /// xmSQL callback detected in Storage Engine query.
        /// Indicates SE calling back to FE during scan.
        /// </summary>
        XmSqlCallback,

        /// <summary>
        /// High number of Storage Engine queries without cache hits.
        /// May indicate inefficient query pattern.
        /// </summary>
        LowCacheHitRatio,

        /// <summary>
        /// Large table scan followed by filter, suggesting
        /// filter could not be pushed down to scan.
        /// </summary>
        InefficientFilter,

        /// <summary>
        /// Large data size in SE operation (100MB+ warning, 1GB+ error).
        /// Indicates large data volumes flowing through the query.
        /// </summary>
        ExcessiveDataSize
    }
}
