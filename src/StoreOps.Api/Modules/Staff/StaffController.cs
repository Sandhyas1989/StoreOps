using Microsoft.AspNetCore.Mvc;

namespace StoreOps.Api.Modules.Staff;

[ApiController]
[Route("api/staff")]
public sealed class StaffController(IStaffService staff) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StaffResponse>>> List(CancellationToken cancellationToken)
    {
        var members = await staff.ListAsync(cancellationToken);
        return Ok(members.Select(StaffResponse.From).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<StaffResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(StaffResponse.From(await staff.GetAsync(id, cancellationToken)));
}
