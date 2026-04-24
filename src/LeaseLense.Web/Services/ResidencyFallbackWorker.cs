using Azure;
using LeaseLense.Application.Abstractions;
using LeaseLense.Application.Profile;

namespace LeaseLense.Web.Services;

public sealed class ResidencyFallbackWorker : BackgroundService
{
    private readonly IResidencyFallbackQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ResidencyFallbackWorker> _logger;

    public ResidencyFallbackWorker(
        IResidencyFallbackQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<ResidencyFallbackWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);
            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fallback worker failed for {Email} / {DocumentType}", job.Email, job.DocumentType);
            }
        }
    }

    private async Task ProcessJobAsync(ResidencyFallbackJob job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var extractionService = scope.ServiceProvider.GetRequiredService<IDocumentExtractionService>();
        var emailSender = scope.ServiceProvider.GetRequiredService<IEmailVerificationSender>();

        var profile = await profileService.GetProfileAsync(job.Email, cancellationToken);
        var extraction = await extractionService.ExtractResidencyEvidenceAsync(
            job.FileBytes,
            job.DocumentType,
            profile.DisplayName,
            job.ContentType,
            cancellationToken);
        _logger.LogInformation(
            "Fallback worker extraction completed for {Email}. DocumentType: {DocumentType}, NamePresent: {NamePresent}, AddressPresent: {AddressPresent}, ParserConfidence: {ParserConfidence}",
            job.Email,
            job.DocumentType,
            !string.IsNullOrWhiteSpace(extraction.ExtractedName),
            !string.IsNullOrWhiteSpace(extraction.ExtractedAddress),
            extraction.ParserConfidence);

        var result = await profileService.SubmitResidencyVerificationAsync(new SubmitResidencyVerificationDto
        {
            Email = job.Email,
            CommunityName = job.CommunityName,
            PropertyTitle = job.PropertyTitle,
            DocumentType = job.DocumentType,
            FileName = job.FileName,
            ContentType = job.ContentType,
            FileSizeBytes = job.FileSizeBytes,
            FileHashSha256 = job.FileHashSha256,
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
            _logger.LogInformation(
                "Sending fallback decision email for {Email}. Status: {Status}, NamePresent: {NamePresent}, AddressPresent: {AddressPresent}",
                job.Email,
                result.Status,
                !string.IsNullOrWhiteSpace(extraction.ExtractedName),
                !string.IsNullOrWhiteSpace(extraction.ExtractedAddress));
            await emailSender.SendResidencyDecisionEmailAsync(
                job.Email,
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
                    DocumentType = job.DocumentType,
                    CommunityName = job.CommunityName,
                    PropertyTitle = job.PropertyTitle,
                    FileName = job.FileName,
                    ExtractedName = extraction.ExtractedName ?? string.Empty,
                    ExtractedAddress = extraction.ExtractedAddress ?? string.Empty,
                    ExtractedFromDate = extraction.ExtractedFromDate,
                    ExtractedToDate = extraction.ExtractedToDate,
                    ParserConfidence = extraction.ParserConfidence
                },
                cancellationToken);
        }
    }
}
