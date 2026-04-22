using LeaseLense.Application.Profile;

namespace LeaseLense.Application.Abstractions;

public interface IProfileService
{
    Task<UserProfileDto> GetProfileAsync(string email, CancellationToken cancellationToken = default);
    Task UpdateProfileAsync(UpdateUserProfileDto request, CancellationToken cancellationToken = default);
    Task<ResidencyVerificationDecisionDto> SubmitResidencyVerificationAsync(SubmitResidencyVerificationDto request, CancellationToken cancellationToken = default);
}
