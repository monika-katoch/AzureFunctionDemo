using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace SimpleHttpFunction.Common.Http;

// =============================================================================
// Small helpers to produce consistent responses across all functions.
// =============================================================================
// Using RFC 7807 "problem+json" for errors gives clients ONE predictable error
// shape everywhere, instead of every endpoint inventing its own.

public static class ApiResults
{
    public static IActionResult Problem(int status, string title, string? detail = null, object? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail
        };

        if (extensions is not null)
        {
            // Attach structured extra data (e.g. the list of validation errors).
            problem.Extensions["errors"] = extensions;
        }

        return new ObjectResult(problem)
        {
            StatusCode = status,
            ContentTypes = { "application/problem+json" }
        };
    }

    public static IActionResult ValidationProblem(IReadOnlyList<string> errors) =>
        Problem(StatusCodes.Status400BadRequest, "One or more validation errors occurred.", extensions: errors);
}
