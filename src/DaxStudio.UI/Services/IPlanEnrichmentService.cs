using System.Collections.Generic;
using System.Threading.Tasks;
using DaxStudio.UI.Model;
using DaxStudio.UI.ViewModels;

namespace DaxStudio.UI.Services
{
    /// <summary>
    /// Service for enriching raw query plan data with timing metrics,
    /// resolved column names, and detected performance issues.
    /// </summary>
    public interface IPlanEnrichmentService
    {
        /// <summary>
        /// Enriches a physical query plan with timing and metadata.
        /// </summary>
        /// <param name="rawPlan">Raw physical plan rows from trace</param>
        /// <param name="timingEvents">Storage engine timing events</param>
        /// <param name="columnResolver">Column name resolver</param>
        /// <param name="activityId">Activity ID for correlation</param>
        /// <returns>Fully enriched query plan</returns>
        Task<EnrichedQueryPlan> EnrichPhysicalPlanAsync(
            IEnumerable<PhysicalQueryPlanRow> rawPlan,
            IEnumerable<TraceStorageEngineEvent> timingEvents,
            IColumnNameResolver columnResolver,
            string activityId);

        /// <summary>
        /// Enriches a logical query plan with metadata.
        /// </summary>
        /// <param name="rawPlan">Raw logical plan rows from trace</param>
        /// <param name="columnResolver">Column name resolver</param>
        /// <param name="activityId">Activity ID for correlation</param>
        /// <returns>Fully enriched query plan</returns>
        Task<EnrichedQueryPlan> EnrichLogicalPlanAsync(
            IEnumerable<LogicalQueryPlanRow> rawPlan,
            IColumnNameResolver columnResolver,
            string activityId);
    }
}
