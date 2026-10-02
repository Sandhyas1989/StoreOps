namespace StoreOps.Api.Modules.Staff;

public sealed record StaffResponse(Guid Id, string Name, StaffRole Role, string StoreId)
{
    public static StaffResponse From(StaffMember member) => new(member.Id, member.Name, member.Role, member.StoreId);
}
