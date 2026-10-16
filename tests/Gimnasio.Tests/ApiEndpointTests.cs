using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Gimnasio.Tests;

public class ApiEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ApiEndpointTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/personas")]
    [InlineData("/membresias")]
    [InlineData("/operaciones")]
    [InlineData("/studio")]
    public async Task GetPaginasHtml_DebeRetornarHttp200YContenidoHtml(string url)
    {
        var response = await _client.GetAsync(url);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("text/html");

        var html = await response.Content.ReadAsStringAsync();
        html.Should().Contain("<!DOCTYPE html>");
        html.Should().Contain("GimnasioDB");
    }

    [Fact]
    public async Task GetOpenApiDocument_DebeRetornarEsquemaValido()
    {
        var response = await _client.GetAsync("/openapi/v1.json");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("openapi");
    }

    [Fact]
    public async Task GetScalarDocumentation_DebeRetornarInterfazInteractiva()
    {
        var response = await _client.GetAsync("/scalar/v1");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.ToLowerInvariant().Should().Contain("scalar");
    }

    [Fact]
    public async Task PostPersona_ConDatosInvalidos_DebeRetornarBadRequestConDetalle()
    {
        var payloadInvalido = new
        {
            nombre = "",
            apellido = "",
            email = "no-es-un-email",
            fechaNacimiento = DateTime.Today.AddDays(10), // Fecha en el futuro
            esSocio = false,
            esEmpleado = true,
            idRol = (int?)null // Empleado sin rol
        };

        var response = await _client.PostAsJsonAsync("/api/personas", payloadInvalido);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("El nombre es obligatorio");
        body.Should().Contain("El formato del correo electrónico no es válido");
    }

    [Fact]
    public async Task PostClase_ConCupoInvalido_DebeRetornarBadRequest()
    {
        var payloadInvalido = new
        {
            nombre = "Spinning",
            horario = DateTime.Today.AddHours(20),
            cupoMaximo = 0, // Cupo inválido
            idProfesor = 1
        };

        var response = await _client.PostAsJsonAsync("/api/operaciones/clases", payloadInvalido);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("El cupo máximo de alumnos debe ser superior a cero.");
    }

    [Fact]
    public async Task GetPagosRecientes_DebeRetornarHttp200()
    {
        var response = await _client.GetAsync("/api/operaciones/pagos?limit=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task DeletePago_ConIdInexistente_DebeRetornarNotFound()
    {
        var response = await _client.DeleteAsync("/api/operaciones/pagos/9999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteContrato_ConIdInexistente_DebeRetornarNotFound()
    {
        var response = await _client.DeleteAsync("/api/membresias/contratos/9999999");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
