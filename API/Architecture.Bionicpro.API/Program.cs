using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var frontendCorsPolicyName = "frontendPolicy";
var protheticUserOnlyPolicyName = "ProtheticUserOnly";
var protheticUserRoleName = "prothetic_user";
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: frontendCorsPolicyName,
        policy =>
        {
            policy.WithOrigins("http://localhost:3000")
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.Authority = "http://keycloak:8080/realms/reports-realm";
        options.Audience = "reports-frontend";
        options.RequireHttpsMetadata = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidIssuer = "http://localhost:8080/realms/reports-realm",
            RoleClaimType = "roles",
            NameClaimType = "preferred_username"
        };
        // options.Events = new JwtBearerEvents
        // {
        //     OnAuthenticationFailed = context =>
        //     {
        //         Console.WriteLine("Authentication failed: " + context.Exception.Message);
        //         return Task.CompletedTask;
        //     },
        //     OnTokenValidated = context =>
        //     {
        //         Console.WriteLine("Token is valid");
        //         return Task.CompletedTask;
        //     },
        //     OnMessageReceived = context =>
        //     {
        //         Console.WriteLine("Token received: " + context.Token);
        //         return Task.CompletedTask;
        //     }
        // };
    });

// Не получилось передать плоский список ролей из Keycloak
// Стандартные механизмы dotnet не понимают вложенный объект типа realm_access: {"roles":["prothetic_user"]}
// Поэтому впилен этот костыль для проверки роли
builder.Services.AddAuthorization(options => options.AddPolicy(protheticUserOnlyPolicyName, policy =>
    policy.RequireAssertion(c =>
        JsonSerializer.Deserialize<Dictionary<string, string[]>>(c.User.FindFirst(claim => claim.Type == "realm_access")?.Value ?? "{}")!
            .FirstOrDefault().Value?.Any(v => v == protheticUserRoleName) ?? false)));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors(frontendCorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

var summaries = new[]
{
    "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

app.MapGet("/reports", (HttpContext context, ILogger<Program> logger) =>
    {
        var forecast = Enumerable.Range(1, 5).Select(index =>
                new WeatherForecast
                (
                    DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                    Random.Shared.Next(-20, 55),
                    summaries[Random.Shared.Next(summaries.Length)]
                ))
            .ToArray();
        return forecast;
    })
    .RequireAuthorization(protheticUserOnlyPolicyName)
    .WithName("GetReport")
    .WithOpenApi();

app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}