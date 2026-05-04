using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Search;
using LeaseLense.Web.Models.Search;
using LeaseLense.Web.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaseLense.Web.Controllers;

public sealed class SearchController : Controller
{
    private readonly INlQueryParserLlmClient _nlParser;
    private readonly ICoreSearchService _coreSearch;
    private readonly ILogger<SearchController> _logger;

    public SearchController(
        INlQueryParserLlmClient nlParser,
        ICoreSearchService coreSearch,
        ILogger<SearchController> logger)
    {
        _nlParser = nlParser;
        _coreSearch = coreSearch;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q, CancellationToken cancellationToken)
    {
        // Empty state — just show the search page with examples
        if (string.IsNullOrWhiteSpace(q))
        {
            return View(new NlSearchViewModel());
        }

        // Step 1: Ask the LLM to parse the natural-language query
        NlQueryParseResult? parsed = null;
        try
        {
            parsed = await _nlParser.TryParseAsync(q, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NL query parser threw unexpectedly; falling back to plain search.");
        }

        var llmUnavailable = parsed is null;

        // Step 2: Build a SearchQueryDto — use LLM result or fall back to plain keyword
        var searchQuery = parsed?.Query ?? new SearchQueryDto
        {
            EntityType = SearchEntityType.Property,
            QueryText = q,
            Limit = 40
        };

        // Step 3: Execute the search
        IReadOnlyList<PropertyMatch> propertyResults = [];
        IReadOnlyList<ReviewMatch> reviewResults = [];
        IReadOnlyList<ScamReportMatch> scamResults = [];

        try
        {
            var raw = await _coreSearch.SearchAsync(searchQuery, cancellationToken);

            switch (searchQuery.EntityType)
            {
                case SearchEntityType.Property:
                    propertyResults = raw as IReadOnlyList<PropertyMatch> ?? [];
                    break;
                case SearchEntityType.Review:
                    reviewResults = raw as IReadOnlyList<ReviewMatch> ?? [];
                    break;
                case SearchEntityType.ScamReport:
                    scamResults = raw as IReadOnlyList<ScamReportMatch> ?? [];
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CoreSearchService threw for NL search query '{Query}'.", q);
        }

        var vm = new NlSearchViewModel
        {
            Query = q,
            HasSearched = true,
            EntityType = searchQuery.EntityType,
            InterpretedFilters = parsed?.InterpretedFilters ?? [],
            LlmUnavailable = llmUnavailable,
            PropertyResults = propertyResults,
            ReviewResults = reviewResults,
            ScamReportResults = scamResults
        };

        return View(vm);
    }
}
