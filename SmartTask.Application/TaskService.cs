using SmartTask.Domain;

namespace SmartTask.Application;

public sealed class TaskService(ITaskRepository repository) : ITaskService
{
    public async Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(ToResponse).ToArray();

    public async Task<TaskResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(id, cancellationToken);
        return task is null ? null : ToResponse(task);
    }

    public async Task<TaskResponse> CreateAsync(string title, string? description, CancellationToken cancellationToken)
    {
        var task = TaskItem.Create(title, description);
        await repository.AddAsync(task, cancellationToken);
        return ToResponse(task);
    }

    public async Task<TaskResponse?> UpdateAsync(
        Guid id, string title, string? description, bool isCompleted, CancellationToken cancellationToken)
    {
        var task = await repository.GetByIdAsync(id, cancellationToken);
        if (task is null)
            return null;

        var updated = task.Update(title, description, isCompleted);
        return await repository.UpdateAsync(updated, cancellationToken) ? ToResponse(updated) : null;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(id, cancellationToken);

    private static TaskResponse ToResponse(TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.IsCompleted, task.CreatedAt);
}
