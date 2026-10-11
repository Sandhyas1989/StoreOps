using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Alerts;

/// <summary>
/// Alerts the store's shift leads when an activity breaches its SLA, and its store managers when the breach escalates.
/// Repeated events for the same activity and recipient raise one alert.
/// </summary>
public sealed class SlaBreachAlertHandler(IAlertService alerts, IStaffLookup staff) : IEventHandler<ActivitySlaBreached>
{
    public async Task HandleAsync(ActivitySlaBreached integrationEvent, CancellationToken cancellationToken)
    {
        var escalated = integrationEvent.Stage == SlaStage.Escalation;
        var role = escalated ? StaffRole.StoreManager : StaffRole.ShiftLead;
        var members = await staff.ListAsync(cancellationToken);

        foreach (var recipient in members.Where(m => m.StoreId == integrationEvent.StoreId && m.Role == role))
        {
            await alerts.RaiseAsync(
                new RaiseAlertCommand(
                    escalated ? AlertTypes.SlaEscalation : AlertTypes.SlaBreach,
                    escalated ? AlertSeverity.Critical : AlertSeverity.Warning,
                    escalated
                        ? $"SLA breach of activity '{integrationEvent.Title}' in store {integrationEvent.StoreId} has escalated."
                        : $"Activity '{integrationEvent.Title}' in store {integrationEvent.StoreId} has breached its SLA.",
                    SourceModule: "activities",
                    SourceId: integrationEvent.ActivityId,
                    RecipientStaffId: recipient.Id,
                    RecipientRole: recipient.Role),
                cancellationToken);
        }
    }
}
