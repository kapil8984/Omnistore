using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Domain.Exceptions;

namespace OrderInventory.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        int statusCode = (int)HttpStatusCode.InternalServerError;
        string title = "An internal server error occurred.";
        string detail = exception.Message;

        switch (exception)
        {
            case AppException appEx:
                statusCode = appEx.StatusCode;
                title = appEx switch
                {
                    NotFoundException => "Resource Not Found",
                    BadRequestException => "Bad Request",
                    ConflictException => "Conflict",
                    UnauthorizedException => "Unauthorized",
                    ForbiddenException => "Forbidden",
                    _ => "Application Error"
                };
                _logger.LogWarning(exception, "Handled business domain exception: {Title} ({StatusCode})", title, statusCode);
                break;

            case DbUpdateConcurrencyException dbEx:
                statusCode = (int)HttpStatusCode.Conflict;
                title = "Concurrency Conflict";
                detail = "The record you attempted to edit was modified by another user after you got the original value. The edit operation was canceled.";
                _logger.LogWarning(dbEx, "Database concurrency conflict detected.");
                break;

            case UnauthorizedAccessException:
                statusCode = (int)HttpStatusCode.Unauthorized;
                title = "Unauthorized";
                _logger.LogWarning(exception, "Unauthorized access.");
                break;

            case KeyNotFoundException:
                statusCode = (int)HttpStatusCode.NotFound;
                title = "Not Found";
                _logger.LogWarning(exception, "Resource not found.");
                break;

            default:
                _logger.LogError(exception, "Unhandled system exception occurred.");
                detail = "An unexpected error occurred. Please contact system support.";
                break;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }
}
