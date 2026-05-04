using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Search;
using LeaseLense.Application.ScamReports;
using LeaseLense.Web.Models.ScamReports;
using LeaseLense.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaseLense.Web.Controllers;

public sealed class ScamReportsController : Controller
{
    private readonly IScamReportMvpService _scamReportMvpService;
    private readonly SmartSearchOrchestrator _smartSearch;

    public ScamReportsController(
        IScamReportMvpService scamReportMvpService,
        SmartSearchOrchestrator smartSearch)
    {
        _scamReportMvpService = scamReportMvpService;
        _smartSearch = smartSearch;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? q,
        string? city,
        decimal? minSeverity,
        string? scamType,
        decimal? maxSeverity,
        DateOnly? dateReportedAfter,
        string? sortBy,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> interpretedFilters = [];
        bool llmUnavailable = false;
        bool nlFallback = false;

        if (!string.IsNullOrWhiteSpace(q))
        {
            var smart = await _smartSearch.SearchAsync(q, SearchEntityType.ScamReport, 120, cancellationToken);
            var effective = smart.EffectiveQuery;

            q = effective.QueryText ?? q;
            city ??= effective.City;
            minSeverity ??= effective.MinSeverity;
            scamType ??= effective.ScamType;
            maxSeverity ??= effective.MaxSeverity;
            dateReportedAfter ??= effective.DateReportedAfter;
            sortBy ??= effective.SortBy;

            interpretedFilters = smart.InterpretedFilters;
            llmUnavailable = smart.LlmUnavailable;
            nlFallback = smart.NlFallback;
        }

        var reports = await _scamReportMvpService.GetScamReportsAsync(
            q, city, minSeverity, scamType, maxSeverity, dateReportedAfter, sortBy, cancellationToken);

        var model = new ScamReportIndexViewModel
        {
            QueryText = q,
            City = city,
            MinSeverity = minSeverity,
            ScamType = scamType,
            MaxSeverity = maxSeverity,
            DateReportedAfter = dateReportedAfter,
            SortBy = sortBy,
            Reports = reports.Select(x => new ScamReportListItemViewModel
            {
                PropertyId = x.PropertyId,
                PropertyTitle = x.PropertyTitle,
                CommunityName = x.CommunityName,
                City = x.City,
                ScamType = x.ScamType,
                SeverityScore = x.SeverityScore,
                DateReported = x.DateReported,
                Description = x.Description,
                VerificationBadge = x.VerificationBadge
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
        var metadata = await _scamReportMvpService.GetFormMetadataAsync(reporterEmail, cancellationToken);
        return View(ToCreatePageModel(ApplyPreferredProperty(new CreateScamReportViewModel(), metadata, propertyId), metadata));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateScamReportViewModel form, CancellationToken cancellationToken)
    {
        var reporterEmail = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(reporterEmail))
        {
            return Challenge();
        }
        var metadata = await _scamReportMvpService.GetFormMetadataAsync(reporterEmail, cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(ToCreatePageModel(form, metadata));
        }

        var request = new CreateScamReportDto
        {
            PropertyId = form.PropertyNotListed ? null : form.PropertyId,
            NewCommunityName = form.NewCommunityName,
            NewPropertyTitle = form.NewPropertyTitle,
            NewPropertyStreetAddress = form.NewPropertyStreetAddress,
            NewPropertyCity = form.NewPropertyCity,
            NewPropertyCountry = form.NewPropertyCountry,
            ScamType = form.ScamType,
            Description = form.Description,
            SeverityScore = form.SeverityScore,
            ReporterEmail = reporterEmail
        };

        try
        {
            await _scamReportMvpService.SubmitScamReportAsync(request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View(ToCreatePageModel(form, metadata));
        }
        TempData["ScamReportSuccess"] = "Scam report submitted successfully.";
        return RedirectToAction(nameof(Index));
    }

    private static ScamReportCreatePageViewModel ToCreatePageModel(
        CreateScamReportViewModel form,
        ScamReportFormMetadataDto metadata)
    {
        return new ScamReportCreatePageViewModel
        {
            Form = form,
            CanSubmit = metadata.CanSubmit,
            RestrictionMessage = metadata.RestrictionMessage,
            Properties = metadata.Properties
                .Select(x => new ScamReportOptionViewModel { Id = x.Id, Label = x.Label })
                .ToList()
        };
    }

    private static CreateScamReportViewModel ApplyPreferredProperty(
        CreateScamReportViewModel form,
        ScamReportFormMetadataDto metadata,
        Guid? preferredPropertyId)
    {
        if (!preferredPropertyId.HasValue)
        {
            return form;
        }

        var hasProperty = metadata.Properties.Any(x => x.Id == preferredPropertyId.Value);
        if (!hasProperty)
        {
            return form;
        }

        return new CreateScamReportViewModel
        {
            PropertyId = preferredPropertyId.Value,
            PropertyNotListed = false
        };
    }
}
