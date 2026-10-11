using Microsoft.AspNetCore.Mvc;

namespace StoreOps.Api.Modules.Activities;

[ApiController]
[Route("api/activities/sla-evaluations")]
public sealed class ActivitySlaController(ISlaEvaluationService sla) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SlaEvaluationResponse>> Evaluate(CancellationToken cancellationToken) =>
        Ok(await sla.EvaluateAsync(cancellationToken));
}
