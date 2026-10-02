using StoreOps.Api.Modules.Activities;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Alerts;

/// <summary>Raises a warning alert when a high-priority activity is created.</summary>
public sealed class HighPriorityActivityAlertHandler(IAlertService alerts) : IEventHandler<ActivityCreated>
{
    public async Task HandleAsync(ActivityCreated integrationEvent, CancellationToken cancellationToken)
    {
        if (integrationEvent.Priority != ActivityPriority.High)
        {
            return;
        }

        await alerts.RaiseAsync(
            new RaiseAlertCommand(
                AlertTypes.HighPriorityActivity,
                AlertSeverity.Warning,
                $"High-priority activity '{integrationEvent.Title}' created for store {integrationEvent.StoreId}.",
                SourceModule: "activities",
                SourceId: integrationEvent.ActivityId),
            cancellationToken);
    }
}
