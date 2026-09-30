using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;

namespace SimpleHttpFunction.Learning;

// =============================================================================
// STEP 3 — Accepting data in the request BODY (POST) and returning proper
//          HTTP status codes.
// =============================================================================
//
// GET requests carry data in the URL. POST/PUT requests carry it in the body,
// usually as JSON. This is how you accept structured input (a form, a record).
//
// Q (participant): "Why bother with status codes? Can't I just return text?"
// A: HTTP status codes are the CONTRACT with the caller. 200 = success,
//    400 = "you sent bad data", 404 = "not found", 500 = "we broke". Clients,
//    load balancers, and monitoring all rely on them. Returning 200 with an
//    error message inside is a classic API smell.
//
public class S3_RequestBody
{
    // A DTO (Data Transfer Object): the shape we expect the caller to send.
    // Keeping this separate from any internal model is good hygiene (see Track B).
    public record SumRequest(int[]? Numbers);

    // Reuse one options object; web defaults make JSON matching case-insensitive
    // (so "numbers" and "Numbers" both bind).
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    [Function("S3_Sum")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "sum")] HttpRequest req)
    {
        SumRequest? body;
        try
        {
            // Deserialize the JSON body into our DTO. This can throw if the caller
            // sends malformed JSON — so we guard it and translate failure into 400.
            body = await JsonSerializer.DeserializeAsync<SumRequest>(req.Body, JsonOpts);
        }
        catch (JsonException)
        {
            // 400 Bad Request: the problem is on the CALLER's side.
            return new BadRequestObjectResult(new { error = "Body is not valid JSON." });
        }

        // Validate. Never trust the body just because it parsed.
        if (body?.Numbers is null || body.Numbers.Length == 0)
        {
            return new BadRequestObjectResult(new { error = "Provide a non-empty 'numbers' array." });
        }

        var total = body.Numbers.Sum();

        // 200 OK with the result.
        return new OkObjectResult(new { count = body.Numbers.Length, total });
    }
}
