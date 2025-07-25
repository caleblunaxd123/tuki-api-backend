﻿using WebApplication1.Data;
using WebApplication1.Controllers;
using static WebApplication1.Controllers.PagoController;

var builder = WebApplication.CreateBuilder(args);

// ================================
// CONFIGURACIÓN DE SERVICIOS
// ================================

// Servicios existentes
builder.Services.AddScoped<SqlConnectionFactory>();
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// SignalR para tiempo real
builder.Services.AddSignalR();

// CORS - DEBE IR ANTES DE Build()
builder.Services.AddCors(options =>
{
    options.AddPolicy("CorsPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Configuración para desarrollo
            policy
                .WithOrigins(
                    "http://localhost:3000",
                    "http://192.168.18.18:3000",
                    "http://10.0.2.2:3000",
                    "http://127.0.0.1:3000"
                )
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials()
                .SetIsOriginAllowed(origin => true);
        }
        else
        {
            // Configuración para producción
            policy
                .AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        }
    });
});

// Configuración adicional para producción
builder.Services.Configure<RouteOptions>(options =>
{
    options.LowercaseUrls = true;
});

var app = builder.Build();

// ================================
// CONFIGURACIÓN DE MIDDLEWARE (ORDEN IMPORTANTE)
// ================================

// CORS debe ir PRIMERO
app.UseCors("CorsPolicy");

// Swagger habilitado tanto en desarrollo como en producción (temporalmente)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Tuki API V1");
    c.RoutePrefix = "swagger";
});

// Middleware de desarrollo adicional
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Redirección HTTPS (comentar para desarrollo local)
// app.UseHttpsRedirection();

// Routing
app.UseRouting();

// Autorización (si la usas en el futuro)
app.UseAuthorization();

// Mapear controllers
app.MapControllers();

// ================================
// SIGNALR HUB - DEBE IR AL FINAL
// ================================
app.MapHub<WebApplication1.Controllers.PagoHub>("/pagohub");

// ================================
// ENDPOINTS ADICIONALES PARA TESTING
// ================================

// Endpoint de salud (disponible en todos los ambientes)
app.MapGet("/health", () => new {
    Status = "OK",
    Timestamp = DateTime.Now,
    Environment = app.Environment.EnvironmentName,
    Service = "Tuki API",
    Version = "1.0.0"
});

// Endpoint raíz
app.MapGet("/", () => new {
    Message = "Tuki API está funcionando correctamente",
    Status = "Online",
    Environment = app.Environment.EnvironmentName,
    Version = "1.0.0",
    Endpoints = new
    {
        Health = "/health",
        Swagger = "/swagger",
        SignalRHub = "/pagohub",
        ApiBase = "/api"
    }
});

// Endpoint para probar CORS (disponible en todos los ambientes)
app.MapGet("/test-cors", () => new {
    Message = "CORS funcionando correctamente",
    Environment = app.Environment.EnvironmentName,
    CorsPolicy = "Configurado para permitir cualquier origen en producción"
});

// Endpoint para probar SignalR
app.MapGet("/test-signalr", () => new {
    SignalRHub = "/pagohub",
    Message = "Hub disponible para conexiones",
    Environment = app.Environment.EnvironmentName
});

// Endpoints adicionales solo en desarrollo
if (app.Environment.IsDevelopment())
{
    app.MapGet("/dev-info", () => new {
        Environment = "Development",
        LocalUrls = new[] {
            "http://localhost:5220",
            "http://192.168.18.18:5220"
        },
        SwaggerUrl = "http://localhost:5220/swagger"
    });
}

// ================================
// INFORMACIÓN DE INICIO
// ================================
app.Lifetime.ApplicationStarted.Register(() =>
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("🚀 Tuki API iniciada correctamente");
    logger.LogInformation("🔗 SignalR Hub disponible en: /pagohub");
    logger.LogInformation("📱 CORS configurado para React Native");
    logger.LogInformation("🌐 Environment: {Environment}", app.Environment.EnvironmentName);

    if (app.Environment.IsDevelopment())
    {
        logger.LogInformation("📊 Swagger UI: http://localhost:5220/swagger");
        logger.LogInformation("🩺 Health Check: http://localhost:5220/health");
    }
    else
    {
        logger.LogInformation("📊 Swagger UI disponible en: /swagger");
        logger.LogInformation("🩺 Health Check disponible en: /health");
        logger.LogInformation("🔧 Endpoints de prueba disponibles en: /test-cors, /test-signalr");
    }
});

app.Run();