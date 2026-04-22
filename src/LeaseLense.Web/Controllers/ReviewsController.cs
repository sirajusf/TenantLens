using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Reviews;
using LeaseLense.Web.Models.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaseLense.Web.Controllers;

public sealed class ReviewsController : Controller
{
    private readonly IReviewMvpService _reviewMvpService;

    public ReviewsController(IReviewMvpService reviewMvpService)
    {
        _reviewMvpService = reviewMvpService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? propertyQuery,
        string? city,
        decimal? minRent,
        decimal? maxRent,
        decimal? minRating,
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
            SortBy = sortBy
        };

        var reviews = await _reviewMvpService.GetReviewsAsync(query, cancellationToken);

        var model = new ReviewListViewModel
        {
            PropertyQuery = propertyQuery,
            City = city,
            MinRent = minRent,
            MaxRent = maxRent,
            MinRating = minRating,
            SortBy = string.IsNullOrWhiteSpace(sortBy) ? "newest" : sortBy,
            Reviews = reviews.Select(x => new ReviewListItemViewModel
            {
                PropertyId = x.PropertyId,
                PropertyTitle = x.PropertyTitle,
                StreetAddress = x.StreetAddress,
                City = x.City,
                MonthlyRent = x.MonthlyRent,
                AverageRating = x.AverageRating,
                VerificationBadge = x.VerificationBadge,
                AnonymizedReviewer = x.AnonymizedReviewer,
                ReviewText = x.ReviewText
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var reporterEmail = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reporterEmail))
        {
            return Challenge();
        }
        var metadata = await _reviewMvpService.GetCreateMetadataAsync(reporterEmail, cancellationToken);
        return View(ToCreatePageModel(new CreateReviewViewModel(), metadata));
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
}
