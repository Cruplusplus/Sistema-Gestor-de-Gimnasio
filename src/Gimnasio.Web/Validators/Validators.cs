using FluentValidation;
using Gimnasio.Web.Models;

namespace Gimnasio.Web.Validators;

public sealed class CrearPersonaRequestValidator : AbstractValidator<CrearPersonaRequest>
{
    public CrearPersonaRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre no puede superar los 50 caracteres.");

        RuleFor(x => x.Apellido)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(50).WithMessage("El apellido no puede superar los 50 caracteres.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo electrónico es obligatorio.")
            .EmailAddress().WithMessage("El formato del correo electrónico no es válido.")
            .MaximumLength(100).WithMessage("El correo electrónico no puede superar los 100 caracteres.");

        RuleFor(x => x.FechaNacimiento)
            .NotEmpty().WithMessage("La fecha de nacimiento es obligatoria.")
            .LessThan(DateTime.Today).WithMessage("La fecha de nacimiento debe ser anterior al día de hoy.");

        When(x => x.EsEmpleado, () =>
        {
            RuleFor(x => x.IDRol)
                .NotNull().WithMessage("Debe seleccionar un rol para el empleado.")
                .GreaterThan(0).WithMessage("El rol seleccionado no es válido.");

            RuleFor(x => x.FechaContratacion)
                .NotNull().WithMessage("Debe indicar la fecha de contratación del personal.");
        });
    }
}

public sealed class RegistrarSocioMembresiaRequestValidator : AbstractValidator<RegistrarSocioMembresiaRequest>
{
    public RegistrarSocioMembresiaRequestValidator()
    {
        RuleFor(x => x.IDSocio)
            .GreaterThan(0).WithMessage("Debe seleccionar un socio válido.");

        RuleFor(x => x.IDMembresia)
            .GreaterThan(0).WithMessage("Debe seleccionar un plan de membresía válido.");

        RuleFor(x => x.FechaInicio)
            .NotEmpty().WithMessage("La fecha de inicio es obligatoria.");

        When(x => x.FechaFin.HasValue, () =>
        {
            RuleFor(x => x.FechaFin)
                .GreaterThanOrEqualTo(x => x.FechaInicio)
                .WithMessage("La fecha de finalización no puede ser anterior a la fecha de inicio.");
        });

        When(x => x.RegistrarPagoInicial == true, () =>
        {
            RuleFor(x => x.MontoPagoInicial)
                .NotNull().WithMessage("Debe indicar el monto del cobro inicial.")
                .GreaterThan(0).WithMessage("El monto inicial debe ser superior a cero.");
        });
    }
}

public sealed class RegistrarPagoRequestValidator : AbstractValidator<RegistrarPagoRequest>
{
    public RegistrarPagoRequestValidator()
    {
        RuleFor(x => x.IDSocioMembresia)
            .GreaterThan(0).WithMessage("Debe especificar un contrato de membresía válido.");

        RuleFor(x => x.Monto)
            .GreaterThan(0).WithMessage("El monto a pagar debe ser mayor a cero.");

        RuleFor(x => x.FechaPago)
            .NotEmpty().WithMessage("La fecha de pago es obligatoria.");
    }
}

public sealed class InscribirSocioRequestValidator : AbstractValidator<InscribirSocioRequest>
{
    public InscribirSocioRequestValidator()
    {
        RuleFor(x => x.IDSocio)
            .GreaterThan(0).WithMessage("Debe seleccionar un socio válido.");

        RuleFor(x => x.IDClase)
            .GreaterThan(0).WithMessage("Debe seleccionar una clase válida.");
    }
}

public sealed class CrearClaseRequestValidator : AbstractValidator<CrearClaseRequest>
{
    public CrearClaseRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre de la actividad o clase es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre de la clase no puede exceder 50 caracteres.");

        RuleFor(x => x.Horario)
            .NotEmpty().WithMessage("Debe definir un horario para la clase.");

        RuleFor(x => x.CupoMaximo)
            .GreaterThan(0).WithMessage("El cupo máximo de alumnos debe ser superior a cero.")
            .LessThanOrEqualTo(100).WithMessage("El cupo máximo no puede superar 100 alumnos por sala.");

        RuleFor(x => x.IDProfesor)
            .GreaterThan(0).WithMessage("Debe asignar un profesor responsable para la clase.");
    }
}

public sealed class CrearPlanMembresiaRequestValidator : AbstractValidator<CrearPlanMembresiaRequest>
{
    public CrearPlanMembresiaRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del plan de membresía es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre del plan no puede exceder 100 caracteres.");

        RuleFor(x => x.Precio)
            .GreaterThan(0).WithMessage("El precio del plan debe ser superior a cero.");

        RuleFor(x => x.DuracionDias)
            .GreaterThan(0).WithMessage("La duración nominal en días debe ser mayor a cero.");
    }
}

public sealed class ActualizarPrecioPlanRequestValidator : AbstractValidator<ActualizarPrecioPlanRequest>
{
    public ActualizarPrecioPlanRequestValidator()
    {
        RuleFor(x => x.NuevoPrecio)
            .GreaterThan(0).WithMessage("El nuevo arancel debe ser mayor a cero.");
    }
}
