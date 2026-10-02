using Microsoft.AspNetCore.Mvc;

namespace StoreOps.Api.Modules.Programmes;

[ApiController]
[Route("api/programmes")]
public sealed class ProgrammesController(IProgrammeService programmes) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProgrammeResponse>>> List(CancellationToken cancellationToken)
    {
        var result = await programmes.ListAsync(cancellationToken);
        return Ok(result.Select(ProgrammeResponse.From).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProgrammeResponse>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(ProgrammeResponse.From(await programmes.GetAsync(id, cancellationToken)));

    [HttpPost]
    public async Task<ActionResult<ProgrammeResponse>> Create(CreateProgrammeRequest request, CancellationToken cancellationToken)
    {
        var created = await programmes.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ProgrammeResponse.From(created));
    }

    [HttpPost("{id}/members")]
    public async Task<ActionResult<ProgrammeResponse>> AddMember(Guid id, AddProgrammeMemberRequest request, CancellationToken cancellationToken) =>
        Ok(ProgrammeResponse.From(await programmes.AddMemberAsync(id, request, cancellationToken)));
}
