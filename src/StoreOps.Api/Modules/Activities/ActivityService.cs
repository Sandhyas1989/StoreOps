using StoreOps.Api.Modules.Activities.Persistence;
using StoreOps.Api.Modules.Staff;
using StoreOps.Api.Shared.Errors;
using StoreOps.Api.Shared.Events;

namespace StoreOps.Api.Modules.Activities;

/// <summary>Read-only contract other modules may depend on for activity lookups.</summary>
public interface IActivityLookup
{
    Task<IReadOnlyList<Activity>> ListAsync(CancellationToken cancellationToken);

    Task<Activity?> FindAsync(Guid id, CancellationToken cancellationToken);
}

public interface IActivityService
{
    Task<IReadOnlyList<Activity>> ListAsync(ActivityFilter filter, CancellationToken cancellationToken);

    Task<Activity> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<Activity> CreateAsync(CreateActivityRequest request, CancellationToken cancellationToken);

    Task<Activity> UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public sealed class ActivityService(
    IActivityRepository repository,
    IStaffLookup staff,
    IEventBus eventBus,
    TimeProvider clock) : IActivityService, IActivityLookup
{
    public const string NotFoundCode = "ACTIVITY_NOT_FOUND";
    public const string ClosedCode = "ACTIVITY_CLOSED";

    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 2000;

    public async Task<IReadOnlyList<Activity>> ListAsync(ActivityFilter filter, CancellationToken cancellationToken)
    {
        var activities = await repository.ListAsync(cancellationToken);
        return activities
            .Where(a => filter.Status is null || a.Status == filter.Status)
            .Where(a => filter.StoreId is null || string.Equals(a.StoreId, filter.StoreId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(a => a.CreatedAt)
            .ToList();
    }

    public Task<IReadOnlyList<Activity>> ListAsync(CancellationToken cancellationToken) =>
        ListAsync(ActivityFilter.None, cancellationToken);

    public Task<Activity?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        repository.GetAsync(id, cancellationToken);

    public async Task<Activity> GetAsync(Guid id, CancellationToken cancellationToken) =>
        await repository.GetAsync(id, cancellationToken) ?? throw NotFound(id);

    public async Task<Activity> CreateAsync(CreateActivityRequest request, CancellationToken cancellationToken)
    {
        var errors = new ValidationErrorBuilder();
        var title = errors.RequireText("title", request.Title, "Title is required.");
        var storeId = errors.RequireText("storeId", request.StoreId, "Store id is required.");
        ValidateText(title, request.Description, errors);
        await ValidateAssigneeAsync(request.AssignedStaffId, errors, cancellationToken);
        errors.ThrowIfAny();

        var now = clock.GetUtcNow();
        var activity = new Activity
        {
            Id = Guid.NewGuid(),
            Title = title,
            Description = request.Description,
            StoreId = storeId,
            Status = ActivityStatus.Pending,
            Priority = request.Priority ?? ActivityPriority.Medium,
            AssignedStaffId = request.AssignedStaffId,
            DueAt = request.DueAt,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await repository.AddAsync(activity, cancellationToken);
        await eventBus.PublishAsync(
            new ActivityCreated(activity.Id, activity.Title, activity.StoreId, activity.Priority, now),
            cancellationToken);
        return activity;
    }

    public async Task<Activity> UpdateAsync(Guid id, UpdateActivityRequest request, CancellationToken cancellationToken)
    {
        var existing = await GetAsync(id, cancellationToken);
        if (existing.IsClosed)
        {
            throw new ConflictError(ClosedCode, $"Activity '{id}' is {existing.Status} and can no longer be changed.");
        }

        var errors = new ValidationErrorBuilder();
        var title = request.Title?.Trim();
        if (title is { Length: 0 })
        {
            errors.Add("title", "Title cannot be empty.");
        }

        ValidateText(title, request.Description, errors);
        await ValidateAssigneeAsync(request.AssignedStaffId, errors, cancellationToken);
        errors.ThrowIfAny();

        var updated = existing with
        {
            Title = title ?? existing.Title,
            Description = request.Description ?? existing.Description,
            Status = request.Status ?? existing.Status,
            Priority = request.Priority ?? existing.Priority,
            AssignedStaffId = request.AssignedStaffId ?? existing.AssignedStaffId,
            DueAt = request.DueAt ?? existing.DueAt,
            UpdatedAt = clock.GetUtcNow(),
        };

        await repository.UpdateAsync(updated, cancellationToken);
        return updated;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        if (!await repository.DeleteAsync(id, cancellationToken))
        {
            throw NotFound(id);
        }

        await eventBus.PublishAsync(new ActivityDeleted(id, clock.GetUtcNow()), cancellationToken);
    }

    private static NotFoundError NotFound(Guid id) => new(NotFoundCode, $"Activity '{id}' was not found.");

    private static void ValidateText(string? title, string? description, ValidationErrorBuilder errors)
    {
        if (title is { Length: > MaxTitleLength })
        {
            errors.Add("title", $"Title must be at most {MaxTitleLength} characters.");
        }

        if (description is { Length: > MaxDescriptionLength })
        {
            errors.Add("description", $"Description must be at most {MaxDescriptionLength} characters.");
        }
    }

    private async Task ValidateAssigneeAsync(Guid? staffId, ValidationErrorBuilder errors, CancellationToken cancellationToken)
    {
        if (staffId is { } id && await staff.FindAsync(id, cancellationToken) is null)
        {
            errors.Add("assignedStaffId", $"Staff member '{id}' does not exist.");
        }
    }
}
