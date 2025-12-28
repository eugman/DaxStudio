using ADOTabular;

namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for resolving column internal IDs to display names.
    /// </summary>
    public interface IColumnNameResolver
    {
        /// <summary>
        /// Resolves a column internal reference to its display name.
        /// </summary>
        /// <param name="columnRef">Internal column reference/ID</param>
        /// <returns>Resolved column name, or original if not found</returns>
        string ResolveColumnName(string columnRef);

        /// <summary>
        /// Resolves all column references in an operation string.
        /// </summary>
        /// <param name="operation">Raw operation string with column IDs</param>
        /// <returns>Operation string with resolved column names</returns>
        string ResolveOperationString(string operation);

        /// <summary>
        /// Initializes resolver with column metadata from current connection.
        /// </summary>
        /// <param name="columns">Column collection from ADOTabular</param>
        void Initialize(ADOTabularColumnCollection columns);

        /// <summary>
        /// Gets whether the resolver has been initialized.
        /// </summary>
        bool IsInitialized { get; }
    }
}
