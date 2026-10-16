using Gimnasio.Web.Data;
using Gimnasio.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Inyección de dependencias moderna para acceso a datos eficiente
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IGimnasioRepository, GimnasioRepository>();

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

app.UseCors("AllowAll");

// Habilitar servidor de archivos estáticos para la interfaz SPA
app.UseDefaultFiles();
app.UseStaticFiles();

// Mapeo modular de endpoints de la API
app.MapGimnasioEndpoints();

app.Run();
