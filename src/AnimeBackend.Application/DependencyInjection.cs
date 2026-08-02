using AnimeBackend.Application.Events;
using AnimeBackend.Application.Library;
using AnimeBackend.Application.Notes;
using AnimeBackend.Application.Search;
using AnimeBackend.Application.Tags;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeBackend.Application;

public static class DependencyInjection
{
    // Handlers are plain classes resolved by concrete type — controllers ask
    // for exactly what they call, no dispatcher indirection.
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<SearchHandler>();
        services.AddScoped<GetLibraryHandler>();
        services.AddScoped<GetDetailHandler>();
        services.AddScoped<UpdateProgressHandler>();
        services.AddScoped<ReplaceTagsHandler>();
        services.AddScoped<BulkTagsHandler>();
        services.AddScoped<TagsHandler>();
        services.AddScoped<NotesHandler>();
        services.AddScoped<Ai.AiHandlers>();
        services.AddScoped<Ai.AiConversation>();
        services.AddScoped<Ai.AiToolbox>();
        services.AddScoped<Ai.AiModelCatalog>();
        services.AddScoped<AttachmentsHandler>();
        services.AddScoped<Materializer>();
        services.AddScoped<DomainEventDispatcher>();
        return services;
    }
}
