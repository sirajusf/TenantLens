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
                City = x.City,
                ScamType = x.ScamType,
                SeverityScore = x.SeverityScore,
                DateReported = x.DateReported,
                Description = x.Description
            }).ToList()
        };

        return View(model);
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var metadata = await _scamReportMvpService.GetFormMetadataAsync(cancellationToken);
        return View(ToCreatePageModel(new CreateScamReportViewModel(), metadata));
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateScamReportViewModel form, CancellationToken cancellationToken)
    {
        var metadata = await _scamReportMvpService.GetFormMetadataAsync(cancellationToken);

        if (!ModelState.IsValid)
        {
            return View(ToCreatePageModel(form, metadata));
        }

        if (string.IsNullOrWhiteSpace(User.Identity?.Name))
        {
            return Challenge();
        }

        var request = new CreateScamReportDto
        {
            PropertyId = form.PropertyNotListed ? null : form.PropertyId,
            NewPropertyTitle = form.NewPropertyTitle,
            NewPropertyStreetAddress = form.NewPropertyStreetAddress,
            NewPropertyCity = form.NewPropertyCity,
            NewPropertyCountry = form.NewPropertyCountry,
            ScamType = form.ScamType,
            Description = form.Description,
            SeverityScore = form.SeverityScore,
            ReporterEmail = User.Identity!.Name!
        };

        await _scamReportMvpService.SubmitScamReportAsync(request, cancellationToken);
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
            Properties = metadata.Properties
                .Select(x => new ScamReportOptionViewModel { Id = x.Id, Label = x.Label })
                .ToList()
        };
    }
}
