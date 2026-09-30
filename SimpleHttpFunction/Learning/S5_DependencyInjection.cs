using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace SimpleHttpFunction.Learning;

// =============================================================================
// STEP 5 — Dependency Injection (DI), logging, and configuration.
// =============================================================================
//
// Even though a function is stateless PER REQUEST, the app still boots up once
// (see Program.cs) and wires up shared services through a DI container — exactly
// like ASP.NET Core. You ask for what you need in the CONSTRUCTOR and the
// runtime hands it to you. This keeps functions testable and decoupled.
//
// Q (participant): "If it's stateless, how can it have a constructor / services?"
// A: "Stateless" is about not remembering data BETWEEN CALLS. Within a single
//    call you still get a fresh, fully wired object graph. Services like a DB
//    client are safe to share because they hold no per-user request state.
//
// A tiny service to inject. In real apps this would be a repository, an HTTP
// client, a cache, etc.
public interface IGreetingService
{
    string Greet(string name);
}

public sealed class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Greetings, {name}. (served by DI)";
}

public class S5_DependencyInjection
{
    private readonly IGreetingService _greetings;
    private readonly IConfiguration _config;
    private readonly ILogger<S5_DependencyInjection> _logger;

    // The runtime injects all three because we register them in Program.cs
    // (IGreetingService) or they are built in (IConfiguration, ILogger).
    public S5_DependencyInjection(
        IGreetingService greetings,
        IConfiguration config,
        ILogger<S5_DependencyInjection> logger)
    {
        _greetings = greetings;
        _config = config;
        _logger = logger;
    }

    [Function("S5_DI")]
    public IActionResult Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "di/{name}")] HttpRequest req,
        string name)
    {
        // Structured logging: the {Name} token is captured as a searchable field
        // in Application Insights, not just baked into a string. Prefer this over
        // string interpolation for anything you'll want to query later.
        _logger.LogInformation("Greeting requested for {Name}", name);

        // Configuration comes from local.settings.json (locally) or App Settings /
        // Key Vault (in Azure). NEVER hard-code secrets or environment specifics.
        var environment = _config["APP_ENVIRONMENT"] ?? "local";

        return new OkObjectResult(new
        {
            message = _greetings.Greet(name),
            environment
        });
    }
}
