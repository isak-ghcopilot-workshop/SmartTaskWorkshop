using Microsoft.AspNetCore.Http.HttpResults;
using SmartTask.Api.Contracts;
using SmartTask.Application;

namespace SmartTask.Api.Endpoints;

public static class CustomerEndpoints
{
    public static WebApplication MapCustomers(this WebApplication app)
    {
        var group = app.MapGroup("/api/customers").WithTags("Customers");

        group.MapGet("/", async (ICustomerService service, CancellationToken cancellationToken) =>
            TypedResults.Ok(await service.GetAllAsync(cancellationToken)))
            .WithName("GetCustomers")
            .WithSummary("List all customers")
            .WithDescription("Returns customers ordered by creation time, then ID.");

        group.MapGet("/{id:guid}", async Task<Results<Ok<CustomerResponse>, NotFound>> (
            Guid id, ICustomerService service, CancellationToken cancellationToken) =>
        {
            var customer = await service.GetByIdAsync(id, cancellationToken);
            return customer is null ? TypedResults.NotFound() : TypedResults.Ok(customer);
        })
            .WithName("GetCustomerById")
            .WithSummary("Get a customer")
            .WithDescription("Returns a customer by ID, or 404 when it does not exist.");

        group.MapPost("/", async (
            CreateCustomerRequest request, ICustomerService service, CancellationToken cancellationToken) =>
        {
            var customer = await service.CreateAsync(request.Name, request.Email, request.Phone, cancellationToken);
            return TypedResults.Created($"/api/customers/{customer.Id}", customer);
        })
            .WithName("CreateCustomer")
            .WithSummary("Create a customer")
            .WithDescription("Creates a customer and returns its resource URL.")
            .ProducesValidationProblem();

        group.MapPut("/{id:guid}", async Task<Results<Ok<CustomerResponse>, NotFound>> (
            Guid id, UpdateCustomerRequest request, ICustomerService service, CancellationToken cancellationToken) =>
        {
            var customer = await service.UpdateAsync(
                id, request.Name, request.Email, request.Phone, cancellationToken);
            return customer is null ? TypedResults.NotFound() : TypedResults.Ok(customer);
        })
            .WithName("UpdateCustomer")
            .WithSummary("Update a customer")
            .WithDescription("Replaces name, email, and phone without changing the ID or creation time. Omitted phone is cleared.")
            .ProducesValidationProblem();

        group.MapDelete("/{id:guid}", async Task<Results<NoContent, NotFound>> (
            Guid id, ICustomerService service, CancellationToken cancellationToken) =>
            await service.DeleteAsync(id, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound())
            .WithName("DeleteCustomer")
            .WithSummary("Delete a customer")
            .WithDescription("Removes a customer, or returns 404 when it does not exist.");

        return app;
    }
}
