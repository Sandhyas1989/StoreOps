namespace StoreOps.Api.Modules.Staff;

public enum StaffRole
{
    StoreManager,
    ShiftLead,
    Associate,
}

public sealed record StaffMember(Guid Id, string Name, StaffRole Role, string StoreId);

/// <summary>Deterministic seed data; the staff module is read-only in the baseline.</summary>
public static class StaffSeed
{
    public static readonly Guid StoreManagerId = Guid.Parse("5e1f0000-0000-4000-8000-000000000001");
    public static readonly Guid ShiftLeadId = Guid.Parse("5e1f0000-0000-4000-8000-000000000002");
    public static readonly Guid AssociateId = Guid.Parse("5e1f0000-0000-4000-8000-000000000003");

    public static IReadOnlyList<StaffMember> All { get; } =
    [
        new(StoreManagerId, "Priya Nair", StaffRole.StoreManager, "STORE-001"),
        new(ShiftLeadId, "Daniel Okafor", StaffRole.ShiftLead, "STORE-001"),
        new(AssociateId, "Mei Chen", StaffRole.Associate, "STORE-001"),
    ];
}
