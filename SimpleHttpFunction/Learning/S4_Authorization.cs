using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace SimpleHttpFunction.Learning;

// =============================================================================
// STEP 4 — AUTHORIZATION of stateless functions.  (Read the big comment!)
// =============================================================================
//
// Every [HttpTrigger] takes an AuthorizationLevel. This is the BUILT-IN,
// key-based gate that the Functions host enforces BEFORE your code runs.
//
//   AuthorizationLevel.Anonymous
//       No key required. Anyone who can reach the URL can call it.
//       Use for truly public endpoints, or when a real auth layer (JWT, APIM,
//       API Management, a middleware — see Track B) sits in front.
//
//   AuthorizationLevel.Function
//       Caller must present a valid FUNCTION KEY or HOST KEY.
//       This is the most common "protect it a bit" level.
//
//   AuthorizationLevel.Admin
//       Caller must present the MASTER (host) key. Very powerful — avoid
//       exposing Admin endpoints publicly.
//
//   (There is also User/System, rarely used directly.)
//
// HOW DO YOU PASS A KEY?  Two equivalent ways:
//     Header (preferred):   x-functions-key: <the-key>
//     Query string:         ?code=<the-key>
//
// WHERE DO KEYS LIVE?
//     - In Azure: auto-generated per function ("function keys") and per app
//       ("host keys" / "master key"), manageable in the portal or via API.
//     - LOCALLY: the local host does NOT enforce keys by default, so Function/
//       Admin endpoints are callable without a key on your machine. That's a
//       convenience for development — do not mistake it for "it's open in prod".
//
// !!! IMPORTANT SECURITY REALITY — WHAT FUNCTION KEYS ARE *NOT* !!!
//     Function keys are a SHARED SECRET, like a password on the URL. They tell
//     you the caller HAS a key — they do NOT tell you WHO the caller is, they
//     have no expiry, no scopes, no per-user revocation, and they leak easily
//     (logs, browser history, referrer headers).
//
//     For real enterprise auth you want IDENTITY, not just a key:
//       - Microsoft Entra ID (Azure AD) / OAuth2 JWT bearer tokens  <-- best
//       - Azure API Management or App Gateway in front (rate limit, WAF, keys)
//       - A custom middleware that validates an API key per client (Track B
//         shows this pattern, which is auditable and revocable per client).
//
//     Rule: keys are fine for service-to-service or "keep randoms out". Use
//     token-based identity when you need to know WHO is calling and enforce
//     what THEY specifically may do.
//
public class S4_Authorization
{
    // PUBLIC endpoint — no protection. Callable by anyone.
    [Function("S4_Public")]
    public IActionResult Public(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "secure/public")] HttpRequest req)
        => new OkObjectResult(new { access = "public", note = "No key needed." });

    // PROTECTED-BY-KEY endpoint. In Azure the caller must send a valid function
    // or host key. Locally you can still call it without one (see note above).
    [Function("S4_KeyProtected")]
    public IActionResult KeyProtected(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "secure/key")] HttpRequest req)
        => new OkObjectResult(new
        {
            access = "function-key",
            note = "In Azure: send  x-functions-key: <key>  or  ?code=<key>."
        });

    // ADMIN endpoint — requires the master key. Keep these rare and internal.
    [Function("S4_AdminOnly")]
    public IActionResult AdminOnly(
        [HttpTrigger(AuthorizationLevel.Admin, "get", Route = "secure/admin")] HttpRequest req)
        => new OkObjectResult(new { access = "admin", note = "Requires the master/host key." });
}
