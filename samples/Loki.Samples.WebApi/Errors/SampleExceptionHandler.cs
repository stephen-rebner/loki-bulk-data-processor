using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Loki.Samples.WebApi.Errors;

/// <summary>
/// Writes every failure as RFC 9457 problem details (<see cref="ProblemMapping"/>) and logs it without the exception object:
/// driver messages can contain row values, so only the exception types are logged, as Loki does itself.
/// </summary>
internal sealed class SampleExceptionHandler(IProblemDetailsService problemDetails, ILogger<SampleExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        Problem problem = ProblemMapping.For(exception, httpContext.RequestAborted.IsCancellationRequested);
        Log(httpContext, exception, problem);
        httpContext.Response.StatusCode = problem.StatusCode;
        ProblemDetails details = new() { Status = problem.StatusCode, Title = problem.Title, Detail = problem.Detail };
        if (!await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = details }))
        {
            // No writer accepted the request (for example an Accept header without JSON): write it anyway and report it handled,
            // so the middleware never logs the exception object (its driver message can quote row values).
            await Results.Problem(details).ExecuteAsync(httpContext);
        }

        return true;
    }

    private void Log(HttpContext httpContext, Exception exception, Problem problem)
    {
        // The middleware clears the endpoint before it calls the handlers; the feature keeps the one that failed.
        string endpoint = httpContext.Features.Get<IExceptionHandlerFeature>()?.Endpoint?.DisplayName ?? "Unknown endpoint";
        LogLevel level = problem.StatusCode >= StatusCodes.Status500InternalServerError ? LogLevel.Error : LogLevel.Information;
        logger.Log(level, "{Endpoint} answered {StatusCode} after {ExceptionType} (inner {InnerExceptionType})",
            endpoint, problem.StatusCode, exception.GetType().FullName, exception.InnerException?.GetType().FullName);
    }
}
