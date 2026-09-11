using Events.Application.Interfaces;
using Events.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using EventsServiceImpl = Events.Application.Services.EventService;

namespace Events.Application.DependencyInjection;

public static class ApplicationServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventsServiceImpl>();

        return services;
    }
}
