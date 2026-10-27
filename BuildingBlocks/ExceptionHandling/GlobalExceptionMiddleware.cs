using BuildingBlocks.Exceptions;
using BuildingBlocks.Response;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace BuildingBlocks.ExceptionHandling;

/// <summary>
/// Converts exceptions thrown by handlers into a consistent ApiResponse body with the right status code.
/// Unknown exceptions become 500 without leaking internal details to the client.
/// </summary>
public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // The client disconnected; there is nobody to respond to.
            logger.LogInformation("Request {Path} was cancelled by the client", context.Request.Path);
        }
        catch (Exception exception)
        {
            var (statusCode, message, errors) = Map(exception);

            if (statusCode >= StatusCodes.Status500InternalServerError)
                logger.LogError(exception, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            else
                logger.LogWarning("Request {Method} {Path} failed with {StatusCode}: {Message}", context.Request.Method, context.Request.Path, statusCode, message);

            if (context.Response.HasStarted)
            {
                throw;
            }

            // Clear() also removes headers, so keep the correlation id set by CorrelationIdMiddleware.
            var correlationId = context.Response.Headers[CorrelationHeader].FirstOrDefault();
            context.Response.Clear();
            if (correlationId is not null)
            {
                context.Response.Headers[CorrelationHeader] = correlationId;
            }

            context.Response.StatusCode = statusCode;
            await context.Response.WriteAsJsonAsync(new ApiResponse<object>
            {
                Success = false,
                Message = message,
                Errors = errors,
                StatusCode = statusCode,
                CorrelationId = correlationId
            });
        }
    }

    public static (int StatusCode, string Message, IReadOnlyList<string>? Errors) Map(Exception exception) => exception switch
    {
        ValidationException validation => (
            StatusCodes.Status400BadRequest,
            "One or more validation errors occurred.",
            validation.Errors.Any()
                ? validation.Errors.Select(error => string.IsNullOrWhiteSpace(error.PropertyName) ? error.ErrorMessage : $"{error.PropertyName}: {error.ErrorMessage}").ToList()
                : [validation.Message]),
        KeyNotFoundException => (StatusCodes.Status404NotFound, exception.Message, null),
        ForbiddenException or UnauthorizedAccessException => (StatusCodes.Status403Forbidden, exception.Message, null),
        ConflictException => (StatusCodes.Status409Conflict, exception.Message, null),
        _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred. Please try again later.", null)
    };
}
