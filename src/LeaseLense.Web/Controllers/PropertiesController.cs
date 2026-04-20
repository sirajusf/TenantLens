using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Properties;
using LeaseLense.Web.Models.Properties;
using Microsoft.AspNetCore.Mvc;

namespace LeaseLense.Web.Controllers;

public sealed class PropertiesController : Controller
{
    private readonly IPropertyDirectoryService _propertyDirectoryService;

    public PropertiesController(IPropertyDirectoryService propertyDirectoryService)
    {
        _propertyDirectoryService = propertyDirectoryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        var result = await _propertyDirectoryService.SearchAsync(
            new PropertyDirectoryQueryDto
            {
                QueryText = q,
                Limit = 120
            },
            cancellationToken);

        var model = new PropertyDirectoryPageViewModel
        {
            QueryText = result.QueryText,
            Properties = result.Items.Select(x => new PropertyDirectoryItemViewModel
            {
                PropertyId = x.PropertyId,
                Title = x.Title,
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
                IsVerified = x.IsVerified,
                ReviewText = x.ReviewText,
                CreatedAt = x.CreatedAt
            }).ToList(),
            ScamReports = profile.ScamReports.Select(x => new PropertyProfileScamReportViewModel
            {
                ScamType = x.ScamType,
                SeverityScore = x.SeverityScore,
                Description = x.Description,
                DateReported = x.DateReported
            }).ToList()
        };

        return View(model);
    }
}
