using LeaseLense.Application;
using LeaseLense.Infrastructure;
using LeaseLense.Infrastructure.Data;
using LeaseLense.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

using (var keyVaultLoggerFactory = LoggerFactory.Create(static b => b.AddConsole()))
{
    var keyVaultLogger = keyVaultLoggerFactory.CreateLogger("KeyVault");
    await KeyVaultSecretLoader.ApplyAsync(builder.Configuration, keyVaultLogger);
}

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.Configure<GmailSmtpOptions>(builder.Configuration.GetSection(GmailSmtpOptions.SectionName));
builder.Services.Configure<AzureDocumentIntelligenceOptions>(builder.Configuration.GetSection(AzureDocumentIntelligenceOptions.SectionName));
builder.Services.Configure<LlmFoundryFileLoggingOptions>(builder.Configuration.GetSection(LlmFoundryFileLoggingOptions.SectionName));
builder.Services.AddSingleton<ILlmFoundryErrorFileLog, LlmFoundryErrorFileLog>();
builder.Services.AddScoped<IEmailVerificationSender, GmailEmailVerificationSender>();
builder.Services.AddHttpClient<IAddressExtractionLlmClient, AzureFoundryAddressExtractionLlmClient>();
builder.Services.AddScoped<IDocumentExtractionService, AzureDocumentIntelligenceExtractionService>();
builder.Services.AddSingleton<IResidencyFallbackQueue, ResidencyFallbackQueue>();
builder.Services.AddHostedService<ResidencyFallbackWorker>();
builder.Services
    .AddIdentity<IdentityUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequiredLength = 8;
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddDefaultTokenProviders();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapGet("/health/db", async (LeaseLensDbContext dbContext, CancellationToken cancellationToken) =>
{
    try
    {
        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        return canConnect
            ? Results.Ok(new { status = "ok", database = "reachable" })
            : Results.Problem(
                title: "Database is unreachable",
                detail: "Connection failed for the configured SQL Server endpoint.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Database health check failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

var shouldEnsureIdentitySchema = builder.Configuration.GetValue<bool?>("IdentitySchema:AutoEnsureCreated")
    ?? app.Environment.IsDevelopment();
if (shouldEnsureIdentitySchema)
{
    using var scope = app.Services.CreateScope();
    var authDbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    await IdentitySchemaInitializer.EnsureCreatedAsync(authDbContext);
}


await app.RunAsync();
