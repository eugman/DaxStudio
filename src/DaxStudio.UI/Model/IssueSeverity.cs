namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Severity level for detected performance issues.
    /// </summary>
    public enum IssueSeverity
    {
        /// <summary>
        /// Informational - may not require action.
        /// </summary>
        Info,

        /// <summary>
        /// Warning - potential issue that should be investigated.
        /// </summary>
        Warning,

        /// <summary>
        /// Error - definite problem that requires attention.
        /// </summary>
        Error
    }
}
