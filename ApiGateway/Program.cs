using Microsoft.OpenApi.Models;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("http://0.0.0.0:80");

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

builder.Services.AddHttpClient();

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseCors("AllowAll");

// Эндпоинт для агрегированной OpenAPI спецификации
app.MapGet("/swagger/v1/swagger.json", async (IHttpClientFactory httpClientFactory) =>
{
    var httpClient = httpClientFactory.CreateClient();
    var aggregatedSpec = new
    {
        openapi = "3.0.1",
        info = new
        {
            title = "API Gateway - Aggregated APIs",
            version = "v1",
            description = "Unified API documentation for Orders and Payments microservices"
        },
        servers = new[]
        {
            new { url = "/", description = "API Gateway" }
        },
        paths = new Dictionary<string, object>(),
        components = new
        {
            schemas = new Dictionary<string, object>()
        }
    };

    var pathsDict = (Dictionary<string, object>)aggregatedSpec.paths;
    var schemasDict = (Dictionary<string, object>)aggregatedSpec.components.schemas;

    try
    {
        // Получаем спецификацию Orders Service
        var ordersSpec = await httpClient.GetStringAsync("http://orders-service:80/swagger/v1/swagger.json");
        var ordersJson = JsonDocument.Parse(ordersSpec);

        if (ordersJson.RootElement.TryGetProperty("paths", out var ordersPaths))
        {
            foreach (var path in ordersPaths.EnumerateObject())
            {
                var newPath = $"/api/orders{path.Name.Replace("/api", "")}";
                var pathValue = JsonSerializer.Deserialize<Dictionary<string, object>>(path.Value.GetRawText());
                AddTagsToOperations(pathValue, "Orders");
                pathsDict[newPath] = pathValue;
            }
        }

        if (ordersJson.RootElement.TryGetProperty("components", out var ordersComponents) &&
            ordersComponents.TryGetProperty("schemas", out var ordersSchemas))
        {
            foreach (var schema in ordersSchemas.EnumerateObject())
            {
                schemasDict[schema.Name] = JsonSerializer.Deserialize<object>(schema.Value.GetRawText());
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to fetch Orders Service spec: {ex.Message}");
    }

    try
    {
        // Получаем спецификацию Payments Service
        var paymentsSpec = await httpClient.GetStringAsync("http://payments-service:80/swagger/v1/swagger.json");
        var paymentsJson = JsonDocument.Parse(paymentsSpec);

        if (paymentsJson.RootElement.TryGetProperty("paths", out var paymentsPaths))
        {
            foreach (var path in paymentsPaths.EnumerateObject())
            {
                var newPath = $"/api/payments{path.Name.Replace("/api", "")}";
                var pathValue = JsonSerializer.Deserialize<Dictionary<string, object>>(path.Value.GetRawText());
                AddTagsToOperations(pathValue, "Payments");
                pathsDict[newPath] = pathValue;
            }
        }

        if (paymentsJson.RootElement.TryGetProperty("components", out var paymentsComponents) &&
            paymentsComponents.TryGetProperty("schemas", out var paymentsSchemas))
        {
            foreach (var schema in paymentsSchemas.EnumerateObject())
            {
                schemasDict[schema.Name] = JsonSerializer.Deserialize<object>(schema.Value.GetRawText());
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Failed to fetch Payments Service spec: {ex.Message}");
    }

    return Results.Content(JsonSerializer.Serialize(aggregatedSpec, new JsonSerializerOptions
    {
        WriteIndented = true
    }), "application/json");
}).ExcludeFromDescription();

app.MapGet("/swagger", async context =>
{
    var html = GenerateSwaggerUI();
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(html);
});

app.MapGet("/swagger/index.html", async context =>
{
    var html = GenerateSwaggerUI();
    context.Response.ContentType = "text/html";
    await context.Response.WriteAsync(html);
});

app.Use(async (context, next) =>
{
    Console.WriteLine($"[DEBUG] Request: {context.Request.Method} {context.Request.Path}");
    await next();
});

app.MapHealthChecks("/health");

app.MapReverseProxy();

app.Run();

static void AddTagsToOperations(Dictionary<string, object> pathValue, string serviceName)
{
    foreach (var method in pathValue.ToList())
    {
        if (method.Value is JsonElement methodElement && methodElement.ValueKind == JsonValueKind.Object)
        {
            var methodDict = JsonSerializer.Deserialize<Dictionary<string, object>>(methodElement.GetRawText());
            if (methodDict != null)
            {
                methodDict["tags"] = new[] { serviceName };
                pathValue[method.Key] = methodDict;
            }
        }
    }
}

static string GenerateSwaggerUI()
{
    return @"
<!DOCTYPE html>
<html>
<head>
    <title>API Gateway - Swagger UI</title>
    <link rel=""stylesheet"" type=""text/css"" href=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui.css"" />
    <style>
        html { box-sizing: border-box; overflow: -moz-scrollbars-vertical; overflow-y: scroll; }
        *, *:before, *:after { box-sizing: inherit; }
        body { margin:0; background: #fafafa; }
    </style>
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-bundle.js""></script>
    <script src=""https://unpkg.com/swagger-ui-dist@5.9.0/swagger-ui-standalone-preset.js""></script>
    <script>
        window.onload = function() {
            const ui = SwaggerUIBundle({
                url: '/swagger/v1/swagger.json',
                dom_id: '#swagger-ui',
                deepLinking: true,
                presets: [
                    SwaggerUIBundle.presets.apis,
                    SwaggerUIStandalonePreset
                ],
                plugins: [
                    SwaggerUIBundle.plugins.DownloadUrl
                ],
                layout: ""StandaloneLayout""
            });
        };
    </script>
</body>
</html>";
}