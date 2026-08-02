using Microsoft.Extensions.DependencyInjection;

namespace AnimeBackend.Application.Events;

// Extension point for the future trigger engine (tag workflows in the GitHub
// Actions spirit: event -> conditions -> actions). Mutation handlers publish
// these after a successful save; today there are zero subscribers, the rule
// engine will be the first.
public sealed record TagsChanged(string MediaId, IReadOnlyList<Guid> TagIds);

public sealed record ProgressChanged(string MediaId, double? Score);

public interface IDomainEventHandler<in TEvent>
{
    Task HandleAsync(TEvent domainEvent, CancellationToken ct);
}

public sealed class DomainEventDispatcher(IServiceProvider services)
{
    public async Task PublishAsync<TEvent>(TEvent domainEvent, CancellationToken ct)
    {
        foreach (var handler in services.GetServices<IDomainEventHandler<TEvent>>())
            await handler.HandleAsync(domainEvent, ct);
    }
}
