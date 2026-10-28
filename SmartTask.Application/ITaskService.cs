namespace SmartTask.Application;

public interface ITaskService
{
    Task<IReadOnlyList<TaskResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<TaskResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<TaskResponse> CreateAsync(string title, string? description, CancellationToken cancellationToken);
    Task<TaskResponse?> UpdateAsync(Guid id, string title, string? description, bool isCompleted, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
