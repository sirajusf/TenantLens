namespace LeaseLense.Application.Abstractions;

public interface IUserAccountService
{
    Task<Guid> EnsureRenterForEmailAsync(string email, string? displayName, CancellationToken cancellationToken = default);
    Task<Guid?> GetRenterIdByEmailAsync(string email, CancellationToken cancellationToken = default);
}
