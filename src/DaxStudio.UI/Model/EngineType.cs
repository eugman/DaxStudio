namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Specifies which engine executes an operation.
    /// </summary>
    public enum EngineType
    {
        /// <summary>
        /// Engine type cannot be determined.
        /// </summary>
        Unknown,

        /// <summary>
        /// Storage Engine (VertiPaq) operation.
        /// </summary>
        StorageEngine,

        /// <summary>
        /// Formula Engine operation.
        /// </summary>
        FormulaEngine,

        /// <summary>
        /// DirectQuery operation (queries sent to external data source).
        /// </summary>
        DirectQuery
    }
}
