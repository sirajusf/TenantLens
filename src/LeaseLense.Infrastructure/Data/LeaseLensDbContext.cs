using LeaseLense.Application.Abstractions.Persistence;
using LeaseLense.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeaseLense.Infrastructure.Data;

public sealed class LeaseLensDbContext : DbContext, ILeaseLensDbContext
{
    public LeaseLensDbContext(DbContextOptions<LeaseLensDbContext> options)
        : base(options)
    {
    }

    public DbSet<Renter> Renters => Set<Renter>();
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<ReviewRating> ReviewRatings => Set<ReviewRating>();
    public DbSet<ReviewIssueTag> ReviewIssueTags => Set<ReviewIssueTag>();
    public DbSet<ScamReport> ScamReports => Set<ScamReport>();
    public DbSet<ScamEvidence> ScamEvidence => Set<ScamEvidence>();
    public DbSet<LeaseDocument> LeaseDocuments => Set<LeaseDocument>();
    public DbSet<LeaseAnalysis> LeaseAnalyses => Set<LeaseAnalysis>();
    public DbSet<LeaseClauseFlag> LeaseClauseFlags => Set<LeaseClauseFlag>();
    public DbSet<NegotiationSession> NegotiationSessions => Set<NegotiationSession>();
    public DbSet<NegotiationSuggestion> NegotiationSuggestions => Set<NegotiationSuggestion>();

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

    public Task AddPropertyAsync(Property property, CancellationToken cancellationToken = default)
    {
        return Properties.AddAsync(property, cancellationToken).AsTask();
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
            entity.Property(x => x.IsVerified).HasColumnName("is_verified");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<Property>(entity =>
        {
            entity.ToTable("properties", "dbo");
            entity.HasKey(x => x.PropertyId);
            entity.Property(x => x.PropertyId).HasColumnName("property_id");
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
    }
}
