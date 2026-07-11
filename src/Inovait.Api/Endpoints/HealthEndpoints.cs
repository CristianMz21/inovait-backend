using Inovait.Infrastructure.Persistence;

namespace Inovait.Api.Endpoints;

public static class HealthEndpoints
{
    public static IEndpointRouteBuilder MapHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/health/live", () => Results.Ok(new { status = "live" }));

        app.MapGet("/health/ready", async (HttpContext context, CancellationToken cancellationToken) =>
        {
            var db = context.RequestServices.GetService<InovaitDbContext>();
            if (db is null)
            {
                return Results.Json(new { status = "degraded" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            try
            {
                var canConnect = await db.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? Results.Ok(new { status = "ready" })
                    : Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
            catch
            {
                return Results.Json(new { status = "unavailable" }, statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        return app;
    }
}
