using Chain.Sample;

var pipeline = RequestContextHandlerPipeline.Instance;

Console.WriteLine("=== Authenticated request ===");
var authorized = new RequestContext("/api/orders", isAuthenticated: true);
await pipeline.InvokeAsync(authorized);
Console.WriteLine($"Response: {authorized.Response}");
Console.WriteLine();

Console.WriteLine("=== Unauthenticated request (short-circuit) ===");
var unauthorized = new RequestContext("/api/orders", isAuthenticated: false);
await pipeline.InvokeAsync(unauthorized);
Console.WriteLine($"Response: {unauthorized.Response}");
