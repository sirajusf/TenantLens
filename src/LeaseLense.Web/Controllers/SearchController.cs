using LeaseLense.Application.Search;
using LeaseLense.Web.Models.Search;
using LeaseLense.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaseLense.Web.Controllers;

public sealed class SearchController : Controller
{
    private readonly SmartSearchOrchestrator _smartSearch;

    public SearchController(
        SmartSearchOrchestrator smartSearch)
    {
        _smartSearch = smartSearch;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(q))
            return View(new NlSearchViewModel());

        var smart = await _smartSearch.SearchAsync(q, forcedEntityType: null, limit: 40, cancellationToken);

        return View(new NlSearchViewModel
        {
            Query = q,
            HasSearched = true,
            TargetEntityTypes = smart.TargetEntityTypes,
            InterpretedFilters = smart.InterpretedFilters,
            LlmUnavailable = smart.LlmUnavailable,
            NlFallback = smart.NlFallback,
            PropertyResults = smart.PropertyResults,
            ReviewResults = smart.ReviewResults,
            ScamReportResults = smart.ScamReportResults
        });
    }
}
