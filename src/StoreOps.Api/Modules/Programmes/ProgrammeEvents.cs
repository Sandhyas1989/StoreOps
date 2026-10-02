using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Programmes;

public sealed record ProgrammeMemberAdded(Guid ProgrammeId, Guid StaffId, DateTimeOffset OccurredAt) : IIntegrationEvent;
