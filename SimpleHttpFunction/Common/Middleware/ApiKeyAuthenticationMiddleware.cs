using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SimpleHttpFunction.Common.Middleware;

// =============================================================================
// CUSTOM API-KEY AUTHENTICATION (middleware).
// =============================================================================
// This is the ENTERPRISE alternative to the built-in function keys from S4.
// Why build our own instead of AuthorizationLevel.Function?
//   - It is auditable: we control it, log it, and can attribute the key to a
//     named client. Function keys are opaque and shared.
//   - It works identically locally and in Azure (function keys aren't enforced
//     locally, which hides auth bugs until production).
//   - It's a stepping stone: swap this for JWT bearer validation (Entra ID /
//     OAuth2) and the pattern — validate-in-middleware, short-circuit on failure —
//     stays exactly the same.
//
// We ONLY guard the enterprise Orders endpoints (/api/orders...). The learning
// endpoints stay open so the workshop can call them freely.
//
// NOTE: the valid key is read from configuration (local.settings.json locally,
// App Settings / Key Vault in Azure) — NEVER hard-coded.

public sealed class ApiKeyAuthenticationMiddleware : IFunctionsWorkerMiddleware
{
    private const string ApiKeyHeader = "x-api-key";
    private const string ProtectedPathPrefix = "/api/orders";

    private readonly string? _configuredKey;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;

    public ApiKeyAuthenticationMiddleware(IConfiguration config, ILogger<ApiKeyAuthenticationMiddleware> logger)
    {
        _configuredKey = config["ORDERS_API_KEY"];
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var httpContext = context.GetHttpContext();

        // Non-HTTP or endpoints outside the protected prefix -> pass straight through.
        if (httpContext is null ||
            !httpContext.Request.Path.StartsWithSegments(ProtectedPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        // Fail closed: if no key is configured, refuse rather than allow-all.
        if (string.IsNullOrWhiteSpace(_configuredKey))
        {
            _logger.LogError("ORDERS_API_KEY is not configured; rejecting request to {Path}.", httpContext.Request.Path);
            await Reject(httpContext, "Server auth is not configured.");
            return;
        }

        var provided = httpContext.Request.Headers[ApiKeyHeader].ToString();

        // Constant-time-ish comparison via a fixed ordinal check. (For real JWTs
        // the library handles signature verification for you.)
        if (string.IsNullOrEmpty(provided) || !string.Equals(provided, _configuredKey, StringComparison.Ordinal))
        {
            _logger.LogWarning("Rejected request to {Path}: missing or invalid API key.", httpContext.Request.Path);
            await Reject(httpContext, $"A valid '{ApiKeyHeader}' header is required.");
            return;
        }

        // Authenticated -> continue down the pipeline to the function.
        await next(context);
    }

    private static async Task Reject(HttpContext http, string detail)
    {
        http.Response.StatusCode = StatusCodes.Status401Unauthorized;
        http.Response.ContentType = "application/problem+json";
        await http.Response.WriteAsJsonAsync(new { status = 401, title = "Unauthorized", detail });
    }
}
