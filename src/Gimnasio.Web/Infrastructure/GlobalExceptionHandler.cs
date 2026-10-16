using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace Gimnasio.Web.Infrastructure;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Error no controlado capturado por GlobalExceptionHandler: {Message}", exception.Message);

        int statusCode;
        string title;
        string detail = exception.Message;

        switch (exception)
        {
            case ArgumentException argEx:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Solicitud Inválida";
                detail = argEx.Message;
                break;

            case KeyNotFoundException knfEx:
                statusCode = StatusCodes.Status404NotFound;
                title = "Recurso No Encontrado";
                detail = knfEx.Message;
                break;

            case InvalidOperationException invEx:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Operación No Permitida";
                detail = invEx.Message;
                break;

            case SqlException sqlEx:
                statusCode = StatusCodes.Status400BadRequest;
                title = "Error de Base de Datos";
                // Identificar violaciones de restricción UNIQUE o errores de negocio emitidos por triggers/SPs
                if (sqlEx.Number is 2627 or 2601)
                {
                    statusCode = StatusCodes.Status409Conflict;
                    title = "Conflicto de Integridad Única";
                    detail = "El registro ya existe o viola una restricción única en la base de datos.";
                }
                else if (sqlEx.Number == 50000) // RAISERROR personalizado de triggers o SPs
                {
                    detail = sqlEx.Message;
                }
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "Error Interno del Servidor";
                detail = "Ocurrió un error inesperado al procesar la solicitud.";
                break;
        }

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path
        };
        problemDetails.Extensions["message"] = detail;

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);

        return true;
    }
}
