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
        int status = exception switch
        {
            ValidationException => 400,
            BadHttpRequestException bad => bad.StatusCode,
            UnauthorizedAccessException => 401,
            _ when IsUnavailable(exception) => 503,
            _ => 500
        };

        var factory = context.RequestServices.GetRequiredService<ProblemDetailsFactory>();
        ProblemDetails problem;
        if (exception is ValidationException validation)
        {
            var modelState = new ModelStateDictionary();
            string[] members = validation.ValidationResult?.MemberNames.Distinct().ToArray()
                ?? Array.Empty<string>();
            foreach (string member in members.Length == 0 ? new[] { "request" } : members)
                modelState.AddModelError(member,
                    validation.ValidationResult?.ErrorMessage ?? "The value is invalid.");
            problem = factory.CreateValidationProblemDetails(context, modelState, statusCode: status);
        }
        else
        {
            string? detail = exception is BadHttpRequestException { StatusCode: 404 or 409 } ? exception.Message : null;
            problem = factory.CreateProblemDetails(context, statusCode: status, detail: detail);
        }

        if (status >= 500)
            logger.LogError(exception, "Error al procesar la solicitud. TraceId: {TraceId}", context.TraceIdentifier);
        else
            logger.LogWarning("Solicitud rechazada con estado {Status}. TraceId: {TraceId}", status, context.TraceIdentifier);

        if (status == 401)
            context.Response.Headers.WWWAuthenticate = "Bearer";

        // Results.Problem utiliza IProblemDetailsService y su alternativa nativa de JSON.
        await Results.Problem(problem).ExecuteAsync(context);
        return true;
    }

    private static bool IsUnavailable(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is NpgsqlException { IsTransient: true })
                return true;
        }
        return false;
    }
}
