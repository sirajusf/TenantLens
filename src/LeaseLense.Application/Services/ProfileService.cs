using System.Text.RegularExpressions;
using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Application.Profile;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class ProfileService : IProfileService
{
    private static readonly Regex NonAlphaNumericRegex = new("[^A-Z0-9]", RegexOptions.Compiled);
    private readonly ILeaseLensDbContext _dbContext;

    public ProfileService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<UserProfileDto> GetProfileAsync(string email, CancellationToken cancellationToken = default)
    {
        var renter = await _dbContext.GetRenterByEmailAsync(email.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("Authenticated renter profile was not found.");

        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var verifications = await _dbContext.GetRenterPropertyVerificationsAsync(cancellationToken);
        var documents = await _dbContext.GetResidencyVerificationDocumentsAsync(cancellationToken);
        var nameLocked = verifications.Any(x =>
            x.RenterId == renter.RenterId
            && string.Equals(x.Status, "verified_stay", StringComparison.OrdinalIgnoreCase));

        var propertyById = properties.ToDictionary(x => x.PropertyId, x => x);

        var documentLookup = documents
            .GroupBy(x => x.RenterPropertyVerificationId)
            .ToDictionary(
                x => x.Key,
                x => x.OrderByDescending(d => d.UploadedAt).First());

        return new UserProfileDto
        {
            Email = renter.Email,
            DisplayName = renter.DisplayName ?? string.Empty,
            EmailVerified = renter.EmailVerified,
            StreetAddress = renter.StreetAddress,
            City = renter.City,
            StateOrRegion = renter.StateOrRegion,
            PostalCode = renter.PostalCode,
            Country = renter.Country,
            NameLocked = nameLocked,
            HasVerifiedStay = nameLocked,
            Verifications = verifications
                .Where(x => x.RenterId == renter.RenterId)
                .OrderByDescending(x => x.UpdatedAt)
                .Select(x =>
                {
                    var doc = documentLookup.GetValueOrDefault(x.RenterPropertyVerificationId);
                    var extractedName = doc?.ExtractedName;
                    var extractedAddress = doc?.ExtractedAddress;
                    var property = propertyById.GetValueOrDefault(x.PropertyId);
                    var providedAddress = property is null
                        ? string.Empty
                        : ResidencyVerificationDisplayHelper.FormatPropertyAddress(
                            property.StreetAddress,
                            property.City,
                            property.StateOrRegion,
                            property.PostalCode,
                            property.Country);

                    return new UserVerificationStatusDto
                    {
                        VerificationId = x.RenterPropertyVerificationId,
                        PropertyId = x.PropertyId,
                        PropertyTitle = property?.Title ?? "Unknown Property",
                        Status = x.Status,
                        ConfidenceScore = x.ConfidenceScore,
                        ReviewReason = x.ReviewReason,
                        ExtractedName = extractedName,
                        ExtractedAddress = extractedAddress,
                        ProvidedName = renter.DisplayName ?? string.Empty,
                        ProvidedAddressSummary = providedAddress,
                        CustomerSummary = ResidencyVerificationDisplayHelper.BuildCustomerSummary(
                            x.Status,
                            extractedName,
                            extractedAddress),
                        UpdatedAt = x.UpdatedAt
                    };
                })
                .ToList()
        };
    }

    public async Task UpdateProfileAsync(UpdateUserProfileDto request, CancellationToken cancellationToken = default)
    {
        var renter = await _dbContext.GetRenterByEmailAsync(request.Email.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("Authenticated renter profile was not found.");
        var verifications = await _dbContext.GetRenterPropertyVerificationsAsync(cancellationToken);
        var nameLocked = verifications.Any(x =>
            x.RenterId == renter.RenterId
            && string.Equals(x.Status, "verified_stay", StringComparison.OrdinalIgnoreCase));

        if (!nameLocked)
        {
            renter.DisplayName = request.DisplayName.Trim();
        }
        else if (!string.Equals(renter.DisplayName?.Trim(), request.DisplayName.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Name cannot be edited after Verified Stay.");
        }
        renter.StreetAddress = Normalize(request.StreetAddress);
        renter.City = Normalize(request.City);
        renter.StateOrRegion = Normalize(request.StateOrRegion);
        renter.PostalCode = Normalize(request.PostalCode);
        renter.Country = Normalize(request.Country);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<ResidencyVerificationDecisionDto> SubmitResidencyVerificationAsync(SubmitResidencyVerificationDto request, CancellationToken cancellationToken = default)
    {
        var renter = await _dbContext.GetRenterByEmailAsync(request.Email.Trim(), cancellationToken)
            ?? throw new InvalidOperationException("Authenticated renter profile was not found.");
        if (!renter.EmailVerified)
        {
            throw new InvalidOperationException("Please verify your email before requesting address verification.");
        }
        var targetStreetAddress = Normalize(renter.StreetAddress);
        var targetCity = Normalize(renter.City);
        var targetStateOrRegion = Normalize(renter.StateOrRegion);
        var targetPostalCode = Normalize(renter.PostalCode);
        var targetCountry = Normalize(renter.Country);

        if (string.IsNullOrWhiteSpace(targetStreetAddress) || string.IsNullOrWhiteSpace(targetCity) || string.IsNullOrWhiteSpace(targetCountry))
        {
            throw new InvalidOperationException("Please provide a complete property address before submitting verification.");
        }
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var property = properties.FirstOrDefault(x =>
            string.Equals(x.StreetAddress, targetStreetAddress, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.City, targetCity, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Country, targetCountry, StringComparison.OrdinalIgnoreCase));
        if (property is null)
        {
            if (string.IsNullOrWhiteSpace(request.PropertyTitle) || string.IsNullOrWhiteSpace(request.CommunityName))
            {
                throw new InvalidOperationException("Please provide a property/community name for unlisted addresses.");
            }
            var communities = await _dbContext.GetCommunitiesAsync(cancellationToken);
            var community = communities.FirstOrDefault(x =>
                string.Equals(x.Name, request.CommunityName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (community is null)
            {
                community = new Community
                {
                    CommunityId = Guid.NewGuid(),
                    Name = request.CommunityName.Trim(),
                    City = targetCity,
                    StateOrRegion = targetStateOrRegion,
                    Country = targetCountry,
                    CreatedAt = DateTime.UtcNow
                };
                await _dbContext.AddCommunityAsync(community, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            property = new Property
            {
                PropertyId = Guid.NewGuid(),
                CommunityId = community.CommunityId,
                Title = request.PropertyTitle.Trim(),
                StreetAddress = targetStreetAddress.Trim(),
                City = targetCity.Trim(),
                Country = targetCountry.Trim(),
                StateOrRegion = targetStateOrRegion,
                PostalCode = targetPostalCode,
                CreatedByRenterId = renter.RenterId,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.AddPropertyAsync(property, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        var existingDocs = await _dbContext.GetResidencyVerificationDocumentsAsync(cancellationToken);
        var duplicateHash = existingDocs.Any(x => x.FileHashSha256 == request.FileHashSha256);

        var nameScore = ComputeNameScore(renter.DisplayName, request.ExtractedName);
        var nameMatched = nameScore > 0m;
        var extractedAddressNormalized = NormalizeForAddressMatch(request.ExtractedAddress);
        var propertyAddressNormalized = NormalizeForAddressMatch(property.StreetAddress);
        var extractedAddressNoUnit = RemoveUnitSegment(extractedAddressNormalized);
        var propertyAddressNoUnit = RemoveUnitSegment(propertyAddressNormalized);
        var fullUnitAddressMatched = !string.IsNullOrWhiteSpace(extractedAddressNormalized)
            && !string.IsNullOrWhiteSpace(propertyAddressNormalized)
            && extractedAddressNormalized.Contains(propertyAddressNormalized, StringComparison.OrdinalIgnoreCase);
        var buildingOnlyAddressMatched = !fullUnitAddressMatched
            && !string.IsNullOrWhiteSpace(extractedAddressNoUnit)
            && !string.IsNullOrWhiteSpace(propertyAddressNoUnit)
            && extractedAddressNoUnit.Contains(propertyAddressNoUnit, StringComparison.OrdinalIgnoreCase);
        var reverseAddressMatched = !fullUnitAddressMatched
            && !buildingOnlyAddressMatched
            && !string.IsNullOrWhiteSpace(extractedAddressNoUnit)
            && !string.IsNullOrWhiteSpace(propertyAddressNoUnit)
            && propertyAddressNoUnit.Contains(extractedAddressNoUnit, StringComparison.OrdinalIgnoreCase);
        var addressMatched = fullUnitAddressMatched || buildingOnlyAddressMatched || reverseAddressMatched;

        var confidence = 0m;
        confidence += nameScore;
        if (addressMatched) confidence += 45m;
        if (request.ExtractedFromDate.HasValue || request.ExtractedToDate.HasValue) confidence += 15m;
        if (duplicateHash) confidence -= 10m;
        if (request.ParserConfidence < 0.35m) confidence -= 15m;
        else if (request.ParserConfidence >= 0.75m) confidence += 10m;
        confidence = Math.Clamp(confidence, 0m, 100m);

        var hasExtraction = !string.IsNullOrWhiteSpace(request.ExtractedName) || !string.IsNullOrWhiteSpace(request.ExtractedAddress);
        var status = !hasExtraction ? "pending_manual_review"
            : !nameMatched ? "pending_manual_review"
            : (nameMatched && addressMatched && confidence < 45m) ? "pending_manual_review"
            : confidence >= 75m ? "verified_stay"
            : confidence >= 45m ? "pending_manual_review"
            : "rejected";

        var reason = status switch
        {
            "verified_stay" => "Auto-checks matched name/address with high confidence.",
            "pending_manual_review" when !hasExtraction => "Document text extraction was inconclusive; queued for manual review.",
            "pending_manual_review" => "Auto-checks were inconclusive; queued for manual review.",
            _ => duplicateHash
                ? "Rejected because this document hash was already used."
                : "Rejected due to low confidence on name/address match."
        };
        var addressMatchMode = fullUnitAddressMatched
            ? "full building and unit match"
            : buildingOnlyAddressMatched
                ? "building-only match (unit fallback)"
                : reverseAddressMatched
                    ? "building-only reverse match (unit fallback)"
                : "no address match";
        reason = $"{reason} Name match: {nameMatched} (score: {nameScore}). Address match: {addressMatched} ({addressMatchMode}). Date evidence: {(request.ExtractedFromDate.HasValue || request.ExtractedToDate.HasValue)}. Duplicate document: {duplicateHash}.";

        var verification = new RenterPropertyVerification
        {
            RenterPropertyVerificationId = Guid.NewGuid(),
            RenterId = renter.RenterId,
            PropertyId = property.PropertyId,
            Status = status,
            ConfidenceScore = confidence,
            VerifiedFrom = request.ExtractedFromDate,
            VerifiedTo = request.ExtractedToDate,
            ReviewReason = reason,
            VerifiedAt = status == "verified_stay" ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var document = new ResidencyVerificationDocument
        {
            ResidencyVerificationDocumentId = Guid.NewGuid(),
            RenterPropertyVerificationId = verification.RenterPropertyVerificationId,
            DocumentType = request.DocumentType.Trim(),
            FileName = request.FileName,
            ContentType = request.ContentType,
            FileSizeBytes = request.FileSizeBytes,
            FileHashSha256 = request.FileHashSha256,
            ExtractedName = request.ExtractedName.Trim(),
            ExtractedAddress = request.ExtractedAddress.Trim(),
            ExtractedFromDate = request.ExtractedFromDate,
            ExtractedToDate = request.ExtractedToDate,
            ParserConfidence = request.ParserConfidence,
            ProcessingStatus = "processed",
            UploadedAt = DateTime.UtcNow
        };

        await _dbContext.AddRenterPropertyVerificationAsync(verification, cancellationToken);
        await _dbContext.AddResidencyVerificationDocumentAsync(document, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return new ResidencyVerificationDecisionDto
        {
            Status = status,
            Reason = reason
        };
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string NormalizeForAddressMatch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value
            .ToUpperInvariant()
            .Replace(".", " ")
            .Replace(",", " ")
            .Replace("#", " APT ");

        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string RemoveUnitSegment(string address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return string.Empty;
        }

        var markers = new[] { " APT ", " APARTMENT ", " UNIT ", " STE ", " SUITE ", " FL " };
        foreach (var marker in markers)
        {
            var index = address.IndexOf(marker, StringComparison.Ordinal);
            if (index >= 0)
            {
                return address[..index].Trim();
            }
        }

        return address.Trim();
    }

    private static decimal ComputeNameScore(string? expectedName, string? extractedName)
    {
        if (string.IsNullOrWhiteSpace(expectedName) || string.IsNullOrWhiteSpace(extractedName))
        {
            return 0m;
        }

        var normalizedExpected = NormalizeForNameMatch(expectedName);
        var normalizedExtracted = NormalizeForNameMatch(extractedName);
        if (string.IsNullOrWhiteSpace(normalizedExpected) || string.IsNullOrWhiteSpace(normalizedExtracted))
        {
            return 0m;
        }

        if (normalizedExtracted.Contains(normalizedExpected, StringComparison.OrdinalIgnoreCase)
            || normalizedExpected.Contains(normalizedExtracted, StringComparison.OrdinalIgnoreCase))
        {
            return 40m;
        }

        var expectedParts = normalizedExpected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var extractedParts = normalizedExtracted.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (expectedParts.Length == 0 || extractedParts.Length == 0)
        {
            return 0m;
        }

        var matchedTokens = 0;
        foreach (var expectedPart in expectedParts)
        {
            if (extractedParts.Any(extractedPart => ComputeLevenshteinDistance(expectedPart, extractedPart) <= 4))
            {
                matchedTokens++;
            }
        }

        var coverage = (decimal)matchedTokens / expectedParts.Length;
        if (coverage >= 1m)
        {
            return 40m;
        }

        if (coverage >= 0.67m)
        {
            return 28m;
        }

        if (coverage >= 0.34m)
        {
            return 20m;
        }

        return 0m;
    }

    private static string NormalizeForNameMatch(string value)
    {
        var upper = value.ToUpperInvariant();
        var cleaned = NonAlphaNumericRegex.Replace(upper, " ");
        return string.Join(" ", cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static int ComputeLevenshteinDistance(string left, string right)
    {
        if (left.Length == 0) return right.Length;
        if (right.Length == 0) return left.Length;

        var dp = new int[left.Length + 1, right.Length + 1];
        for (var i = 0; i <= left.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= right.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= left.Length; i++)
        {
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(
                    Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1),
                    dp[i - 1, j - 1] + cost);
            }
        }

        return dp[left.Length, right.Length];
    }

}
