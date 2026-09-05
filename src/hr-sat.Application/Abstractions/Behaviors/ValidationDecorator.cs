using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;

namespace hr_sat.Application.Abstractions.Behaviors;

internal sealed class ValidationDecorator<TCommand>(
    IEnumerable<IValidator<TCommand>> validators,
    ICommandHandler<TCommand> handler)
    : ICommandHandler<TCommand>
    where TCommand : ICommand
{
    public async Task<Result> Handle(
        TCommand command,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidationDecorator.ValidateAsync(
            validators,
            command,
            cancellationToken);
        return validationError is null
            ? await handler.Handle(command, cancellationToken)
            : Result.Failure(validationError);
    }
}

internal sealed class ValidationDecorator<TCommand, TResponse>(
    IEnumerable<IValidator<TCommand>> validators,
    ICommandHandler<TCommand, TResponse> handler)
    : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async Task<Result<TResponse>> Handle(
        TCommand command,
        CancellationToken cancellationToken)
    {
        var validationError = await ValidationDecorator.ValidateAsync(
            validators,
            command,
            cancellationToken);
        return validationError is null
            ? await handler.Handle(command, cancellationToken)
            : Result<TResponse>.Failure(validationError);
    }
}

internal static class ValidationDecorator
{
    public static async Task<ValidationError?> ValidateAsync<TCommand>(
        IEnumerable<IValidator<TCommand>> validators,
        TCommand command,
        CancellationToken cancellationToken)
    {
        var failures = new List<ValidationFailure>();
        foreach (var validator in validators)
        {
            var result = await validator.ValidateAsync(command, cancellationToken);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            return null;
        }

        var errors = failures
            .GroupBy(
                failure => ToApiPropertyName(failure.PropertyName),
                StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray(),
                StringComparer.Ordinal);
        return new ValidationError(
            "Validation.Invalid",
            "One or more validation errors occurred.",
            errors);
    }

    private static string ToApiPropertyName(string propertyName)
    {
        var collectionIndex = propertyName.IndexOf('[', StringComparison.Ordinal);
        return JsonNamingPolicy.CamelCase.ConvertName(
            collectionIndex < 0 ? propertyName : propertyName[..collectionIndex]);
    }
}