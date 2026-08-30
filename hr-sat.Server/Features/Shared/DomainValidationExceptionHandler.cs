using hr_sat.Server.Domain;
using Microsoft.AspNetCore.Diagnostics;

namespace hr_sat.Server.Features.Shared;

internal sealed class DomainValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not DomainValidationException validationException)
        {
            return false;
        }

        await TypedResults.ValidationProblem(validationException.Errors)
            .ExecuteAsync(httpContext);
        return true;
    }
}