using SmartTask.Domain;

namespace SmartTask.Application;

public sealed class CustomerService(ICustomerRepository repository) : ICustomerService
{
    public async Task<IReadOnlyList<CustomerResponse>> GetAllAsync(CancellationToken cancellationToken) =>
        (await repository.GetAllAsync(cancellationToken)).Select(ToResponse).ToArray();

    public async Task<CustomerResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(id, cancellationToken);
        return customer is null ? null : ToResponse(customer);
    }

    public async Task<CustomerResponse> CreateAsync(
        string name, string email, string? phone, CancellationToken cancellationToken)
    {
        var customer = Customer.Create(name, email, phone);
        await repository.AddAsync(customer, cancellationToken);
        return ToResponse(customer);
    }

    public async Task<CustomerResponse?> UpdateAsync(
        Guid id, string name, string email, string? phone, CancellationToken cancellationToken)
    {
        var customer = await repository.GetByIdAsync(id, cancellationToken);
        if (customer is null)
            return null;

        var updated = customer.Update(name, email, phone);
        return await repository.UpdateAsync(updated, cancellationToken) ? ToResponse(updated) : null;
    }

    public Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        repository.DeleteAsync(id, cancellationToken);

    private static CustomerResponse ToResponse(Customer customer) =>
        new(customer.Id, customer.Name, customer.Email, customer.Phone, customer.CreatedAt);
}
