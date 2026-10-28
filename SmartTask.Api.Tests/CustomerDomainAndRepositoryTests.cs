using SmartTask.Application;
using SmartTask.Domain;
using SmartTask.Infrastructure;

namespace SmartTask.Api.Tests;

public sealed class CustomerDomainAndRepositoryTests
{
    [Theory]
    [InlineData(null, "customer@example.com")]
    [InlineData("", "customer@example.com")]
    [InlineData(" ", "customer@example.com")]
    [InlineData("Customer", null)]
    [InlineData("Customer", "")]
    [InlineData("Customer", " ")]
    [InlineData("Customer", "invalid")]
    [InlineData("Customer", "a@@example.com")]
    public void Domain_rejects_invalid_contact_details(string? name, string? email)
    {
        Assert.ThrowsAny<ArgumentException>(() => Customer.Create(name!, email!, null));
    }

    [Fact]
    public void Domain_enforces_limits_and_updates_without_mutating_the_original()
    {
        var email = $"{new string('e', 242)}@example.com";
        Assert.Throws<ArgumentException>(() => Customer.Create(new string('n', 201), email, null));
        Assert.Throws<ArgumentException>(() => Customer.Create("Customer", $"e{email}", null));
        Assert.Throws<ArgumentException>(() => Customer.Create("Customer", email, new string('1', 51)));
        var customer = Customer.Create(new string('n', 200), email, new string('1', 50));
        var updated = customer.Update(" Changed ", "changed@example.com", " +1 555 0100 ");
        Assert.NotSame(customer, updated);
        Assert.Equal(customer.Id, updated.Id);
        Assert.Equal(customer.CreatedAt, updated.CreatedAt);
        Assert.Equal("Changed", updated.Name);
        Assert.Equal("changed@example.com", updated.Email);
        Assert.Equal("+1 555 0100", updated.Phone);
        Assert.Null(updated.Update("Customer", email, " ").Phone);
        Assert.Equal(200, customer.Name.Length);
        Assert.Equal(email, customer.Email);
        Assert.Equal(50, customer.Phone!.Length);
        Assert.Throws<ArgumentException>(() => customer.Update(" ", email, null));
        Assert.Throws<ArgumentException>(() => customer.Update("Customer", "invalid", null));
    }

    [Fact]
    public async Task Repository_preserves_snapshots_and_cannot_resurrect_a_deleted_customer()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = Customer.Create("Customer", "customer@example.com", null);
        await repository.AddAsync(customer, CancellationToken.None);
        var snapshot = await repository.GetAllAsync(CancellationToken.None);
        var updated = customer.Update("Changed", "changed@example.com", "123");
        Assert.True(await repository.UpdateAsync(updated, CancellationToken.None));
        Assert.Same(updated, await repository.GetByIdAsync(customer.Id, CancellationToken.None));
        Assert.Same(customer, Assert.Single(snapshot));
        Assert.True(await repository.DeleteAsync(customer.Id, CancellationToken.None));
        Assert.False(await repository.UpdateAsync(customer, CancellationToken.None));
        Assert.False(await repository.DeleteAsync(customer.Id, CancellationToken.None));
        Assert.Null(await repository.GetByIdAsync(customer.Id, CancellationToken.None));
        Assert.Empty(await repository.GetAllAsync(CancellationToken.None));
    }

    [Fact]
    public async Task Concurrent_creates_do_not_lose_customers_and_results_are_ordered()
    {
        var repository = new InMemoryCustomerRepository();
        await Task.WhenAll(Enumerable.Range(0, 100).Select(index =>
            Task.Run(() => repository.AddAsync(
                Customer.Create($"Customer {index}", $"customer{index}@example.com", null), CancellationToken.None))));
        var customers = await repository.GetAllAsync(CancellationToken.None);
        Assert.Equal(100, customers.Count);
        Assert.Equal(100, customers.Select(customer => customer.Id).Distinct().Count());
        Assert.Equal(customers.OrderBy(customer => customer.CreatedAt).ThenBy(customer => customer.Id), customers);
    }

    [Fact]
    public async Task Cancelled_repository_operations_do_not_write_data()
    {
        var repository = new InMemoryCustomerRepository();
        var customer = Customer.Create("Customer", "customer@example.com", null);
        await repository.AddAsync(customer, CancellationToken.None);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetAllAsync(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.GetByIdAsync(customer.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.AddAsync(
            Customer.Create("Other", "other@example.com", null), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.UpdateAsync(
            customer.Update("Changed", "changed@example.com", null), source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.DeleteAsync(customer.Id, source.Token));
        Assert.Same(customer, Assert.Single(await repository.GetAllAsync(CancellationToken.None)));
    }

    [Fact]
    public async Task Service_forwards_cancellation_for_every_operation()
    {
        var repository = new InMemoryCustomerRepository();
        var service = new CustomerService(repository);
        var customer = await service.CreateAsync("Customer", "customer@example.com", null, CancellationToken.None);
        using var source = new CancellationTokenSource();
        source.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetAllAsync(source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GetByIdAsync(customer.Id, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateAsync("Other", "other@example.com", null, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.UpdateAsync(customer.Id, "Changed", "changed@example.com", null, source.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.DeleteAsync(customer.Id, source.Token));
        Assert.Equal(customer, Assert.Single(await service.GetAllAsync(CancellationToken.None)));
    }
}
