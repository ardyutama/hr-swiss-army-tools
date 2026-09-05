using hr_sat.Domain;

namespace hr_sat.Web.Api;

public static class CustomResults
{
    public static IResult Problem(Error error) =>
        error is ValidationError validationError
            ? TypedResults.ValidationProblem(validationError.Errors)
            : TypedResults.Problem(
                statusCode: GetStatusCode(error.Type),
                title: error.Code,
                detail: error.Message);

    private static int GetStatusCode(ErrorType errorType) => errorType switch
    {
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        ErrorType.Failure => StatusCodes.Status500InternalServerError,
        _ => StatusCodes.Status400BadRequest
    };
}