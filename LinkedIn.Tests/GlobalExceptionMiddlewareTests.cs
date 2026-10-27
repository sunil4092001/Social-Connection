using System.Text.Json;
using BuildingBlocks.ExceptionHandling;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Response;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace LinkedIn.Tests;

public sealed class GlobalExceptionMiddlewareTests
{
    [Fact]
    public void Map_returns_400_with_field_errors_for_validation_exception()
    {
        var exception = new ValidationException([new ValidationFailure("Title", "Title is required.")]);

        var (statusCode, _, errors) = GlobalExceptionMiddleware.Map(exception);

        Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
        Assert.Equal(["Title: Title is required."], errors);
    }

    [Fact]
    public void Map_returns_404_for_key_not_found()
    {
        var (statusCode, message, _) = GlobalExceptionMiddleware.Map(new KeyNotFoundException("Requirement was not found."));

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        Assert.Equal("Requirement was not found.", message);
    }

    [Fact]
    public void Map_returns_403_for_forbidden_and_unauthorized_access()
    {
        Assert.Equal(StatusCodes.Status403Forbidden, GlobalExceptionMiddleware.Map(new ForbiddenException("No.")).StatusCode);
        Assert.Equal(StatusCodes.Status403Forbidden, GlobalExceptionMiddleware.Map(new UnauthorizedAccessException("No.")).StatusCode);
    }

    [Fact]
    public void Map_returns_409_for_conflict()
    {
        Assert.Equal(StatusCodes.Status409Conflict, GlobalExceptionMiddleware.Map(new ConflictException("Duplicate.")).StatusCode);
    }

    [Fact]
    public void Map_hides_details_of_unexpected_exceptions()
    {
        var (statusCode, message, _) = GlobalExceptionMiddleware.Map(new InvalidOperationException("Unable to resolve service for type X"));

        Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        Assert.DoesNotContain("resolve service", message);
    }

    [Fact]
    public async Task Invoke_writes_api_response_body_with_status_code()
    {
        var middleware = new GlobalExceptionMiddleware(
            _ => throw new ConflictException("You have already responded to this requirement."),
            NullLogger<GlobalExceptionMiddleware>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Response.Headers["X-Correlation-Id"] = "corr-123";

        await middleware.Invoke(context);

        Assert.Equal(StatusCodes.Status409Conflict, context.Response.StatusCode);
        Assert.Equal("corr-123", context.Response.Headers["X-Correlation-Id"].ToString());
        context.Response.Body.Position = 0;
        var body = await JsonSerializer.DeserializeAsync<ApiResponse<object>>(context.Response.Body, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal(409, body.StatusCode);
        Assert.Equal("You have already responded to this requirement.", body.Message);
        Assert.Equal("corr-123", body.CorrelationId);
    }

    [Fact]
    public void ApiResponseFactory_Failure_flattens_error_dictionary()
    {
        var response = ApiResponseFactory.Failure<object>("Invalid.", new Dictionary<string, string[]>
        {
            ["Email"] = ["Email is required.", "Email is invalid."]
        });

        Assert.Equal(["Email: Email is required.", "Email: Email is invalid."], response.Errors);
    }
}
