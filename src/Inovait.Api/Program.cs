using System.Text.Json;
using Inovait.Infrastructure;
using Inovait.Infrastructure.Persistence;

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
}

builder.Services.AddProblemDetails();
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
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapGet("/", () => new { service = "Inovait API", status = "ready" });

app.UseHttpsRedirection();
app.UseCors();
app.UseExceptionHandler();

app.Run();

public partial class Program;
