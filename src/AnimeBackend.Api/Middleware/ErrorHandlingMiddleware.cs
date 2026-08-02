using System.Text.Json;
using AnimeBackend.Domain;

namespace AnimeBackend.Api.Middleware;

// Every failure leaves as `{ "message": "..." }` — the frontend renders that
// text verbatim. Business errors are 4xx on purpose: the frontend retries 429
// and 5xx three times, so 5xx is reserved for genuine crashes.
public sealed class ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nothing to answer.
        }
        catch (SourceUnavailableException ex)
        {
            logger.LogWarning("Источник недоступен: {Message}", ex.Message);
            await WriteAsync(context, StatusCodes.Status424FailedDependency, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, ex.Message);
        }
        catch (DomainException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Необработанная ошибка на {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteAsync(context, StatusCodes.Status500InternalServerError,
                "Внутренняя ошибка сервера");
        }
    }

    private static async Task WriteAsync(HttpContext context, int statusCode, string message)
    {
        if (context.Response.HasStarted) return;
        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        await context.Response.WriteAsync(JsonSerializer.Serialize(new { message }));
    }
}
