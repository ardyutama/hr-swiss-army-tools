using hr_sat.Application.Abstractions.Data;
using hr_sat.Application.Abstractions.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using hr_sat.Application.Abstractions.Behaviors;

namespace hr_sat.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(IApplicationDbContext).Assembly;

        services.AddValidatorsFromAssembly(assembly);
        services.Scan(scan => scan
            .FromAssemblies(assembly)
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(ICommandHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IQueryHandler<,>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime()
            .AddClasses(classes => classes.AssignableTo(typeof(IDomainEventHandler<>)), publicOnly: false)
            .AsImplementedInterfaces()
            .WithScopedLifetime());
        services.TryDecorate(typeof(ICommandHandler<>), typeof(ValidationDecorator<>));
        services.TryDecorate(typeof(ICommandHandler<,>), typeof(ValidationDecorator<,>));
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        return services;
    }
}