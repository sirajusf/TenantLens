using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Properties;
using LeaseLense.Application.Search;
using LeaseLense.Web.Models.Properties;
using LeaseLense.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaseLense.Web.Controllers;

public sealed class PropertiesController : Controller
{
    private readonly IPropertyDirectoryService _propertyDirectoryService;
    private readonly SmartSearchOrchestrator _smartSearch;

    public PropertiesController(
        IPropertyDirectoryService propertyDirectoryService,
        SmartSearchOrchestrator smartSearch)
    {
        _propertyDirectoryService = propertyDirectoryService;
        _smartSearch = smartSearch;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        string? city,
        string? propertyType,
        string? landlordName,
        decimal? minRating,
        bool hasVerifiedReviews,
        string? sortBy,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> interpretedFilters = [];
        bool llmUnavailable = false;
        bool nlFallback = false;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var smart = await _smartSearch.SearchAsync(q, SearchEntityType.Property, 120, cancellationToken);
            var effective = smart.EffectiveQuery;

            q = effective.QueryText ?? q;
            city ??= effective.City;
            propertyType ??= effective.PropertyType;
            landlordName ??= effective.LandlordName;
            minRating ??= effective.MinRating;
            hasVerifiedReviews = hasVerifiedReviews || effective.HasVerifiedReviews;
            sortBy ??= effective.SortBy;

            interpretedFilters = smart.InterpretedFilters;
            llmUnavailable = smart.LlmUnavailable;
            nlFallback = smart.NlFallback;
        }

        var result = await _propertyDirectoryService.SearchAsync(
            new PropertyDirectoryQueryDto
            {
                QueryText = q,
                City = city,
                PropertyType = propertyType,
                LandlordName = landlordName,
                MinRating = minRating,
                HasVerifiedReviews = hasVerifiedReviews,
                SortBy = sortBy,
                Limit = 120
            },
            cancellationToken);

        var model = new PropertyDirectoryPageViewModel
        {
            QueryText = result.QueryText,
            City = city,
            PropertyType = propertyType,
            LandlordName = landlordName,
            MinRating = minRating,
            HasVerifiedReviews = hasVerifiedReviews,
            SortBy = sortBy,
            InterpretedFilters = interpretedFilters,
            LlmUnavailable = llmUnavailable,
            NlFallback = nlFallback,
            Properties = result.Items.Select(x => new PropertyDirectoryItemViewModel
            {
                PropertyId = x.PropertyId,
                Title = x.Title,
                CommunityName = x.CommunityName,
                StreetAddress = x.StreetAddress,
                City = x.City,
                Country = x.Country,
                AverageRating = x.AverageRating,
                AverageScamSeverity = x.AverageScamSeverity
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var profile = await _propertyDirectoryService.GetProfileAsync(id, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }

        var model = new PropertyProfileViewModel
        {
            PropertyId = profile.PropertyId,
            Title = profile.Title,
            CommunityName = profile.CommunityName,
            StreetAddress = profile.StreetAddress,
            City = profile.City,
            Country = profile.Country,
            LandlordName = profile.LandlordName,
            ManagementCompanyName = profile.ManagementCompanyName,
            AverageRating = profile.AverageRating,
            ReviewCount = profile.ReviewCount,
            AverageScamSeverity = profile.AverageScamSeverity,
            Reviews = profile.Reviews.Select(x => new PropertyProfileReviewViewModel
            {
                ReviewerAlias = x.ReviewerAlias,
                MonthlyRent = x.MonthlyRent,
                AverageRating = x.AverageRating,
                VerificationBadge = x.VerificationBadge,
                ReviewText = x.ReviewText,
                CreatedAt = x.CreatedAt
            }).ToList(),
            ScamReports = profile.ScamReports.Select(x => new PropertyProfileScamReportViewModel
            {
                ScamType = x.ScamType,
                SeverityScore = x.SeverityScore,
                Description = x.Description,
                VerificationBadge = x.VerificationBadge,
                DateReported = x.DateReported
            }).ToList()
        };

        return View(model);
    }
}
