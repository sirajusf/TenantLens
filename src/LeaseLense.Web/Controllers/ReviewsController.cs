using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Reviews;
using LeaseLense.Web.Models.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace LeaseLense.Web.Controllers;

public sealed class ReviewsController : Controller
{
    private readonly IReviewMvpService _reviewMvpService;
    private readonly ILogger<ReviewsController> _logger;

    public ReviewsController(IReviewMvpService reviewMvpService, ILogger<ReviewsController> logger)
    {
        _reviewMvpService = reviewMvpService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? propertyQuery,
        string? city,
        decimal? minRent,
        decimal? maxRent,
        decimal? minRating,
        decimal? minCommunicationRating,
        decimal? minMaintenanceRating,
        bool verifiedOnly,
        [FromQuery] List<string>? issueTypes,
        string? sortBy,
        CancellationToken cancellationToken)
    {
        var query = new ReviewQueryDto
        {
            PropertyQuery = propertyQuery,
            City = city,
            MinRent = minRent,
            MaxRent = maxRent,
            MinRating = minRating,
            MinCommunicationRating = minCommunicationRating,
            MinMaintenanceRating = minMaintenanceRating,
            VerifiedOnly = verifiedOnly,
            IssueTypes = issueTypes is { Count: > 0 } ? issueTypes : null,
            SortBy = sortBy
        };

        var sortKey = string.IsNullOrWhiteSpace(sortBy) ? "newest" : sortBy;

        ReviewListPageDto page;
        try
        {
            page = await _reviewMvpService.GetReviewsAsync(query, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load reviews. PropertyQuery: {PropertyQuery}, City: {City}, SortBy: {SortBy}", propertyQuery, city, sortKey);
            var errorModel = new ReviewListViewModel
            {
                PropertyQuery = propertyQuery,
                City = city,
                MinRent = minRent,
                MaxRent = maxRent,
                MinRating = minRating,
                MinCommunicationRating = minCommunicationRating,
                MinMaintenanceRating = minMaintenanceRating,
                VerifiedOnly = verifiedOnly,
                IssueTypes = issueTypes,
                SortBy = sortKey,
                HasLoadError = true,
                LoadErrorMessage = "We couldn't load reviews right now. Please try again in a moment.",
                Reviews = []
            };
            return View(errorModel);
        }

        var s = page.Summary;
        var model = new ReviewListViewModel
        {
            PropertyQuery = propertyQuery,
            City = city,
            MinRent = minRent,
            MaxRent = maxRent,
            MinRating = minRating,
            MinCommunicationRating = minCommunicationRating,
            MinMaintenanceRating = minMaintenanceRating,
            VerifiedOnly = verifiedOnly,
            IssueTypes = issueTypes,
            SortBy = sortKey,
            Summary = new ReviewSummaryViewModel
            {
                TotalMatching = s.TotalMatching,
                AverageRating = s.AverageRating,
                VerifiedStaysCount = s.VerifiedStaysCount,
                VerifiedStaysPercent = s.VerifiedStaysPercent
            },
            Reviews = page.Items.Select(x => new ReviewListItemViewModel
            {
                PropertyId = x.PropertyId,
                PropertyTitle = x.PropertyTitle,
                StreetAddress = x.StreetAddress,
                CommunityName = x.CommunityName,
                City = x.City,
                MonthlyRent = x.MonthlyRent,
                AverageRating = x.AverageRating,
                IsVerifiedStay = x.IsVerifiedStay,
                VerificationBadge = x.VerificationBadge,
                AnonymizedReviewer = x.AnonymizedReviewer,
                ReviewText = x.ReviewText
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Create(Guid? propertyId, CancellationToken cancellationToken)
    {
        var reporterEmail = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reporterEmail))
        {
            return Challenge();
        }
        var metadata = await _reviewMvpService.GetCreateMetadataAsync(reporterEmail, cancellationToken);
        return View(ToCreatePageModel(ApplyPreferredProperty(new CreateReviewViewModel(), metadata, propertyId), metadata));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateReviewViewModel form, CancellationToken cancellationToken)
    {
        var reporterEmail = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reporterEmail))
        {
            return Challenge();
        }
        var metadata = await _reviewMvpService.GetCreateMetadataAsync(reporterEmail, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(ToCreatePageModel(form, metadata));
        }

        var request = new CreateReviewDto
        {
            PropertyId = form.PropertyNotListed ? null : form.PropertyId,
            NewCommunityName = form.NewCommunityName,
            NewPropertyTitle = form.NewPropertyTitle,
            NewPropertyStreetAddress = form.NewPropertyStreetAddress,
            NewPropertyCity = form.NewPropertyCity,
            NewPropertyCountry = form.NewPropertyCountry,
            MonthlyRent = form.MonthlyRent,
            UnitType = form.UnitType,
            ReviewText = form.ReviewText,
            OverallRating = form.OverallRating,
            ReporterEmail = reporterEmail
        };

        try
        {
            await _reviewMvpService.SubmitReviewAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Review submission rejected for {ReporterEmail}.", reporterEmail);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(ToCreatePageModel(form, metadata));
        }
        TempData["ReviewSubmitSuccess"] = "Review submitted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private static ReviewCreatePageViewModel ToCreatePageModel(CreateReviewViewModel form, ReviewCreateMetadataDto metadata)
    {
        return new ReviewCreatePageViewModel
        {
            Form = form,
            CanSubmit = metadata.CanSubmit,
            RestrictionMessage = metadata.RestrictionMessage,
            Properties = metadata.Properties
                .Select(x => new ReviewPropertyOptionViewModel
                {
                    PropertyId = x.PropertyId,
                    Label = x.Label
                })
                .ToList()
        };
    }

    private static CreateReviewViewModel ApplyPreferredProperty(
        CreateReviewViewModel form,
        ReviewCreateMetadataDto metadata,
        Guid? preferredPropertyId)
    {
        if (!preferredPropertyId.HasValue)
        {
            return form;
        }

        var hasProperty = metadata.Properties.Any(x => x.PropertyId == preferredPropertyId.Value);
        if (!hasProperty)
        {
            return form;
        }

        return new CreateReviewViewModel
        {
            PropertyId = preferredPropertyId.Value,
            PropertyNotListed = false
        };
    }
}
