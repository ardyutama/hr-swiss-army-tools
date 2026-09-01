using hr_sat.Application.Abstractions.Messaging;
using hr_sat.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace hr_sat.Application;

internal sealed class DomainEventDispatcher(IServiceProvider serviceProvider)
    : IDomainEventDispatcher
{
    public async Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken)
    {
        foreach (var domainEvent in domainEvents)
        {
            var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
            var handlers = serviceProvider.GetServices(handlerType);
            foreach (var handler in handlers)
            {
                await ((dynamic)handler!).Handle((dynamic)domainEvent, cancellationToken);
            }
        }
    }
}