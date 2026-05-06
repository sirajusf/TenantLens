using LeaseLense.Application.Abstractions;
using LeaseLense.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeaseLense.Infrastructure.Data;
//here this is leanselensdbcontext class which is used to interact with the database is exending 

public sealed class LeaseLensDbContext : DbContext, ILeaseLensRepository
{
    public LeaseLensDbContext(DbContextOptions<LeaseLensDbContext> options)
        : base(options)
    {
    }

    public DbSet<Renter> Renters => Set<Renter>();
    public DbSet<Community> Communities => Set<Community>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewRating> ReviewRatings => Set<ReviewRating>();
    public DbSet<ReviewIssueTag> ReviewIssueTags => Set<ReviewIssueTag>();
    public DbSet<ScamReport> ScamReports => Set<ScamReport>();
    public DbSet<ScamEvidence> ScamEvidence => Set<ScamEvidence>();
    public DbSet<LeaseDocument> LeaseDocuments => Set<LeaseDocument>();
    public DbSet<LeaseAnalysis> LeaseAnalyses => Set<LeaseAnalysis>();
    public DbSet<LeaseClauseFlag> LeaseClauseFlags => Set<LeaseClauseFlag>();
    public DbSet<LeaseSummarizationJob> LeaseSummarizationJobs => Set<LeaseSummarizationJob>();
    public DbSet<NegotiationSession> NegotiationSessions => Set<NegotiationSession>();
    public DbSet<NegotiationSuggestion> NegotiationSuggestions => Set<NegotiationSuggestion>();
    public DbSet<RenterPropertyVerification> RenterPropertyVerifications => Set<RenterPropertyVerification>();
    public DbSet<ResidencyVerificationDocument> ResidencyVerificationDocuments => Set<ResidencyVerificationDocument>();

