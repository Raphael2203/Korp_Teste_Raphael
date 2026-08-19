using InventoryService.Ai;
using InventoryService.Data;
using InventoryService.Exceptions;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Registra os Controllers
builder.Services.AddControllers();

// Registra o DbContext e configura o PostgreSQL
builder.Services.AddDbContext<InventoryDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("InventoryDatabase")
    )
);

// Assistente de IA para sugestão de descrição de produto (opcional).
builder.Services.AddSingleton<ProductDescriptionAssistant>();

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
            "Inventory Service API"
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
        .GetRequiredService<InventoryDbContext>();

    await dbContext.Database.MigrateAsync();
}

app.Run();
