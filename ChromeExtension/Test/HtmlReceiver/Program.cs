var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors("AllowAll");

app.MapPost("/receive-html", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine("Received HTML:");
    Console.WriteLine(body[..Math.Min(500, body.Length)]); // print first 500 chars
    return Results.Ok(new { status = "received" });
});

app.Run();
