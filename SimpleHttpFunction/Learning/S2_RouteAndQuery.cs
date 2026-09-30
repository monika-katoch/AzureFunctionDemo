using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace SimpleHttpFunction.Learning;

// =============================================================================
// STEP 2 — Reading input from the URL: route parameters and query strings.
// =============================================================================
//
// Two common ways a caller passes data in a GET request:
//   1. Route parameter  ->  /api/greet/Monika          ("Monika" is part of the path)
//   2. Query string     ->  /api/greet/Monika?loud=true (key=value after the '?')
//
// Rule of thumb:
//   - Route params identify *which* resource (the "noun").
//   - Query params tune *how* you return it (filter, sort, page, flags).
//
public class S2_RouteAndQuery
{
    [Function("S2_Greet")]
    public IActionResult Run(
        // "Route =" OVERRIDES the default route. {name} is a placeholder that gets
        // bound to the method parameter of the same name below.
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "greet/{name}")] HttpRequest req,
        string name) // <-- the runtime fills this in from the {name} route segment.
    {
        // Query strings are NOT part of the route. Read them from req.Query.
        // Everything from the URL is a string and always attacker-controlled,
        // so treat it as untrusted input and parse defensively.
        bool loud = bool.TryParse(req.Query["loud"], out var l) && l;

        var greeting = $"Hello, {name}!";
        if (loud)
        {
            greeting = greeting.ToUpperInvariant();
        }

        // Returning an anonymous object via JsonResult -> Content-Type application/json.
        return new JsonResult(new { message = greeting, loud });
    }
}
