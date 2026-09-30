using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SimpleHttpFunction.Common.Http;
using SimpleHttpFunction.Orders.Contracts;
using SimpleHttpFunction.Orders.Domain;
using SimpleHttpFunction.Orders.Services;
using SimpleHttpFunction.Orders.Validation;

namespace SimpleHttpFunction.Orders.Functions;

// =============================================================================
// ENTERPRISE ENDPOINTS — the "Order Intake API".
// =============================================================================
// A realistic, RESTful surface:
//     POST   /api/orders          -> create (idempotent, protected by API key)
//     GET    /api/orders/{id}      -> fetch one
//     GET    /api/orders          -> list
//
// These functions are intentionally THIN. All they do is: read HTTP, delegate to
// the service, map the result back to HTTP. No business rules live here.
// Auth is handled upstream by ApiKeyAuthenticationMiddleware, so we don't repeat
// it in every method.

public sealed class OrdersFunctions
{
    private readonly IOrderService _orders;
    private readonly ILogger<OrdersFunctions> _logger;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public OrdersFunctions(IOrderService orders, ILogger<OrdersFunctions> logger)
    {
        _orders = orders;
        _logger = logger;
    }

    // -------------------------------------------------------------------------
    // CREATE
    // -------------------------------------------------------------------------
    // AuthorizationLevel.Anonymous here means "the function host does not require
    // a function key" — because OUR OWN API-key middleware is the gate. Don't
    // stack both; pick one clear auth story.
    [Function("Orders_Create")]
    public async Task<IActionResult> Create(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "orders")] HttpRequest req,
        CancellationToken ct)
    {
        // IDEMPOTENCY KEY: the caller sends a unique key per logical order (often
        // a GUID they generate). Retrying with the SAME key must not create a
        // second order. This is essential for reliable, stateless APIs where the
        // client may retry after a timeout without knowing if the first call
        // actually succeeded.
        var idempotencyKey = req.Headers["Idempotency-Key"].ToString();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return ApiResults.Problem(StatusCodes.Status400BadRequest,
                "Missing required header.",
                "Provide an 'Idempotency-Key' header (a unique value per order).");
        }

        // Parse the body defensively.
        CreateOrderRequest? request;
        try
        {
            request = await JsonSerializer.DeserializeAsync<CreateOrderRequest>(req.Body, JsonOpts, ct);
        }
        catch (JsonException)
        {
            return ApiResults.Problem(StatusCodes.Status400BadRequest, "Malformed JSON body.");
        }

        // Validate before touching any business logic.
        var errors = CreateOrderRequestValidator.Validate(request);
        if (errors.Count > 0)
        {
            return ApiResults.ValidationProblem(errors);
        }

        var result = await _orders.CreateAsync(request!, idempotencyKey, ct);
        var body = ToResponse(result.Order);

        // Distinguish "created new" from "you already sent this".
        if (result.WasDuplicate)
        {
            // 200 OK: the resource already existed; we return it unchanged.
            return new OkObjectResult(body);
        }

        // 201 Created + a Location header pointing at the new resource — the
        // correct REST response for a successful create.
        return new CreatedResult($"/api/orders/{result.Order.Id}", body);
    }

    // -------------------------------------------------------------------------
    // GET ONE
    // -------------------------------------------------------------------------
    [Function("Orders_GetById")]
    public async Task<IActionResult> GetById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders/{id}")] HttpRequest req,
        string id,
        CancellationToken ct)
    {
        var order = await _orders.GetAsync(id, ct);
        if (order is null)
        {
            // 404 Not Found — the correct answer for a missing resource.
            return ApiResults.Problem(StatusCodes.Status404NotFound, "Order not found.", $"No order with id '{id}'.");
        }
        return new OkObjectResult(ToResponse(order));
    }

    // -------------------------------------------------------------------------
    // LIST
    // -------------------------------------------------------------------------
    [Function("Orders_List")]
    public async Task<IActionResult> List(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "orders")] HttpRequest req,
        CancellationToken ct)
    {
        var orders = await _orders.ListAsync(ct);
        return new OkObjectResult(orders.Select(ToResponse));
    }

    // Map internal domain -> public response DTO. Kept private and explicit so the
    // wire format is never an accident of the domain model's internals.
    private static OrderResponse ToResponse(Order o) => new(
        o.Id,
        o.CustomerId,
        o.Status.ToString(),
        o.TotalAmount,
        o.Currency,
        o.CreatedAtUtc,
        o.Lines.Select(l => new OrderLineResponse(l.Sku, l.Quantity, l.UnitPrice, l.Quantity * l.UnitPrice)).ToList());
}
