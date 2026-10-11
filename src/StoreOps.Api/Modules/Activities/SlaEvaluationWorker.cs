using Microsoft.Extensions.Options;

namespace StoreOps.Api.Modules.Activities;

/// <summary>
/// Runs the SLA evaluation on every <see cref="SlaOptions.EvaluationInterval"/> tick. All business rules stay in
/// <see cref="ISlaEvaluationService"/>; the worker only schedules it and keeps running if a tick fails unexpectedly.
/// </summary>
public sealed class SlaEvaluationWorker(
    IServiceScopeFactory scopes,
    IOptions<SlaOptions> options,
    TimeProvider clock,
    ILogger<SlaEvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.EvaluationInterval, clock);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                await EvaluateOnceAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host is stopping.
        }
    }

    private async Task EvaluateOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var evaluator = scope.ServiceProvider.GetRequiredService<ISlaEvaluationService>();
            await evaluator.EvaluateAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "SLA evaluation tick failed");
        }
    }
}
