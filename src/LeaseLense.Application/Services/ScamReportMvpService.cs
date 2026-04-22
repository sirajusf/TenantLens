using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Application.Common;
using LeaseLense.Application.ScamReports;
using LeaseLense.Domain.Entities;

namespace LeaseLense.Application.Services;

public sealed class ScamReportMvpService : IScamReportMvpService
{
    private readonly ILeaseLensDbContext _dbContext;

    public ScamReportMvpService(ILeaseLensDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ScamReportListItemDto>> GetScamReportsAsync(CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var scams = await _dbContext.GetScamReportsAsync(cancellationToken);
        var verifications = await _dbContext.GetRenterPropertyVerificationsAsync(cancellationToken);
        var propertyLookup = properties.ToDictionary(x => x.PropertyId);

        return scams
            .OrderByDescending(x => x.DateReported)
            .Select(x =>
            {
                propertyLookup.TryGetValue(x.PropertyId, out var property);
                var hasVerifiedStay = verifications.Any(v =>
                    v.RenterId == x.RenterId
                    && v.PropertyId == x.PropertyId
                    && string.Equals(v.Status, "verified_stay", StringComparison.OrdinalIgnoreCase));
                return new ScamReportListItemDto
                {
                    ScamReportId = x.ScamReportId,
                    PropertyId = x.PropertyId,
                    PropertyTitle = property?.Title ?? "Unknown Property",
                    City = property?.City ?? "Unknown City",
                    ScamType = DisplayTextFormatter.ToTitleLabel(x.ScamType),
                    SeverityScore = x.SeverityScore,
                    DateReported = x.DateReported,
                    Description = string.IsNullOrWhiteSpace(x.Description) ? "No details provided." : x.Description!,
                    VerificationBadge = hasVerifiedStay ? "Verified Stay" : "Unverified Stay"
                };
            })
            .Take(100)
            .ToList();
    }

    public async Task<ScamReportFormMetadataDto> GetFormMetadataAsync(string reporterEmail, CancellationToken cancellationToken = default)
    {
        var properties = await _dbContext.GetPropertiesAsync(cancellationToken);
        var canSubmit = false;
        var restrictionMessage = "You can submit for any property. Verified Stay applies only to your matched verified residency.";
        if (!string.IsNullOrWhiteSpace(reporterEmail))
        {
            var renter = await _dbContext.GetRenterByEmailAsync(reporterEmail.Trim(), cancellationToken);
            if (renter is not null)
            {
                canSubmit = true;
            }
        }

        return new ScamReportFormMetadataDto
        {
            CanSubmit = canSubmit,
            RestrictionMessage = restrictionMessage,
            Properties = properties
                .OrderBy(x => x.Title)
                .Select(x => new ScamReportOptionDto
                {
                    Id = x.PropertyId,
                    Label = $"{x.Title} - {x.StreetAddress}, {x.City}"
                })
                .ToList()
        };
    }

    public async Task SubmitScamReportAsync(CreateScamReportDto request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReporterEmail))
        {
            throw new InvalidOperationException("Reporter email is required.");
        }

        var renter = await _dbContext.GetRenterByEmailAsync(request.ReporterEmail.Trim(), cancellationToken);
        if (renter is null)
        {
            throw new InvalidOperationException("Authenticated renter profile was not found.");
        }

        var propertyId = request.PropertyId;
        if (!propertyId.HasValue)
        {
            propertyId = await CreateOrResolvePropertyAsync(renter.RenterId, request, cancellationToken);
        }

        var report = new ScamReport
        {
            ScamReportId = Guid.NewGuid(),
            PropertyId = propertyId.Value,
            RenterId = renter.RenterId,
            ScamType = request.ScamType.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            VerificationStatus = "unverified",
            SeverityScore = request.SeverityScore,
            DateReported = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.AddScamReportAsync(report, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<Guid> CreateOrResolvePropertyAsync(
        Guid renterId,
        CreateScamReportDto request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.NewPropertyTitle)
            || string.IsNullOrWhiteSpace(request.NewCommunityName)
            || string.IsNullOrWhiteSpace(request.NewPropertyStreetAddress)
            || string.IsNullOrWhiteSpace(request.NewPropertyCity)
            || string.IsNullOrWhiteSpace(request.NewPropertyCountry))
        {
            throw new InvalidOperationException("Property details are required for unlisted property submission.");
        }

        var title = request.NewPropertyTitle.Trim();
        var communityName = request.NewCommunityName.Trim();
        var street = request.NewPropertyStreetAddress.Trim();
        var city = request.NewPropertyCity.Trim();
        var country = request.NewPropertyCountry.Trim();
        var communities = await _dbContext.GetCommunitiesAsync(cancellationToken);
        var community = communities.FirstOrDefault(x =>
            string.Equals(x.Name, communityName, StringComparison.OrdinalIgnoreCase));
        if (community is null)
        {
            community = new Community
            {
                CommunityId = Guid.NewGuid(),
                Name = communityName,
                City = city,
                Country = country,
                CreatedAt = DateTime.UtcNow
            };
            await _dbContext.AddCommunityAsync(community, cancellationToken);
        }

        var existingProperty = (await _dbContext.GetPropertiesAsync(cancellationToken))
            .FirstOrDefault(x =>
                string.Equals(x.StreetAddress, street, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.City, city, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Country, country, StringComparison.OrdinalIgnoreCase));

        if (existingProperty is not null)
        {
            return existingProperty.PropertyId;
        }

        var property = new Property
        {
            PropertyId = Guid.NewGuid(),
            CommunityId = community.CommunityId,
            Title = title,
            StreetAddress = street,
            City = city,
            Country = country,
            CreatedByRenterId = renterId,
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.AddPropertyAsync(property, cancellationToken);
        return property.PropertyId;
    }
}
