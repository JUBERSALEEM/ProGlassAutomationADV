using System.Text.Json;

namespace ProGlass.Saas.Api.Middleware;

/// <summary>
/// The last resort: an unhandled exception becomes the shared error
/// envelope (API design §0) with a correlation id, and NOTHING internal
/// (stack traces, SQL, connection strings) reaches the client. The
/// exception is logged with the same correlation id so an operator can
/// join the response to the log line.
/// </summary>
public sealed class GlobalExceptionHandlerMiddleware(
    RequestDelegate next, ILogger<GlobalExceptionHandlerMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (
            context.RequestAborted.IsCancellationRequested)
        {
            // The client went away — not a server fault; no 500 noise.
            throw;
        }
        catch (Exception ex)
        {
            var correlationId = Guid.NewGuid().ToString("N")[..12];
            logger.LogError(ex,
                "unhandled exception on {Method} {Path} [{CorrelationId}]",
                context.Request.Method, context.Request.Path, correlationId);

            context.Response.Clear();
            context.Response.StatusCode =
                StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                code = "api.internal_error",
                message = "An unexpected error occurred. Quote "
                    + $"reference {correlationId} to support.",
                correlationId
            }));
        }
    }
}
