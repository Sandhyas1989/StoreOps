using Microsoft.AspNetCore.Mvc;

namespace StoreOps.Api.Modules.Activities;

[ApiController]
[Route("api/activities")]
public sealed class ActivitiesController(IActivityService activities) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ActivityResponse>>> List(
        [FromQuery] ActivityStatus? status,
        [FromQuery] string? storeId,
        CancellationToken cancellationToken)
    {
        var result = await activities.ListAsync(new ActivityFilter(status, storeId), cancellationToken);
        return Ok(result.Select(ActivityResponse.From).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ActivityResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ActivityResponse.From(await activities.GetAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ActivityResponse>> Create(CreateActivityRequest request, CancellationToken cancellationToken)
    {
        var created = await activities.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ActivityResponse.From(created));
    }

    [HttpPatch("{id}")]
    public async Task<ActionResult<ActivityResponse>> Update(Guid id, UpdateActivityRequest request, CancellationToken cancellationToken) =>
        Ok(ActivityResponse.From(await activities.UpdateAsync(id, request, cancellationToken)));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await activities.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
