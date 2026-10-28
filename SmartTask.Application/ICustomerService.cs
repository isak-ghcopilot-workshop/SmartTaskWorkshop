namespace SmartTask.Application;

public interface ICustomerService
{
    Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<CustomerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<CustomerResponse> CreateAsync(string name, string email, string? phone, CancellationToken cancellationToken);
    Task<CustomerResponse?> UpdateAsync(Guid id, string name, string email, string? phone, CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}
