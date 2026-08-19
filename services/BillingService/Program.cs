using BillingService.Clients;
using BillingService.Data;
using BillingService.Exceptions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registra os Controllers
builder.Services.AddControllers();

// Registra o DbContext e configura o PostgreSQL
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("BillingDatabase")
    )
);

// Cliente tipado para o microsserviço de estoque, com política de resiliência
// (retry, timeout e circuit breaker) do Microsoft.Extensions.Http.Resilience.
var inventoryBaseUrl = builder.Configuration["Services:InventoryUrl"]
    ?? "http://localhost:5159";

builder.Services
    .AddHttpClient<InventoryClient>(client =>
    {
        client.BaseAddress = new Uri(inventoryBaseUrl.TrimEnd('/') + "/");
    })
    .AddStandardResilienceHandler(options =>
    {
        // Timeouts curtos: o usuário precisa de feedback rápido quando o
        // estoque está fora do ar.
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(5);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(20);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(15);
        options.Retry.MaxRetryAttempts = 2;
    });

// Tratamento global de exceções com respostas padronizadas (ProblemDetails).
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHealthChecks();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy =>
        {
            policy
                .WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

app.UseCors("AllowAngular");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint(
            "/openapi/v1.json",
            "Billing Service API"
        );
    });
}

app.MapHealthChecks("/health");

// Mapeia os Controllers
app.MapControllers();

// Aplica as migrations pendentes na subida (usado pelo ambiente Docker).
if (app.Configuration.GetValue("Database:AutoMigrate", false))
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<BillingDbContext>();

    await dbContext.Database.MigrateAsync();
}

app.Run();
