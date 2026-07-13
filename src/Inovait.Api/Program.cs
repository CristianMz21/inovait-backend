using System.Text.Json;
using Inovait.Api.Endpoints;
using Inovait.Api.Errors;
using Inovait.Api.Reads;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;
using Inovait.Infrastructure.Persistence.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
});

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? Array.Empty<string>();
var connectionString = builder.Configuration.GetConnectionString("InovaitDatabase");
if (!string.IsNullOrWhiteSpace(connectionString))
{
    builder.Services.AddInovaitInfrastructure(connectionString);
    builder.Services.AddScoped<CatalogReadService>();
    builder.Services.AddScoped<EnrollmentReadService>();
    builder.Services.AddScoped<TeacherContractReadService>();
    builder.Services.AddScoped<ReferenceLookupService>();
    builder.Services.AddScoped<StudentHistoryReadService>();
    builder.Services.AddScoped<ReportReadService>();
}

builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        if (context.ProblemDetails.Extensions.ContainsKey("code"))
        {
            return;
        }

        context.ProblemDetails.Extensions["code"] = context.ProblemDetails.Status switch
        {
            StatusCodes.Status400BadRequest => "invalid_request",
            StatusCodes.Status404NotFound => "resource_not_found",
            StatusCodes.Status409Conflict => "history_conflict",
            StatusCodes.Status422UnprocessableEntity => "business_rule_violation",
            _ => "internal_error",
        };
    };
});
builder.Services.AddExceptionHandler<BadRequestExceptionHandler>();
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(connectionString))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AcademicConfigurationStartupCheck>()
        .EnsurePresentAsync();

    // Fictitious LOCAL-EVALUATION demo data (never applied in Production without an explicit
    // flag): --seed-demo on the command line, INOVAIT_SEED_DEMO=true in the environment, or
    // Inovait:SeedDemoData=true in configuration while running in Development.
    var seedDemoData = args.Contains("--seed-demo")
        || string.Equals(Environment.GetEnvironmentVariable("INOVAIT_SEED_DEMO"), "true", StringComparison.OrdinalIgnoreCase)
        || (app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("Inovait:SeedDemoData"));
    if (seedDemoData)
    {
        await DemoDataSeeder.ApplyAsync(scope.ServiceProvider.GetRequiredService<InovaitDbContext>());
        app.Logger.LogInformation("Demo data: applied (fictitious local-evaluation dataset; see docs/SEED_DATA.md).");
    }
    else
    {
        app.Logger.LogInformation("Demo data: skipped (no --seed-demo / INOVAIT_SEED_DEMO / Inovait:SeedDemoData flag).");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/", () => new { service = "Inovait API", status = "ready" });

app.MapHealthEndpoints();

if (!string.IsNullOrWhiteSpace(connectionString))
{
    app.MapCatalogEndpoints();
    app.MapEnrollmentEndpoints();
    app.MapTeacherContractEndpoints();
    app.MapStudentHistoryEndpoints();
    app.MapReportEndpoints();
}

app.UseHttpsRedirection();
app.UseCors();
app.UseExceptionHandler();

await app.RunAsync();

public partial class Program
{
    protected Program() { }
}
