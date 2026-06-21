using EventServiceApi.Exceptions;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace EventServiceApi.Middleware;

public sealed class ExceptionHandlingMiddleware : IMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        // По умолчанию 500
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var detail = "Произошла непредвиденная ошибка.";

        // Маппинг типов исключений -> HTTP статус
        switch (ex)
        {
            case ValidationException ve:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Bad Request";
                detail = ve.Message;
                break;

            case ArgumentException ae:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Bad Request";
                detail = ae.Message;
                break;

            case KeyNotFoundException knf:
                statusCode = StatusCodes.Status404NotFound;
                title = "Not Found";
                detail = knf.Message;
                break;

            case NotFoundException nf:
                statusCode = StatusCodes.Status404NotFound;
                title = "Not Found";
                detail = nf.Message;
                break;

            case NoAvailableSeatsException nase:
                statusCode = StatusCodes.Status409Conflict;
                title = "Conflict";
                detail = nase.Message;
                break;
        }

        // Логирование: 5xx как Error, 4xx как Warning
        if (statusCode >= 500)
            _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);
        else
            _logger.LogWarning(ex, "Request error. TraceId={TraceId}", context.TraceIdentifier);

        var problem = new ProblemDetails
        {
            Type = "about:blank",
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = context.Request.Path
        };

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}