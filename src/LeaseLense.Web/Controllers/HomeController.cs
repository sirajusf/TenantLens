using System.Diagnostics;
using LeaseLense.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;
using LeaseLense.Web.Models.Home;
using LeaseLense.Web.Models;

namespace LeaseLense.Web.Controllers;

public class HomeController : Controller
{
    private readonly IHomePageReadService _homePageReadService;

    public HomeController(IHomePageReadService homePageReadService)
    {
        _homePageReadService = homePageReadService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var data = await _homePageReadService.GetHomePageDataAsync(cancellationToken);

        var model = new HomeIndexViewModel
        {
            Properties = data.Properties
                .Select(x => new HomePropertyListItemViewModel
                {
                    PropertyId = x.PropertyId,
                    Title = x.Title,
                    StreetAddress = x.StreetAddress,
                    City = x.City,
                    Country = x.Country,
                    CreatedAt = x.CreatedAt
                })
                .ToList(),
            Reviews = data.Reviews
                .Select(x => new HomeReviewListItemViewModel
                {
                    PropertyTitle = x.PropertyTitle,
                    City = x.City,
                    AverageRating = x.AverageRating,
                    MonthlyRent = x.MonthlyRent,
                    ReviewText = x.ReviewText,
                    VerificationBadge = x.VerificationBadge
                })
                .ToList(),
            ScamReports = data.ScamReports
                .Select(x => new HomeScamReportListItemViewModel
                {
                    PropertyTitle = x.PropertyTitle,
                    City = x.City,
                    ScamType = x.ScamType,
                    SeverityScore = x.SeverityScore,
                    Description = x.Description,
                    VerificationBadge = x.VerificationBadge
                })
                .ToList()
        };

        return View(model);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Visualizations()
    {
        return View();
    }

    public IActionResult Contact()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
