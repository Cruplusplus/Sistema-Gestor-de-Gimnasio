using FluentValidation;
using Gimnasio.Web.Data;
using Gimnasio.Web.Endpoints;
using Gimnasio.Web.Infrastructure;
using Gimnasio.Web.Validators;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Inyección de dependencias moderna para acceso a datos eficiente
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IGimnasioRepository, GimnasioRepository>();

// Soporte de OpenAPI y documentación interactiva Scalar en .NET 10
builder.Services.AddOpenApi();

// Manejo centralizado de excepciones (RFC 7807 ProblemDetails)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Validadores automáticos con FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<CrearPersonaRequestValidator>();

// Soporte de CORS para desarrollo flexible
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseCors("AllowAll");

// Documentación de endpoints en OpenAPI / Scalar
app.MapOpenApi();
app.MapScalarApiReference(options =>
{
    options.WithTitle("GimnasioDB API • Suite Comercial")
           .WithTheme(ScalarTheme.Moon);
});

// Habilitar servidor de archivos estáticos para la interfaz SPA
app.UseDefaultFiles();
app.UseStaticFiles();

// Mapeo modular de endpoints de la API
app.MapGimnasioEndpoints();

// Rutas amigables para navegación limpia Multi-Page (URLs limpias sin .html)
app.MapGet("/personas", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.WebRootPath, "personas.html"), "text/html"));
app.MapGet("/membresias", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.WebRootPath, "membresias.html"), "text/html"));
app.MapGet("/operaciones", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.WebRootPath, "operaciones.html"), "text/html"));
app.MapGet("/studio", (IWebHostEnvironment env) => 
    Results.File(Path.Combine(env.WebRootPath, "studio.html"), "text/html"));

app.Run();

// Permitir que WebApplicationFactory en tests de integración use Program
public partial class Program { }
