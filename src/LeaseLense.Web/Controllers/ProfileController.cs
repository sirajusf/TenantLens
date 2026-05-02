using System.Security.Cryptography;
using Azure;
using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Profile;
using LeaseLense.Web.Models.Profile;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using LeaseLense.Web.Services;
using System.Text;

namespace LeaseLense.Web.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private readonly IProfileService _profileService;
    private readonly UserManager<IdentityUser> _userManager;
    private readonly IEmailVerificationSender _emailVerificationSender;
    private readonly IDocumentExtractionService _documentExtractionService;
    private readonly IResidencyFallbackQueue _residencyFallbackQueue;
    private readonly ILogger<ProfileController> _logger;

    public ProfileController(
        IProfileService profileService,
        UserManager<IdentityUser> userManager,
        IEmailVerificationSender emailVerificationSender,
        IDocumentExtractionService documentExtractionService,
        IResidencyFallbackQueue residencyFallbackQueue,
        ILogger<ProfileController> logger)
    {
        _profileService = profileService;
        _userManager = userManager;
        _emailVerificationSender = emailVerificationSender;
        _documentExtractionService = documentExtractionService;
        _residencyFallbackQueue = residencyFallbackQueue;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Challenge();
        }

        var profile = await _profileService.GetProfileAsync(email, cancellationToken);
        return View(ToPageModel(profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendVerificationEmail(CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Challenge();
        }

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            TempData["ProfileError"] = "Account was not found for this email.";
            return RedirectToAction(nameof(Index));
        }

        if (user.EmailConfirmed)
        {
            TempData["ProfileSuccess"] = "Your email is already verified.";
            return RedirectToAction(nameof(Index));
        }

        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var callbackUrl = Url.Action(
            action: "VerifyEmail",
            controller: "Account",
            values: new { userId = user.Id, token = encodedToken },
            protocol: Request.Scheme) ?? string.Empty;

        try
        {
            await _emailVerificationSender.SendVerificationEmailAsync(user.Email ?? email, callbackUrl, cancellationToken);
            TempData["ProfileSuccess"] = "Verification email sent. Check your inbox.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send verification email from profile page for {Email}.", user.Email ?? email);
            TempData["ProfileError"] = "Could not send verification email. Check SMTP configuration and try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAccount(ProfileAccountFormViewModel form, CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Challenge();
        }

        try
        {
            await _profileService.UpdateProfileAsync(new UpdateUserProfileDto
            {
                Email = email,
                DisplayName = form.DisplayName,
                StreetAddress = form.StreetAddress,
                City = form.City,
                StateOrRegion = form.StateOrRegion,
                PostalCode = form.PostalCode,
                Country = form.Country
            }, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Profile update rejected for {Email}.", email);
            TempData["ProfileError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }

        TempData["ProfileSuccess"] = "Profile updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitVerification(ProfileResidencyVerificationFormViewModel form, IFormFile? proofDocument, CancellationToken cancellationToken)
    {
        var email = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(email))
        {
            return Challenge();
        }

        if (proofDocument is null || proofDocument.Length == 0)
        {
            TempData["ProfileError"] = "Please upload a supporting document.";
            return RedirectToAction(nameof(Index));
        }

        if (proofDocument.Length > 10 * 1024 * 1024)
        {
            TempData["ProfileError"] = "File too large. Max size is 10 MB.";
            return RedirectToAction(nameof(Index));
        }

        var allowed = new[] { "application/pdf", "image/png", "image/jpeg" };
        if (!allowed.Contains(proofDocument.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            TempData["ProfileError"] = "Unsupported file type. Upload PDF, PNG, or JPEG.";
            return RedirectToAction(nameof(Index));
        }

        byte[] fileBytes;
        string hashHex;
        await using (var stream = proofDocument.OpenReadStream())
        {
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory, cancellationToken);
            fileBytes = memory.ToArray();
            var hash = SHA256.HashData(fileBytes);
            hashHex = Convert.ToHexString(hash);
        }

        try
        {
            var profile = await _profileService.GetProfileAsync(email, cancellationToken);
            var primaryExtraction = await _documentExtractionService.ExtractPrimaryAsync(
                fileBytes,
                form.DocumentType ?? string.Empty,
                profile.DisplayName,
                proofDocument.ContentType,
                cancellationToken);

            if (primaryExtraction.RequiresBackgroundFallback)
            {
                await _residencyFallbackQueue.QueueAsync(new ResidencyFallbackJob
                {
                    Email = email,
                    DocumentType = form.DocumentType ?? string.Empty,
                    CommunityName = form.CommunityName ?? string.Empty,
                    PropertyTitle = form.PropertyTitle ?? string.Empty,
                    FileName = proofDocument.FileName ?? string.Empty,
                    ContentType = proofDocument.ContentType,
                    FileSizeBytes = proofDocument.Length,
                    FileHashSha256 = hashHex,
                    FileBytes = fileBytes
                }, cancellationToken);
                await _emailVerificationSender.SendResidencyVerificationInProcessEmailAsync(email, cancellationToken);
                TempData["ProfileSuccess"] = "Verification submitted and is being processed. You will receive an email soon.";
                return RedirectToAction(nameof(Index));
            }

            var extraction = primaryExtraction.Extraction;
            var result = await _profileService.SubmitResidencyVerificationAsync(new SubmitResidencyVerificationDto
            {
                Email = email,
                CommunityName = form.CommunityName ?? string.Empty,
                PropertyTitle = form.PropertyTitle ?? string.Empty,
                DocumentType = form.DocumentType ?? string.Empty,
                FileName = proofDocument.FileName ?? string.Empty,
                ContentType = proofDocument.ContentType,
                FileSizeBytes = proofDocument.Length,
                FileHashSha256 = hashHex,
                ExtractedName = extraction.ExtractedName ?? string.Empty,
                ExtractedAddress = extraction.ExtractedAddress ?? string.Empty,
                ExtractedFromDate = extraction.ExtractedFromDate,
                ExtractedToDate = extraction.ExtractedToDate,
                ParserConfidence = extraction.ParserConfidence
            }, cancellationToken);

            if (string.Equals(result.Status, "verified_stay", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Status, "pending_manual_review", StringComparison.OrdinalIgnoreCase)
                || string.Equals(result.Status, "rejected", StringComparison.OrdinalIgnoreCase))
            {
                await _emailVerificationSender.SendResidencyDecisionEmailAsync(
                    email,
                    new ResidencyDecisionEmailContext
                    {
                        Status = result.Status,
                        Reason = result.Reason,
                        DisplayName = profile.DisplayName,
                        StreetAddress = profile.StreetAddress,
                        City = profile.City,
                        StateOrRegion = profile.StateOrRegion,
                        PostalCode = profile.PostalCode,
                        Country = profile.Country,
                        DocumentType = form.DocumentType ?? string.Empty,
                        CommunityName = form.CommunityName ?? string.Empty,
                        PropertyTitle = form.PropertyTitle ?? string.Empty,
                        FileName = proofDocument.FileName ?? string.Empty,
                        ExtractedName = extraction.ExtractedName ?? string.Empty,
                        ExtractedAddress = extraction.ExtractedAddress ?? string.Empty,
                        ExtractedFromDate = extraction.ExtractedFromDate,
                        ExtractedToDate = extraction.ExtractedToDate,
                        ParserConfidence = extraction.ParserConfidence
                    },
                    cancellationToken);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Residency verification rejected before completion for {Email}. DocumentType: {DocumentType}", email, form.DocumentType);
            TempData["ProfileError"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Document Intelligence failed for {Email}. DocumentType: {DocumentType}, Status: {Status}", email, form.DocumentType, ex.Status);
            TempData["ProfileError"] = ex.Status == 404
                ? "Azure Document Intelligence returned 404. Verify this endpoint belongs to a Document Intelligence resource in the same region as the API key."
                : "Document analysis (Azure) failed. Confirm endpoint, API key, model id, and region in appsettings, then try again.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected residency verification failure for {Email}. DocumentType: {DocumentType}", email, form.DocumentType);
            TempData["ProfileError"] =
                "Verification could not be completed. If you saw a success message before, check your email; otherwise try again.";
            return RedirectToAction(nameof(Index));
        }

        TempData["ProfileSuccess"] = "Verification submitted. Status will update after checks.";
        return RedirectToAction(nameof(Index));
    }

    private static ProfilePageViewModel ToPageModel(UserProfileDto profile)
    {
        return new ProfilePageViewModel
        {
            Account = new ProfileAccountFormViewModel
            {
                DisplayName = profile.DisplayName,
                Email = profile.Email,
                EmailVerified = profile.EmailVerified,
                NameLocked = profile.NameLocked,
                HasVerifiedStay = profile.HasVerifiedStay,
                StreetAddress = profile.StreetAddress,
                City = profile.City,
                StateOrRegion = profile.StateOrRegion,
                PostalCode = profile.PostalCode,
                Country = profile.Country
            },
            VerificationForm = new ProfileResidencyVerificationFormViewModel
            {
                PropertyAddressSummary = ResidencyVerificationDisplayHelper.FormatPropertyAddress(
                    profile.StreetAddress,
                    profile.City,
                    profile.StateOrRegion,
                    profile.PostalCode,
                    profile.Country),
                CommunityName = string.Empty
            },
            VerificationStatuses = profile.Verifications
                .Select(x => new ProfileVerificationStatusViewModel
                {
                    VerificationId = x.VerificationId,
                    PropertyTitle = x.PropertyTitle,
                    Status = x.Status,
                    ExtractedName = x.ExtractedName,
                    ExtractedAddress = x.ExtractedAddress,
                    ProvidedName = x.ProvidedName,
                    ProvidedAddressSummary = x.ProvidedAddressSummary,
                    CustomerSummary = x.CustomerSummary,
                    UpdatedAt = x.UpdatedAt
                })
                .ToList()
        };
    }

}
