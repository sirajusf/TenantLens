namespace LeaseLense.Application.Abstractions;

public interface IUserAccountService
{
    Task<Guid> EnsureRenterForEmailAsync(string email, string? displayName, bool emailVerified, CancellationToken cancellationToken = default);
    Task<Guid?> GetRenterIdByEmailAsync(string email, CancellationToken cancellationToken = default);
}
