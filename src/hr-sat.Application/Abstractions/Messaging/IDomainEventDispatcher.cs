using hr_sat.Domain;

namespace hr_sat.Application.Abstractions.Messaging;

public interface IDomainEventDispatcher
{
    Task DispatchAsync(
        IReadOnlyCollection<IDomainEvent> domainEvents,
        CancellationToken cancellationToken);
}