namespace DaxStudio.UI.Model
{
    /// <summary>
    /// Specifies the type of query execution plan.
    /// </summary>
    public enum PlanType
    {
        /// <summary>
        /// Physical query plan showing actual execution operators.
        /// </summary>
        Physical,

        /// <summary>
        /// Logical query plan showing logical operation tree.
        /// </summary>
        Logical
    }
}
