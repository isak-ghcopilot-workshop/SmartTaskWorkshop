using SmartTask.Application;
using SmartTask.Domain;

namespace SmartTask.Infrastructure;

public sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly Dictionary<Guid, TaskItem> _tasks = [];
    private readonly object _lock = new();

    public Task<IReadOnlyList<TaskItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<TaskItem>>(
                _tasks.Values.OrderBy(task => task.CreatedAt).ThenBy(task => task.Id).ToArray());
        }
    }

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_tasks.GetValueOrDefault(id));
        }
    }

    public Task AddAsync(TaskItem task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _tasks.Add(task.Id, task);
        }
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(TaskItem task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (!_tasks.ContainsKey(task.Id))
                return Task.FromResult(false);

            _tasks[task.Id] = task;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_tasks.Remove(id));
        }
    }
}
