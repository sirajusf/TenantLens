using Microsoft.EntityFrameworkCore;

namespace LeaseLense.Infrastructure.Data;

public static class IdentitySchemaInitializer
{
    public static async Task EnsureCreatedAsync(AuthDbContext dbContext, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetRoles](
                    [Id] nvarchar(450) NOT NULL PRIMARY KEY,
                    [Name] nvarchar(256) NULL,
                    [NormalizedName] nvarchar(256) NULL,
                    [ConcurrencyStamp] nvarchar(max) NULL
                );
            END;

            IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetUsers](
                    [Id] nvarchar(450) NOT NULL PRIMARY KEY,
                    [UserName] nvarchar(256) NULL,
                    [NormalizedUserName] nvarchar(256) NULL,
                    [Email] nvarchar(256) NULL,
                    [NormalizedEmail] nvarchar(256) NULL,
                    [EmailConfirmed] bit NOT NULL DEFAULT 0,
                    [PasswordHash] nvarchar(max) NULL,
                    [SecurityStamp] nvarchar(max) NULL,
                    [ConcurrencyStamp] nvarchar(max) NULL,
                    [PhoneNumber] nvarchar(max) NULL,
                    [PhoneNumberConfirmed] bit NOT NULL DEFAULT 0,
                    [TwoFactorEnabled] bit NOT NULL DEFAULT 0,
                    [LockoutEnd] datetimeoffset(7) NULL,
                    [LockoutEnabled] bit NOT NULL DEFAULT 1,
                    [AccessFailedCount] int NOT NULL DEFAULT 0
                );
            END;

            IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetRoleClaims](
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [RoleId] nvarchar(450) NOT NULL,
                    [ClaimType] nvarchar(max) NULL,
                    [ClaimValue] nvarchar(max) NULL,
                    CONSTRAINT [FK_AspNetRoleClaims_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE
                );
            END;

            IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetUserClaims](
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId] nvarchar(450) NOT NULL,
                    [ClaimType] nvarchar(max) NULL,
                    [ClaimValue] nvarchar(max) NULL,
                    CONSTRAINT [FK_AspNetUserClaims_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
                );
            END;

            IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetUserLogins](
                    [LoginProvider] nvarchar(128) NOT NULL,
                    [ProviderKey] nvarchar(128) NOT NULL,
                    [ProviderDisplayName] nvarchar(max) NULL,
                    [UserId] nvarchar(450) NOT NULL,
                    CONSTRAINT [PK_AspNetUserLogins] PRIMARY KEY ([LoginProvider], [ProviderKey]),
                    CONSTRAINT [FK_AspNetUserLogins_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
                );
            END;

            IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetUserRoles](
                    [UserId] nvarchar(450) NOT NULL,
                    [RoleId] nvarchar(450) NOT NULL,
                    CONSTRAINT [PK_AspNetUserRoles] PRIMARY KEY ([UserId], [RoleId]),
                    CONSTRAINT [FK_AspNetUserRoles_AspNetRoles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [dbo].[AspNetRoles]([Id]) ON DELETE CASCADE,
                    CONSTRAINT [FK_AspNetUserRoles_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
                );
            END;

            IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[AspNetUserTokens](
                    [UserId] nvarchar(450) NOT NULL,
                    [LoginProvider] nvarchar(128) NOT NULL,
                    [Name] nvarchar(128) NOT NULL,
                    [Value] nvarchar(max) NULL,
                    CONSTRAINT [PK_AspNetUserTokens] PRIMARY KEY ([UserId], [LoginProvider], [Name]),
                    CONSTRAINT [FK_AspNetUserTokens_AspNetUsers_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[AspNetUsers]([Id]) ON DELETE CASCADE
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'RoleNameIndex' AND object_id = OBJECT_ID(N'dbo.AspNetRoles'))
                CREATE UNIQUE INDEX [RoleNameIndex] ON [dbo].[AspNetRoles] ([NormalizedName]) WHERE [NormalizedName] IS NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetRoleClaims_RoleId' AND object_id = OBJECT_ID(N'dbo.AspNetRoleClaims'))
                CREATE INDEX [IX_AspNetRoleClaims_RoleId] ON [dbo].[AspNetRoleClaims] ([RoleId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'EmailIndex' AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
                CREATE INDEX [EmailIndex] ON [dbo].[AspNetUsers] ([NormalizedEmail]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UserNameIndex' AND object_id = OBJECT_ID(N'dbo.AspNetUsers'))
                CREATE UNIQUE INDEX [UserNameIndex] ON [dbo].[AspNetUsers] ([NormalizedUserName]) WHERE [NormalizedUserName] IS NOT NULL;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserClaims_UserId' AND object_id = OBJECT_ID(N'dbo.AspNetUserClaims'))
                CREATE INDEX [IX_AspNetUserClaims_UserId] ON [dbo].[AspNetUserClaims] ([UserId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserLogins_UserId' AND object_id = OBJECT_ID(N'dbo.AspNetUserLogins'))
                CREATE INDEX [IX_AspNetUserLogins_UserId] ON [dbo].[AspNetUserLogins] ([UserId]);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AspNetUserRoles_RoleId' AND object_id = OBJECT_ID(N'dbo.AspNetUserRoles'))
                CREATE INDEX [IX_AspNetUserRoles_RoleId] ON [dbo].[AspNetUserRoles] ([RoleId]);
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
