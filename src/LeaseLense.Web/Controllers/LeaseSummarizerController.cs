using System.Security.Cryptography;
using LeaseLense.Application.Abstractions;
using LeaseLense.Domain.Entities;
using LeaseLense.Infrastructure.Data;
using LeaseLense.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaseLense.Web.Controllers;

[Authorize]
public sealed class LeaseSummarizerController : Controller
{
    private readonly IUserAccountService _userAccountService;
    private readonly ILeaseSummarizationQueue _queue;
    private readonly LeaseLensDbContext _leaseLensDb;

    public LeaseSummarizerController(
        LeaseLensDbContext leaseLensDb,
        IUserAccountService userAccountService,
        ILeaseSummarizationQueue queue)
    {
        _leaseLensDb = leaseLensDb;
        _userAccountService = userAccountService;
        _queue = queue;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(IFormFile? leaseDocument, CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Challenge();
        }

        if (leaseDocument is null || leaseDocument.Length == 0)
        {
            TempData["LeaseSummarizerError"] = "Please upload a lease document.";
            return RedirectToAction(nameof(Index));
        }

        if (leaseDocument.Length > 10 * 1024 * 1024)
        {
            TempData["LeaseSummarizerError"] = "File too large. Max size is 10 MB.";
            return RedirectToAction(nameof(Index));
        }

        var allowed = new[] { "application/pdf", "image/png", "image/jpeg" };
        if (!allowed.Contains(leaseDocument.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            TempData["LeaseSummarizerError"] = "Unsupported file type. Upload PDF, PNG, or JPEG.";
            return RedirectToAction(nameof(Index));
        }

        byte[] fileBytes;
        string hashHex;
        await using (var stream = leaseDocument.OpenReadStream())
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            fileBytes = memory.ToArray();
            hashHex = Convert.ToHexString(SHA256.HashData(fileBytes));
        }

        var renterId = await _userAccountService.GetRenterIdByEmailAsync(email, cancellationToken);
        if (renterId is null)
        {
            TempData["LeaseSummarizerError"] = "Account profile was not found for this email.";
            return RedirectToAction(nameof(Index));
        }

        var propertyId = await _leaseLensDb.Properties
            .AsNoTracking()
            .Select(x => x.PropertyId)
            .FirstOrDefaultAsync(cancellationToken);

        if (propertyId == Guid.Empty)
        {
            var placeholder = new Property
            {
                PropertyId = Guid.NewGuid(),
                CommunityId = null,
                Title = "Unknown property (lease upload)",
                StreetAddress = string.Empty,
                City = string.Empty,
                StateOrRegion = null,
                Country = string.Empty,
                PostalCode = null,
                Latitude = null,
                Longitude = null,
                PropertyType = null,
                LandlordName = null,
                ManagementCompanyName = null,
                CreatedByRenterId = renterId.Value,
                CreatedAt = DateTime.UtcNow
            };
            _leaseLensDb.Properties.Add(placeholder);
            await _leaseLensDb.SaveChangesAsync(cancellationToken);
            propertyId = placeholder.PropertyId;
        }

        var leaseDocumentId = Guid.NewGuid();
        _leaseLensDb.LeaseDocuments.Add(new LeaseDocument
        {
            LeaseDocumentId = leaseDocumentId,
            RenterId = renterId.Value,
            PropertyId = propertyId,
            DocumentType = "lease",
            FileUrl = null,
            RawText = null,
            UploadedAt = DateTime.UtcNow
        });

        var jobId = Guid.NewGuid();
        _leaseLensDb.LeaseSummarizationJobs.Add(new LeaseSummarizationJob
        {
            LeaseSummarizationJobId = jobId,
            LeaseDocumentId = leaseDocumentId,
            RenterId = renterId.Value,
            Status = "queued",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = null,
            ErrorMessage = null,
            LeaseAnalysisId = null
        });

        await _leaseLensDb.SaveChangesAsync(cancellationToken);

        await _queue.QueueAsync(new LeaseSummarizationJobRequest
        {
            LeaseSummarizationJobId = jobId,
            Email = email,
            FileName = leaseDocument.FileName ?? string.Empty,
            ContentType = leaseDocument.ContentType,
            FileSizeBytes = leaseDocument.Length,
            FileHashSha256 = hashHex,
            FileBytes = fileBytes
        }, cancellationToken);

        return RedirectToAction(nameof(Status), new { id = jobId });
    }

    [HttpGet]
    public IActionResult Status(Guid id)
    {
        ViewData["LeaseSummarizerJobId"] = id;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> StatusJson(Guid id, CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Challenge();
        }

        var renterId = await _userAccountService.GetRenterIdByEmailAsync(email, cancellationToken);
        if (renterId is null)
        {
            return Unauthorized();
        }

        var job = await _leaseLensDb.LeaseSummarizationJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.LeaseSummarizationJobId == id && x.RenterId == renterId.Value, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        string? summaryText = null;
        string? structuredSummaryJson = null;
        object[]? flags = null;
        if (string.Equals(job.Status, "succeeded", StringComparison.OrdinalIgnoreCase) && job.LeaseAnalysisId is Guid analysisId)
        {
            var analysis = await _leaseLensDb.LeaseAnalyses.AsNoTracking().FirstOrDefaultAsync(x => x.LeaseAnalysisId == analysisId, cancellationToken);
            summaryText = analysis?.SummaryText;
            structuredSummaryJson = analysis?.StructuredSummaryJson;
            var clauseFlags = await _leaseLensDb.LeaseClauseFlags.AsNoTracking()
                .Where(x => x.LeaseAnalysisId == analysisId)
                .ToListAsync(cancellationToken);
            flags = clauseFlags.Select(x => new
            {
                x.ClauseType,
                x.RiskLevel,
                x.FlaggedText,
                x.Explanation,
                x.SuggestedQuestion
            }).ToArray();
        }

        return Json(new
        {
            jobId = job.LeaseSummarizationJobId,
            status = job.Status,
            errorMessage = job.ErrorMessage,
            completedAt = job.CompletedAt,
            leaseAnalysisId = job.LeaseAnalysisId,
            summaryText,
            structuredSummaryJson,
            clauseFlags = flags ?? []
        });
    }
}

