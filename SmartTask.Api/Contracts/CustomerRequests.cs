using System.ComponentModel.DataAnnotations;
using SmartTask.Domain;

namespace SmartTask.Api.Contracts;

/// <summary>Creates a customer with a required name and email and optional phone.</summary>
public sealed record CreateCustomerRequest
{
    [Required, StringLength(Customer.MaxNameLength)]
    public required string Name { get; init; }

    [Required, EmailAddress, StringLength(Customer.MaxEmailLength)]
    public required string Email { get; init; }

    [StringLength(Customer.MaxPhoneLength)]
    public string? Phone { get; init; }
}

/// <summary>Replaces customer contact details while preserving ID and creation time.</summary>
public sealed record UpdateCustomerRequest
{
    [Required, StringLength(Customer.MaxNameLength)]
    public required string Name { get; init; }

    [Required, EmailAddress, StringLength(Customer.MaxEmailLength)]
    public required string Email { get; init; }

    [StringLength(Customer.MaxPhoneLength)]
    public string? Phone { get; init; }
}
