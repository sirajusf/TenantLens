using LeaseLense.Application.Abstractions;
using LeaseLense.Application.ScamReports;
using LeaseLense.Web.Models.ScamReports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaseLense.Web.Controllers;

public sealed class ScamReportsController : Controller
{
    private readonly IScamReportMvpService _scamReportMvpService;

    public ScamReportsController(IScamReportMvpService scamReportMvpService)
    {
        _scamReportMvpService = scamReportMvpService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var reports = await _scamReportMvpService.GetScamReportsAsync(cancellationToken);
        var model = new ScamReportIndexViewModel
        {
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