    public Task<List<Community>> GetCommunitiesAsync(CancellationToken cancellationToken = default)
    {
        return Communities.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<Property>> GetPropertiesAsync(CancellationToken cancellationToken = default)
    {
        return Properties.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<Renter>> GetRentersAsync(CancellationToken cancellationToken = default)
    {
        return Renters.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<Renter?> GetRenterByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return Renters.FirstOrDefaultAsync(x => x.Email == email, cancellationToken);
    }

    public Task<Renter?> GetRenterByIdAsync(Guid renterId, CancellationToken cancellationToken = default)
    {
        return Renters.FirstOrDefaultAsync(x => x.RenterId == renterId, cancellationToken);
    }

    public Task<List<Review>> GetReviewsAsync(CancellationToken cancellationToken = default)
    {
        return Reviews.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<ReviewRating>> GetReviewRatingsAsync(CancellationToken cancellationToken = default)
    {
        return ReviewRatings.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<ScamReport>> GetScamReportsAsync(CancellationToken cancellationToken = default)
    {
        return ScamReports.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<RenterPropertyVerification>> GetRenterPropertyVerificationsAsync(CancellationToken cancellationToken = default)
    {
        return RenterPropertyVerifications.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task<List<ResidencyVerificationDocument>> GetResidencyVerificationDocumentsAsync(CancellationToken cancellationToken = default)
    {
        return ResidencyVerificationDocuments.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task AddPropertyAsync(Property property, CancellationToken cancellationToken = default)
    {
        return Properties.AddAsync(property, cancellationToken).AsTask();
    }

    public Task AddCommunityAsync(Community community, CancellationToken cancellationToken = default)
    {
        return Communities.AddAsync(community, cancellationToken).AsTask();
    }

    public Task AddRenterAsync(Renter renter, CancellationToken cancellationToken = default)
    {
        return Renters.AddAsync(renter, cancellationToken).AsTask();
    }

    public Task AddReviewAsync(Review review, CancellationToken cancellationToken = default)
    {
        return Reviews.AddAsync(review, cancellationToken).AsTask();
    }

    public Task AddReviewRatingAsync(ReviewRating reviewRating, CancellationToken cancellationToken = default)
    {
        return ReviewRatings.AddAsync(reviewRating, cancellationToken).AsTask();
    }

    public Task<List<ReviewIssueTag>> GetReviewIssueTagsAsync(CancellationToken cancellationToken = default)
    {
        return ReviewIssueTags.AsNoTracking().ToListAsync(cancellationToken);
    }

    public Task AddScamReportAsync(ScamReport scamReport, CancellationToken cancellationToken = default)
    {
        return ScamReports.AddAsync(scamReport, cancellationToken).AsTask();
    }

    public Task AddRenterPropertyVerificationAsync(RenterPropertyVerification verification, CancellationToken cancellationToken = default)
    {
        return RenterPropertyVerifications.AddAsync(verification, cancellationToken).AsTask();
    }

    public Task AddResidencyVerificationDocumentAsync(ResidencyVerificationDocument document, CancellationToken cancellationToken = default)
    {
        return ResidencyVerificationDocuments.AddAsync(document, cancellationToken).AsTask();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Renter>(entity =>
        {
            entity.ToTable("renters", "dbo");
            entity.HasKey(x => x.RenterId);
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(510);
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(510);
            entity.Property(x => x.StreetAddress).HasColumnName("street_address").HasMaxLength(510);
            entity.Property(x => x.City).HasColumnName("city").HasMaxLength(200);
            entity.Property(x => x.StateOrRegion).HasColumnName("state_or_region").HasMaxLength(200);
            entity.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(40);
            entity.Property(x => x.Country).HasColumnName("country").HasMaxLength(200);
            entity.Property(x => x.EmailVerified).HasColumnName("email_verified");
            entity.Property(x => x.IsVerified).HasColumnName("is_verified");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Property>(entity =>
        {
            entity.ToTable("properties", "dbo");
            entity.HasKey(x => x.PropertyId);
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
            entity.Property(x => x.CommunityId).HasColumnName("community_id");
            entity.Property(x => x.Title).HasColumnName("title").HasMaxLength(510);
            entity.Property(x => x.StreetAddress).HasColumnName("street_address").HasMaxLength(510);
            entity.Property(x => x.City).HasColumnName("city").HasMaxLength(200);
            entity.Property(x => x.StateOrRegion).HasColumnName("state_or_region").HasMaxLength(200);
            entity.Property(x => x.Country).HasColumnName("country").HasMaxLength(200);
            entity.Property(x => x.PostalCode).HasColumnName("postal_code").HasMaxLength(40);
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasPrecision(9, 6);
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasPrecision(9, 6);
            entity.Property(x => x.PropertyType).HasColumnName("property_type").HasMaxLength(100);
            entity.Property(x => x.LandlordName).HasColumnName("landlord_name").HasMaxLength(510);
            entity.Property(x => x.ManagementCompanyName).HasColumnName("management_company_name").HasMaxLength(510);
            entity.Property(x => x.CreatedByRenterId).HasColumnName("created_by_renter_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.CreatedByRenterId)
                .HasConstraintName("FK_properties_created_by_renter");

            entity.HasOne<Community>()
                .WithMany()
                .HasForeignKey(x => x.CommunityId)
                .HasConstraintName("FK_properties_community");
        });

        modelBuilder.Entity<Community>(entity =>
        {
            entity.ToTable("communities", "dbo");
            entity.HasKey(x => x.CommunityId);
            entity.Property(x => x.CommunityId).HasColumnName("community_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(510);
            entity.Property(x => x.City).HasColumnName("city").HasMaxLength(200);
            entity.Property(x => x.StateOrRegion).HasColumnName("state_or_region").HasMaxLength(200);
            entity.Property(x => x.Country).HasColumnName("country").HasMaxLength(200);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("reviews", "dbo");
            entity.HasKey(x => x.ReviewId);
            entity.Property(x => x.ReviewId).HasColumnName("review_id");
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.LeaseStartDate).HasColumnName("lease_start_date");
            entity.Property(x => x.LeaseEndDate).HasColumnName("lease_end_date");
            entity.Property(x => x.MonthlyRent).HasColumnName("monthly_rent").HasPrecision(10, 2);
            entity.Property(x => x.MoveInYear).HasColumnName("move_in_year");
            entity.Property(x => x.UnitType).HasColumnName("unit_type").HasMaxLength(200);
            entity.Property(x => x.ReviewText).HasColumnName("review_text");
            entity.Property(x => x.VerificationStatus).HasColumnName("verification_status").HasMaxLength(100);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne<Property>()
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .HasConstraintName("FK_reviews_property");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.RenterId)
                .HasConstraintName("FK_reviews_renter");
        });

        modelBuilder.Entity<ReviewRating>(entity =>
        {
            entity.ToTable("review_ratings", "dbo");
            entity.HasKey(x => x.ReviewRatingId);
            entity.Property(x => x.ReviewRatingId).HasColumnName("review_rating_id");
            entity.Property(x => x.ReviewId).HasColumnName("review_id");
            entity.Property(x => x.RatingCategory).HasColumnName("rating_category").HasMaxLength(200);
            entity.Property(x => x.RatingScore).HasColumnName("rating_score");

            entity.HasOne<Review>()
                .WithMany()
                .HasForeignKey(x => x.ReviewId)
                .HasConstraintName("FK_review_ratings_review");
        });

        modelBuilder.Entity<ReviewIssueTag>(entity =>
        {
            entity.ToTable("review_issue_tags", "dbo");
            entity.HasKey(x => x.ReviewIssueTagId);
            entity.Property(x => x.ReviewIssueTagId).HasColumnName("review_issue_tag_id");
            entity.Property(x => x.ReviewId).HasColumnName("review_id");
            entity.Property(x => x.IssueType).HasColumnName("issue_type").HasMaxLength(200);

            entity.HasOne<Review>()
                .WithMany()
                .HasForeignKey(x => x.ReviewId)
                .HasConstraintName("FK_review_issue_tags_review");
        });

        modelBuilder.Entity<ScamReport>(entity =>
        {
            entity.ToTable("scam_reports", "dbo");
            entity.HasKey(x => x.ScamReportId);
            entity.Property(x => x.ScamReportId).HasColumnName("scam_report_id");
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.ScamType).HasColumnName("scam_type").HasMaxLength(200);
            entity.Property(x => x.Description).HasColumnName("description");
            entity.Property(x => x.VerificationStatus).HasColumnName("verification_status").HasMaxLength(100);
            entity.Property(x => x.SeverityScore).HasColumnName("severity_score").HasPrecision(4, 2);
            entity.Property(x => x.DateReported).HasColumnName("date_reported");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne<Property>()
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .HasConstraintName("FK_scam_reports_property");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.RenterId)
                .HasConstraintName("FK_scam_reports_renter");
        });

        modelBuilder.Entity<ScamEvidence>(entity =>
        {
            entity.ToTable("scam_evidence", "dbo");
            entity.HasKey(x => x.ScamEvidenceId);
            entity.Property(x => x.ScamEvidenceId).HasColumnName("scam_evidence_id");
            entity.Property(x => x.ScamReportId).HasColumnName("scam_report_id");
            entity.Property(x => x.FileUrl).HasColumnName("file_url").HasMaxLength(2000);
            entity.Property(x => x.FileType).HasColumnName("file_type").HasMaxLength(200);
            entity.Property(x => x.UploadedAt).HasColumnName("uploaded_at");

            entity.HasOne<ScamReport>()
                .WithMany()
                .HasForeignKey(x => x.ScamReportId)
                .HasConstraintName("FK_scam_evidence_scam_report");
        });

        modelBuilder.Entity<LeaseDocument>(entity =>
        {
            entity.ToTable("lease_documents", "dbo");
            entity.HasKey(x => x.LeaseDocumentId);
            entity.Property(x => x.LeaseDocumentId).HasColumnName("lease_document_id");
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
            entity.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(100);
            entity.Property(x => x.FileUrl).HasColumnName("file_url").HasMaxLength(2000);
            entity.Property(x => x.RawText).HasColumnName("raw_text");
            entity.Property(x => x.UploadedAt).HasColumnName("uploaded_at");

            entity.HasOne<Property>()
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .HasConstraintName("FK_lease_documents_property");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.RenterId)
                .HasConstraintName("FK_lease_documents_renter");
        });

        modelBuilder.Entity<LeaseAnalysis>(entity =>
        {
            entity.ToTable("lease_analyses", "dbo");
            entity.HasKey(x => x.LeaseAnalysisId);
            entity.Property(x => x.LeaseAnalysisId).HasColumnName("lease_analysis_id");
            entity.Property(x => x.LeaseDocumentId).HasColumnName("lease_document_id");
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
            entity.Property(x => x.SummaryRiskScore).HasColumnName("summary_risk_score").HasPrecision(5, 2);
            entity.Property(x => x.RiskLevel).HasColumnName("risk_level").HasMaxLength(40);
            entity.Property(x => x.SummaryText).HasColumnName("summary_text");
            entity.Property(x => x.StructuredSummaryJson).HasColumnName("structured_summary_json");
            entity.Property(x => x.ModelVersion).HasColumnName("model_version").HasMaxLength(200);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne<LeaseDocument>()
                .WithMany()
                .HasForeignKey(x => x.LeaseDocumentId)
                .HasConstraintName("FK_lease_analyses_lease_document");

            entity.HasOne<Property>()
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .HasConstraintName("FK_lease_analyses_property");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.RenterId)
                .HasConstraintName("FK_lease_analyses_renter");
        });

        modelBuilder.Entity<LeaseClauseFlag>(entity =>
        {
            entity.ToTable("lease_clause_flags", "dbo");
            entity.HasKey(x => x.LeaseClauseFlagId);
            entity.Property(x => x.LeaseClauseFlagId).HasColumnName("lease_clause_flag_id");
            entity.Property(x => x.LeaseAnalysisId).HasColumnName("lease_analysis_id");
            entity.Property(x => x.ClauseType).HasColumnName("clause_type").HasMaxLength(200);
            entity.Property(x => x.RiskLevel).HasColumnName("risk_level").HasMaxLength(40);
            entity.Property(x => x.FlaggedText).HasColumnName("flagged_text");
            entity.Property(x => x.Explanation).HasColumnName("explanation");
            entity.Property(x => x.SuggestedQuestion).HasColumnName("suggested_question");

            entity.HasOne<LeaseAnalysis>()
                .WithMany()
                .HasForeignKey(x => x.LeaseAnalysisId)
                .HasConstraintName("FK_lease_clause_flags_lease_analysis");
        });

        modelBuilder.Entity<LeaseSummarizationJob>(entity =>
        {
            entity.ToTable("lease_summarization_jobs", "dbo");
            entity.HasKey(x => x.LeaseSummarizationJobId);
            entity.Property(x => x.LeaseSummarizationJobId).HasColumnName("lease_summarization_job_id");
            entity.Property(x => x.LeaseDocumentId).HasColumnName("lease_document_id");
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(40);
            entity.Property(x => x.ErrorMessage).HasColumnName("error_message").HasMaxLength(2000);
            entity.Property(x => x.LeaseAnalysisId).HasColumnName("lease_analysis_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            entity.Property(x => x.CompletedAt).HasColumnName("completed_at");

            entity.HasOne<LeaseDocument>()
                .WithMany()
                .HasForeignKey(x => x.LeaseDocumentId)
                .HasConstraintName("FK_lease_summarization_jobs_lease_document");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.RenterId)
                .HasConstraintName("FK_lease_summarization_jobs_renter");

            entity.HasOne<LeaseAnalysis>()
                .WithMany()
                .HasForeignKey(x => x.LeaseAnalysisId)
                .HasConstraintName("FK_lease_summarization_jobs_lease_analysis");
        });

        modelBuilder.Entity<NegotiationSession>(entity =>
        {
            entity.ToTable("negotiation_sessions", "dbo");
            entity.HasKey(x => x.NegotiationSessionId);
            entity.Property(x => x.NegotiationSessionId).HasColumnName("negotiation_session_id");
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
            entity.Property(x => x.LeaseAnalysisId).HasColumnName("lease_analysis_id");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.RenterId)
                .HasConstraintName("FK_negotiation_sessions_renter");

            entity.HasOne<Property>()
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .HasConstraintName("FK_negotiation_sessions_property");

            entity.HasOne<LeaseAnalysis>()
                .WithMany()
                .HasForeignKey(x => x.LeaseAnalysisId)
                .HasConstraintName("FK_negotiation_sessions_lease_analysis");
        });

        modelBuilder.Entity<NegotiationSuggestion>(entity =>
        {
            entity.ToTable("negotiation_suggestions", "dbo");
            entity.HasKey(x => x.NegotiationSuggestionId);
            entity.Property(x => x.NegotiationSuggestionId).HasColumnName("negotiation_suggestion_id");
            entity.Property(x => x.NegotiationSessionId).HasColumnName("negotiation_session_id");
            entity.Property(x => x.SuggestionType).HasColumnName("suggestion_type").HasMaxLength(200);
            entity.Property(x => x.Priority).HasColumnName("priority").HasMaxLength(40);
            entity.Property(x => x.Content).HasColumnName("content");

            entity.HasOne<NegotiationSession>()
                .WithMany()
                .HasForeignKey(x => x.NegotiationSessionId)
                .HasConstraintName("FK_negotiation_suggestions_session");
        });

        modelBuilder.Entity<RenterPropertyVerification>(entity =>
        {
            entity.ToTable("renter_property_verifications", "dbo");
            entity.HasKey(x => x.RenterPropertyVerificationId);
            entity.Property(x => x.RenterPropertyVerificationId).HasColumnName("renter_property_verification_id");
            entity.Property(x => x.RenterId).HasColumnName("renter_id");
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
            entity.Property(x => x.Status).HasColumnName("status").HasMaxLength(80);
            entity.Property(x => x.ConfidenceScore).HasColumnName("confidence_score").HasPrecision(5, 2);
            entity.Property(x => x.VerifiedFrom).HasColumnName("verified_from");
            entity.Property(x => x.VerifiedTo).HasColumnName("verified_to");
            entity.Property(x => x.ReviewReason).HasColumnName("review_reason").HasMaxLength(1000);
            entity.Property(x => x.VerifiedAt).HasColumnName("verified_at");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            entity.HasOne<Renter>()
                .WithMany()
                .HasForeignKey(x => x.RenterId)
                .HasConstraintName("FK_renter_property_verifications_renter");

            entity.HasOne<Property>()
                .WithMany()
                .HasForeignKey(x => x.PropertyId)
                .HasConstraintName("FK_renter_property_verifications_property");
        });

        modelBuilder.Entity<ResidencyVerificationDocument>(entity =>
        {
            entity.ToTable("residency_verification_documents", "dbo");
            entity.HasKey(x => x.ResidencyVerificationDocumentId);
            entity.Property(x => x.ResidencyVerificationDocumentId).HasColumnName("residency_verification_document_id");
            entity.Property(x => x.RenterPropertyVerificationId).HasColumnName("renter_property_verification_id");
            entity.Property(x => x.DocumentType).HasColumnName("document_type").HasMaxLength(100);
            entity.Property(x => x.FileName).HasColumnName("file_name").HasMaxLength(400);
            entity.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(200);
            entity.Property(x => x.FileSizeBytes).HasColumnName("file_size_bytes");
            entity.Property(x => x.FileHashSha256).HasColumnName("file_hash_sha256").HasMaxLength(128);
            entity.Property(x => x.ExtractedName).HasColumnName("extracted_name").HasMaxLength(510);
            entity.Property(x => x.ExtractedAddress).HasColumnName("extracted_address").HasMaxLength(800);
            entity.Property(x => x.ExtractedFromDate).HasColumnName("extracted_from_date");
            entity.Property(x => x.ExtractedToDate).HasColumnName("extracted_to_date");
            entity.Property(x => x.ParserConfidence).HasColumnName("parser_confidence").HasPrecision(5, 2);
            entity.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasMaxLength(80);
            entity.Property(x => x.UploadedAt).HasColumnName("uploaded_at");

            entity.HasOne<RenterPropertyVerification>()
                .WithMany()
                .HasForeignKey(x => x.RenterPropertyVerificationId)
                .HasConstraintName("FK_residency_verification_documents_verification");
        });
    }
}
