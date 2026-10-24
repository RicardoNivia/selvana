using Microsoft.AspNetCore.Http;
using Selvana.SharedKernel.Results;

namespace Selvana.Web.Common;

/// <summary>Traduce Result a respuestas HTTP con ProblemDetails (ver docs/arquitectura/backend.md, sección API).</summary>
public static class ResultHttpExtensions
{
    public static IResult ToHttp(this Result result) =>
        result.IsSuccess ? Results.NoContent() : Problem(result.Error);

    public static IResult ToHttp<TValue>(this Result<TValue> result) =>
        result.IsSuccess ? Results.Ok(result.Value) : Problem(result.Error);

    public static IResult ToCreated<TValue>(this Result<TValue> result, Func<TValue, string> location) =>
        result.IsSuccess ? Results.Created(location(result.Value), result.Value) : Problem(result.Error);

    private static IResult Problem(Error error)
    {
        if (error is ValidationError validation)
        {
            return Results.ValidationProblem(
                validation.Errors
                    .GroupBy(e => e.Code)
                    .ToDictionary(g => g.Key, g => g.Select(e => e.Description).ToArray()),
                title: validation.Description);
        }

        var status = error.Type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError,
        };

        return Results.Problem(title: error.Code, detail: error.Description, statusCode: status);
    }
}
