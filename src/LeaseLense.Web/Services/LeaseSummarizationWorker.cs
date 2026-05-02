using LeaseLense.Infrastructure.Data;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace LeaseLense.Web.Services;

public sealed class LeaseSummarizationWorker : BackgroundService
{
    private readonly ILeaseSummarizationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeaseSummarizationWorker> _logger;

    public LeaseSummarizationWorker(
        ILeaseSummarizationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<LeaseSummarizationWorker> logger)
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
                _logger.LogError(ex, "Lease summarization worker failed for JobId {JobId} / {Email}", job.LeaseSummarizationJobId, job.Email);
            }
        }
    }

    private async Task ProcessJobAsync(LeaseSummarizationJobRequest job, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<LeaseLensDbContext>();
        var extractionService = scope.ServiceProvider.GetRequiredService<IDocumentExtractionService>();
        var llmClient = scope.ServiceProvider.GetRequiredService<ILeaseSummarizationLlmClient>();
        var options = scope.ServiceProvider.GetRequiredService<IOptions<AzureDocumentIntelligenceOptions>>().Value;

        var jobRow = await db.LeaseSummarizationJobs
            .FirstOrDefaultAsync(x => x.LeaseSummarizationJobId == job.LeaseSummarizationJobId, cancellationToken);

        if (jobRow is null)
        {
            _logger.LogWarning("Lease summarization job not found in DB. JobId: {JobId}", job.LeaseSummarizationJobId);
            return;
        }

        try
        {
            jobRow.Status = "processing";
            jobRow.ErrorMessage = null;
            jobRow.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);

            var leaseDocument = await db.LeaseDocuments
                .FirstOrDefaultAsync(x => x.LeaseDocumentId == jobRow.LeaseDocumentId, cancellationToken);

            if (leaseDocument is null)
            {
                throw new InvalidOperationException("Lease document was not found.");
            }

            var ocr = await extractionService.ExtractOcrTextAsync(
                job.FileBytes,
                documentType: "lease",
                contentType: job.ContentType,
                cancellationToken: cancellationToken);

            leaseDocument.RawText = ocr.RawText;

            var llm = await llmClient.TrySummarizeAsync(ocr.RawText, cancellationToken);
            if (llm is null)
            {
                throw new InvalidOperationException("Lease summarization model did not return a result.");
            }

            var analysisId = Guid.NewGuid();
            db.LeaseAnalyses.Add(new LeaseLense.Domain.Entities.LeaseAnalysis
            {
                LeaseAnalysisId = analysisId,
                LeaseDocumentId = leaseDocument.LeaseDocumentId,
                RenterId = leaseDocument.RenterId,
                PropertyId = leaseDocument.PropertyId,
                SummaryRiskScore = llm.SummaryRiskScore,
                RiskLevel = llm.RiskLevel,
                SummaryText = llm.SummaryText,
                StructuredSummaryJson = string.IsNullOrWhiteSpace(llm.RawJson) ? null : llm.RawJson,
                ModelVersion = string.IsNullOrWhiteSpace(options.Foundry.Model) ? null : options.Foundry.Model,
                CreatedAt = DateTime.UtcNow
            });

            foreach (var flag in llm.ClauseFlags)
            {
                db.LeaseClauseFlags.Add(new LeaseLense.Domain.Entities.LeaseClauseFlag
                {
                    LeaseClauseFlagId = Guid.NewGuid(),
                    LeaseAnalysisId = analysisId,
                    ClauseType = flag.ClauseType,
                    RiskLevel = flag.RiskLevel,
                    FlaggedText = flag.FlaggedText,
                    Explanation = flag.Explanation,
                    SuggestedQuestion = flag.SuggestedQuestion
                });
            }

            jobRow.Status = "succeeded";
            jobRow.LeaseAnalysisId = analysisId;
            jobRow.UpdatedAt = DateTime.UtcNow;
            jobRow.CompletedAt = DateTime.UtcNow;

            await db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            jobRow.Status = "failed";
            jobRow.ErrorMessage = ex.Message;
            jobRow.UpdatedAt = DateTime.UtcNow;
            jobRow.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            throw;
        }
    }
}

