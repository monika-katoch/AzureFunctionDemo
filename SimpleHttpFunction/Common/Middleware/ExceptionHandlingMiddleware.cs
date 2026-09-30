using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace SimpleHttpFunction.Common.Middleware;

// =============================================================================
// GLOBAL EXCEPTION HANDLER (middleware).
// =============================================================================
// Middleware runs AROUND every function invocation, like a pipeline. This one
// catches any unhandled exception so we NEVER leak stack traces to callers and
// always return a clean 500 problem+json. Centralizing this means individual
// functions don't each need a try/catch for the "something broke" case.
//
// Q (participant): "Isn't this just ASP.NET middleware?"
// A: Same idea, but this is the Functions WORKER pipeline (IFunctionsWorkerMiddleware),
//    which wraps the function execution itself — so it works for every trigger
//    type, not only HTTP.

public sealed class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger) => _logger = logger;

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context); // run the next middleware / the function itself
        }
        catch (Exception ex)
        {
            // Log the FULL detail server-side (with the invocation id for tracing)...
            _logger.LogError(ex, "Unhandled exception in {Function} (invocation {InvocationId})",
                context.FunctionDefinition.Name, context.InvocationId);

            // ...but return only a safe, generic message to the caller.
            var httpContext = context.GetHttpContext();
            if (httpContext is not null)
            {
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                httpContext.Response.ContentType = "application/problem+json";
                await httpContext.Response.WriteAsJsonAsync(new
                {
                    status = 500,
                    title = "An unexpected error occurred.",
                    // Handing back the invocation id lets support correlate the
                    // caller's report with our logs without exposing internals.
                    traceId = context.InvocationId
                });
            }
        }
    }
}
