using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace SimpleHttpFunction.Learning;

// =============================================================================
// STEP 1 — The absolute minimum HTTP-triggered Azure Function.
// =============================================================================
//
// WHAT IS A "STATELESS" FUNCTION?
//   An Azure Function is just a method that Azure runs *for you* when something
//   happens (a "trigger"). It is "stateless" because:
//     - It keeps NO memory between calls. Every request starts from scratch.
//     - Azure can run 1 copy or 1,000 copies of it at the same time (scale-out),
//       and it can throw those copies away when traffic drops (even to zero).
//     - Because any copy can serve any request, you must NEVER store per-user
//       data in a static/instance field and expect it to still be there next
//       time. State lives OUTSIDE the function (a DB, a queue, a cache).
//
//   Contrast: a long-running Web API server holds sessions in memory. A Function
//   does not — which is exactly what makes it cheap and infinitely scalable.
//
// Q (participant): "Is this the same as AWS Lambda?"
// A: Conceptually yes — event-driven, pay-per-execution, auto-scaling functions.
//    Azure Functions is Microsoft's equivalent.
//
public class S1_HelloWorld
{
    // [Function] REGISTERS this method with the Functions runtime and gives it a
    // unique name. This name is what shows up in the portal and in logs. It has
    // nothing to do with the URL by itself.
    [Function("S1_HelloWorld")]
    public IActionResult Run(
        // [HttpTrigger] says: "run this method when an HTTP request arrives."
        //   - AuthorizationLevel.Anonymous  -> no key/token needed (see S4).
        //   - "get"                         -> only GET is allowed here.
        // By default the URL is:  http://localhost:7218/api/S1_HelloWorld
        // (the runtime uses the [Function] name as the route unless you override it).
        [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequest req)
    {
        // We simply return a value. The runtime turns it into an HTTP response.
        // IActionResult lets us pick the shape/status; here 200 OK + plain text.
        return new OkObjectResult("Hello from your first stateless Azure Function!");
    }
}
