using System.ComponentModel.DataAnnotations;

namespace SmartTask.Domain;

public sealed class Customer
{
    public const int MaxNameLength = 200;
    public const int MaxEmailLength = 254;
    public const int MaxPhoneLength = 50;

    public Guid Id { get; }
    public string Name { get; }
    public string Email { get; }
    public string? Phone { get; }
    public DateTimeOffset CreatedAt { get; }

    private Customer(Guid id, string name, string email, string? phone, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        if (name.Length > MaxNameLength)
            throw new ArgumentException($"Name cannot exceed {MaxNameLength} characters.", nameof(name));
        if (email.Length > MaxEmailLength || !new EmailAddressAttribute().IsValid(email))
            throw new ArgumentException($"Email must be a valid address of at most {MaxEmailLength} characters.", nameof(email));
        if (phone?.Length > MaxPhoneLength)
            throw new ArgumentException($"Phone cannot exceed {MaxPhoneLength} characters.", nameof(phone));

        Id = id;
        Name = name.Trim();
        Email = email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        CreatedAt = createdAt;
    }

    public static Customer Create(string name, string email, string? phone) =>
        new(Guid.NewGuid(), name, email, phone, DateTimeOffset.UtcNow);

    public Customer Update(string name, string email, string? phone) =>
        new(Id, name, email, phone, CreatedAt);
}
