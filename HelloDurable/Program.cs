using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

// Minimal startup. The Durable extension wires itself up automatically.
var builder = FunctionsApplication.CreateBuilder(args);
builder.ConfigureFunctionsWebApplication();
builder.Build().Run();
