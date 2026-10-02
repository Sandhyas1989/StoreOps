using Microsoft.AspNetCore.Mvc;

namespace StoreOps.Api.Modules.Reports;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController(IReportService reports) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<StoreSummaryReport>> Summary(CancellationToken cancellationToken) =>
        Ok(await reports.GetStoreSummaryAsync(cancellationToken));
}
