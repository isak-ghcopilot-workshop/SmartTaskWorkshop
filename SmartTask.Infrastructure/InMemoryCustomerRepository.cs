using SmartTask.Application;
using SmartTask.Domain;

namespace SmartTask.Infrastructure;

public sealed class InMemoryCustomerRepository : ICustomerRepository
{
    private readonly Dictionary<Guid, Customer> _customers = [];
    private readonly object _lock = new();

    public Task<IReadOnlyList<Customer>> GetAllAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult<IReadOnlyList<Customer>>(
                _customers.Values.OrderBy(customer => customer.CreatedAt).ThenBy(customer => customer.Id).ToArray());
        }
    }

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_customers.GetValueOrDefault(id));
        }
    }

    public Task AddAsync(Customer customer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            _customers.Add(customer.Id, customer);
        }
        return Task.CompletedTask;
    }

    public Task<bool> UpdateAsync(Customer customer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            if (!_customers.ContainsKey(customer.Id))
                return Task.FromResult(false);

            _customers[customer.Id] = customer;
            return Task.FromResult(true);
        }
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_lock)
        {
            return Task.FromResult(_customers.Remove(id));
        }
    }
}
