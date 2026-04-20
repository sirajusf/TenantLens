using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class UserAccountService : IUserAccountService
{
    private readonly ILeaseLensDbContext _dbContext;

    public UserAccountService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid> EnsureRenterForEmailAsync(string email, string? displayName, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim();
        var existing = await _dbContext.GetRenterByEmailAsync(normalizedEmail, cancellationToken);
        if (existing is not null)
        {
            return existing.RenterId;
        }

        var renter = new Renter
        {
            RenterId = Guid.NewGuid(),
            Email = normalizedEmail,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim(),
            IsVerified = true,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.AddRenterAsync(renter, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return renter.RenterId;
    }

    public async Task<Guid?> GetRenterIdByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var renter = await _dbContext.GetRenterByEmailAsync(email.Trim(), cancellationToken);
        return renter?.RenterId;
    }
}
