using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace backend.Middleware;

/// <summary>Traduce las excepciones mediante el manejador nativo de ASP.NET Core.</summary>
public sealed class ApiExceptionHandler(ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        int status = MapStatusCode(exception);

        var factory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        string? detail = exception is BadHttpRequestException { StatusCode: StatusCodes.Status404NotFound or StatusCodes.Status409Conflict }
            ? exception.Message
            : null;

        ProblemDetails problem = exception is ValidationException validation
            ? CreateValidationProblem(context, factory, validation, status)
            : factory.CreateProblemDetails(context, statusCode: status, detail: detail);

        LogOutcome(exception, status, context.TraceIdentifier);

        if (status == StatusCodes.Status401Unauthorized)
        {
            context.Response.Headers.WWWAuthenticate = "Bearer";
        }

        // Results.Problem usa IProblemDetailsService y su fallback JSON nativo.
        await Results.Problem(problem).ExecuteAsync(context);
        return true;
    }

    private static ProblemDetails CreateValidationProblem(
        HttpContext context, ProblemDetailsFactory factory, ValidationException validation, int status)
    {
        var modelState = new ModelStateDictionary();
        string[] members = validation.ValidationResult?.MemberNames.Distinct().ToArray()
            ?? Array.Empty<string>();
        foreach (string member in members.Length == 0 ? new[] { "request" } : members)
        {
            modelState.AddModelError(member,
                validation.ValidationResult?.ErrorMessage ?? "The value is invalid.");
        }
        return factory.CreateValidationProblemDetails(context, modelState, statusCode: status);
    }

    private void LogOutcome(Exception exception, int status, string? traceId)
    {
        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Error al procesar la solicitud. TraceId: {TraceId}", traceId);
        }
        else
        {
            logger.LogWarning("Solicitud rechazada con estado {Status}. TraceId: {TraceId}", status, traceId);
        }
    }

    private static int MapStatusCode(Exception exception) => exception switch
    {
        ValidationException            => StatusCodes.Status400BadRequest,
        BadHttpRequestException bad    => bad.StatusCode,
        UnauthorizedAccessException    => StatusCodes.Status401Unauthorized,
        _ when IsTransientPostgres(exception) => StatusCodes.Status503ServiceUnavailable,
        _                              => StatusCodes.Status500InternalServerError
    };

    private static bool IsTransientPostgres(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true })
                return true;
        }
        return false;
    }
}
