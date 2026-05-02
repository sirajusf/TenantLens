using Microsoft.EntityFrameworkCore;

namespace LeaseLense.Infrastructure.Data;

public static class LeaseLensSchemaInitializer
{
    public static async Task EnsureCreatedAsync(LeaseLensDbContext dbContext, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.lease_summarization_jobs', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[lease_summarization_jobs](
                    [lease_summarization_job_id] uniqueidentifier NOT NULL PRIMARY KEY,
                    [lease_document_id] uniqueidentifier NOT NULL,
                    [renter_id] uniqueidentifier NOT NULL,
                    [status] nvarchar(40) NOT NULL,
                    [error_message] nvarchar(2000) NULL,
                    [lease_analysis_id] uniqueidentifier NULL,
                    [created_at] datetime2 NOT NULL,
                    [updated_at] datetime2 NOT NULL,
                    [completed_at] datetime2 NULL
                );
            END;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);

        const string alterLeaseAnalyses = """
            IF COL_LENGTH(N'dbo.lease_analyses', N'structured_summary_json') IS NULL
            BEGIN
                ALTER TABLE [dbo].[lease_analyses] ADD [structured_summary_json] nvarchar(max) NULL;
            END;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(alterLeaseAnalyses, cancellationToken);
    }
}

