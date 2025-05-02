using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/weatherforecast", async () =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();

        await Task.Delay(200);
        return forecast;
    })
    .WithName("GetWeatherForecast");

app.MapGet("/hello", async () =>
{
    await Task.Delay(100);
    return "Hello World!";
});

app.MapPost("/auth", () => new AccessTokenResponse(Guid.NewGuid().ToString().ToLowerInvariant(), "Bearer", 3600));

app.MapGet("/random-error", () =>
{
    // Return 500 for 30% of requests
    if (Random.Shared.NextDouble() < 0.3)
    {
        return Results.StatusCode(500);
    }

    return Results.Ok("Success!");
});

app.Run();

internal record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}

internal record AccessTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn);