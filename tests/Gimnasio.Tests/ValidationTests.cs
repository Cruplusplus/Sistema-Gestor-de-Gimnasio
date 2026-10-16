using FluentAssertions;
using Gimnasio.Web.Models;
using Gimnasio.Web.Validators;
using Xunit;

namespace Gimnasio.Tests;

public class ValidationTests
{
    private readonly CrearPersonaRequestValidator _personaValidator = new();
    private readonly RegistrarSocioMembresiaRequestValidator _membresiaValidator = new();
    private readonly CrearClaseRequestValidator _claseValidator = new();
    private readonly CrearPlanMembresiaRequestValidator _planValidator = new();

    [Fact]
    public void PersonaValidator_ConDatosValidos_DebePasar()
    {
        var request = new CrearPersonaRequest(
            Nombre: "Carlos",
            Apellido: "Tevez",
            Email: "carlos.tevez@club.com",
            Telefono: "1122334455",
            FechaNacimiento: new DateTime(1984, 2, 5),
            EsSocio: true,
            FechaRegistro: DateTime.Today,
            EsEmpleado: false,
            IDRol: null,
            FechaContratacion: null
        );

        var result = _personaValidator.Validate(request);

        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("", "Tevez", "correo@valido.com", "El nombre es obligatorio.")]
    [InlineData("Carlos", "", "correo@valido.com", "El apellido es obligatorio.")]
    [InlineData("Carlos", "Tevez", "formato-invalido", "El formato del correo electrónico no es válido.")]
    public void PersonaValidator_ConCamposInvalidos_DebeFallar(string nombre, string apellido, string email, string errorEsperado)
    {
        var request = new CrearPersonaRequest(
            Nombre: nombre,
            Apellido: apellido,
            Email: email,
            Telefono: null,
            FechaNacimiento: new DateTime(1990, 1, 1),
            EsSocio: true,
            FechaRegistro: DateTime.Today,
            EsEmpleado: false,
            IDRol: null,
            FechaContratacion: null
        );

        var result = _personaValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == errorEsperado);
    }

    [Fact]
    public void PersonaValidator_EmpleadoSinRol_DebeFallar()
    {
        var request = new CrearPersonaRequest(
            Nombre: "Lionel",
            Apellido: "Scaloni",
            Email: "scaloni@afa.com",
            Telefono: null,
            FechaNacimiento: new DateTime(1978, 5, 16),
            EsSocio: false,
            FechaRegistro: null,
            EsEmpleado: true,
            IDRol: null,
            FechaContratacion: DateTime.Today
        );

        var result = _personaValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "IDRol");
    }

    [Fact]
    public void MembresiaValidator_FechaFinAnteriorAFechaInicio_DebeFallar()
    {
        var request = new RegistrarSocioMembresiaRequest(
            IDSocio: 1,
            IDMembresia: 1,
            FechaInicio: new DateTime(2026, 10, 15),
            FechaFin: new DateTime(2026, 10, 10),
            Activa: true,
            RegistrarPagoInicial: false,
            MontoPagoInicial: null,
            MetodoPagoInicial: null
        );

        var result = _membresiaValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "FechaFin");
    }

    [Fact]
    public void MembresiaValidator_PagoInicialConMontoCero_DebeFallar()
    {
        var request = new RegistrarSocioMembresiaRequest(
            IDSocio: 1,
            IDMembresia: 1,
            FechaInicio: new DateTime(2026, 10, 15),
            FechaFin: new DateTime(2026, 11, 15),
            Activa: true,
            RegistrarPagoInicial: true,
            MontoPagoInicial: 0,
            MetodoPagoInicial: "Efectivo"
        );

        var result = _membresiaValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "MontoPagoInicial");
    }

    [Theory]
    [InlineData(0, "El cupo máximo de alumnos debe ser superior a cero.")]
    [InlineData(150, "El cupo máximo no puede superar 100 alumnos por sala.")]
    public void ClaseValidator_CuposInvalidos_DebeFallar(int cupoMaximo, string mensajeEsperado)
    {
        var request = new CrearClaseRequest(
            Nombre: "CrossFit Intensivo",
            Horario: DateTime.Today.AddHours(18),
            CupoMaximo: cupoMaximo,
            IDProfesor: 1
        );

        var result = _claseValidator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == mensajeEsperado);
    }

    [Fact]
    public void PlanMembresiaValidator_ConDatosCorrectos_DebePasar()
    {
        var request = new CrearPlanMembresiaRequest(
            Nombre: "Pase Semestral Premium",
            Precio: 150000m,
            DuracionDias: 180
        );

        var result = _planValidator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void PagoValidator_ConDatosValidos_DebePasar()
    {
        var validator = new RegistrarPagoRequestValidator();
        var request = new RegistrarPagoRequest(
            IDSocioMembresia: 1,
            Monto: 35000m,
            FechaPago: DateTime.Now
        );

        var result = validator.Validate(request);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(0, "El monto a pagar debe ser mayor a cero.")]
    [InlineData(-100, "El monto a pagar debe ser mayor a cero.")]
    public void PagoValidator_ConMontoInvalido_DebeFallar(decimal montoInvalido, string errorEsperado)
    {
        var validator = new RegistrarPagoRequestValidator();
        var request = new RegistrarPagoRequest(
            IDSocioMembresia: 1,
            Monto: montoInvalido,
            FechaPago: DateTime.Now
        );

        var result = validator.Validate(request);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == errorEsperado);
    }
}
