using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.Infrastructure.Messaging;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Rebus.Config;
using Rebus.Routing.TypeBased;
using Rebus.Transport.InMem;

namespace Ambev.DeveloperEvaluation.IoC.ModuleInitializers;

public class InfrastructureModuleInitializer : IModuleInitializer
{
    public void Initialize(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<DefaultContext>());

        // Repositories
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ISaleRepository, SaleRepository>();

        // Event publisher — Rebus with InMemory transport (swap for RabbitMQ/ASB via config only)
        builder.Services.AddScoped<IEventPublisher, RebusEventPublisher>();

        builder.Services.AddRebus(configure => configure
            .Transport(t => t.UseInMemoryTransport(new InMemNetwork(), "developer-evaluation"))
            .Routing(r => r.TypeBased())
        );
    }
}
