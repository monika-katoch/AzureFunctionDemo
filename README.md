# Stateless Azure Functions — Workshop

A single runnable **.NET 8 isolated-worker** Functions app that teaches HTTP-triggered
("stateless") Azure Functions from the absolute basics up to an enterprise-grade,
real-world API. Everything runs on your laptop — no Azure subscription needed.

> **What "stateless" means:** a function keeps **no memory between calls**. Azure can
> run many copies at once and scale to zero. Any copy may serve any request, so state
> must live **outside** the function (DB, queue, cache) — never in a static field.

---

## Run it

```bash
cd SimpleHttpFunction
func start --port 7218
```

Then use **`SimpleHttpFunction/Workshop.http`** (VS 2022 / VS Code REST Client) or the
`curl` commands below. All routes are under `http://localhost:7218/api`.

---

## Track A — Learning (one concept per function)

| Step | Endpoint | Concept |
|------|----------|---------|
| S1 | `GET /S1_HelloWorld` | The minimum: `[Function]` + `[HttpTrigger]` + `IActionResult` |
| S2 | `GET /greet/{name}?loud=` | Route params vs. query strings |
| S3 | `POST /sum` | Request body, JSON model binding, proper status codes (200/400) |
| S4 | `GET /secure/{public\|key\|admin}` | **Authorization levels** (see below) |
| S5 | `GET /di/{name}` | Dependency injection, structured logging, configuration |

```bash
curl http://localhost:7218/api/S1_HelloWorld
curl "http://localhost:7218/api/greet/Sagar?loud=true"
curl -X POST http://localhost:7218/api/sum -d '{"numbers":[3,4,5]}'
```

### Authorization of stateless functions (S4)

Each `[HttpTrigger]` declares an `AuthorizationLevel` — the **built-in, key-based** gate
the host enforces before your code runs:

| Level | Who can call | Key sent as |
|-------|--------------|-------------|
| `Anonymous` | Anyone | — |
| `Function`  | Holder of a function/host key | `x-functions-key: <key>` header **or** `?code=<key>` |
| `Admin`     | Holder of the master key | same |

**What function keys are NOT:** they're a shared secret on the URL. They prove the caller
*has a key* — not *who* they are. No identity, no expiry, no scopes, easy to leak.
Locally the host **does not enforce** them, so `Function`/`Admin` endpoints are open on
your machine — a dev convenience, not "open in prod."

**For real enterprise auth use identity, not just a key:**
- **Microsoft Entra ID (Azure AD) / OAuth2 JWT bearer tokens** ← preferred
- **Azure API Management** in front (keys, rate limiting, WAF)
- **A custom middleware** that validates a per-client API key (auditable, revocable) —
  which is exactly what Track B demonstrates.

---

## Track B — Enterprise "Order Intake API" (the real use case)

A realistic order-submission API, layered the way production code is:

```
Orders/
  Contracts/    public request/response DTOs (the wire contract)
  Validation/   input validation, unit-testable in isolation
  Domain/       internal Order model (server owns id, totals, status, timestamps)
  Services/     business logic + idempotency (knows nothing about HTTP)
  Repository/   storage behind an interface (swap in-memory -> Cosmos/SQL, no other changes)
  Functions/    thin HTTP endpoints that just delegate
Common/
  Middleware/   ExceptionHandling (global 500s) + ApiKeyAuthentication (guards /api/orders)
  Http/         consistent RFC 7807 problem+json error responses
```

| Endpoint | Behaviour |
|----------|-----------|
| `POST /orders` | Create. Requires `x-api-key` **and** an `Idempotency-Key` header. `201` on create, `200` if the key was already used. |
| `GET /orders/{id}` | Fetch one (`404` if missing). |
| `GET /orders` | List all. |

Enterprise practices shown: DTO/domain separation, layered DI, custom API-key auth in
middleware (the swap-in point for JWT), **idempotency** (safe client retries),
validation with clear per-field errors, and uniform `problem+json` errors.

```bash
KEY="x-api-key: local-demo-key-12345"
BODY='{"customerId":"CUST-100","currency":"USD","lines":[{"sku":"A1","quantity":2,"unitPrice":9.5}]}'

# No key -> 401
curl -i -X POST http://localhost:7218/api/orders -H 'Idempotency-Key: k1' -d "$BODY"

# With key -> 201 Created
curl -i -X POST http://localhost:7218/api/orders -H "$KEY" -H 'Idempotency-Key: order-1' -d "$BODY"

# Same Idempotency-Key again -> 200 OK, same order (no duplicate)
curl -i -X POST http://localhost:7218/api/orders -H "$KEY" -H 'Idempotency-Key: order-1' -d "$BODY"

curl http://localhost:7218/api/orders -H "$KEY"
```

---

## Anticipated participant questions

- **Is this AWS Lambda?** Same idea — event-driven, pay-per-use, auto-scaling functions.
- **If it's stateless, how does it have a constructor and DI?** "Stateless" = no memory
  *between* calls. Within one call you still get a fully wired object graph.
- **Why not a `static Dictionary` for storage?** Multiple instances each get their own —
  reads would randomly miss. State must be external. The in-memory repo here is a demo
  cheat; the `IOrderRepository` seam is where a real DB plugs in.
- **Why an Idempotency-Key?** A stateless endpoint may receive the same request twice
  (client retry, at-least-once queue delivery). The key makes "create" safe to repeat.
- **Function key vs. our API key?** The function key isn't enforced locally and carries no
  identity; the custom middleware is auditable, revocable, and identical local vs. Azure —
  and is the drop-in point for real JWT auth.

## Notes for deploying to Azure (beyond this local demo)

- Replace `InMemoryOrderRepository` with a Cosmos DB / SQL implementation.
- Move `ORDERS_API_KEY` to **App Settings / Key Vault** (never in source).
- Prefer **Entra ID JWT** or **API Management** over the demo API key for production.
- Publish downstream work (e.g. to **Service Bus**) instead of processing inline.
