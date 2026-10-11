using Microsoft.AspNetCore.Mvc;

namespace StoreOps.Api.Modules.Alerts;

[ApiController]
[Route("api/alerts")]
public sealed class AlertsController(IAlertService alerts) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AlertResponse>>> List(
        [FromQuery] AlertSeverity? severity,
        [FromQuery] Guid? recipientStaffId,
        CancellationToken cancellationToken)
    {
        var result = await alerts.ListAsync(severity, recipientStaffId, cancellationToken);
        return Ok(result.Select(AlertResponse.From).ToList());
    }
}
