using Ambev.DeveloperEvaluation.Domain.Repositories;
using Ambev.DeveloperEvaluation.Domain.Services;
using Ambev.DeveloperEvaluation.ORM;
using Ambev.DeveloperEvaluation.ORM.Interceptors;
using Ambev.DeveloperEvaluation.Infrastructure.Messaging;
using Ambev.DeveloperEvaluation.ORM.Repositories;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Ambev.DeveloperEvaluation.IoC.ModuleInitializers;

public class InfrastructureModuleInitializer : IModuleInitializer
{
    public void Initialize(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<OutboxInterceptor>();

        builder.Services.AddDbContext<DefaultContext>((sp, options) =>
            options.UseNpgsql(
                builder.Configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("Ambev.DeveloperEvaluation.ORM"))
            .AddInterceptors(sp.GetRequiredService<OutboxInterceptor>()));

        builder.Services.AddScoped<DbContext>(provider => provider.GetRequiredService<DefaultContext>());

        // Repositories
        builder.Services.AddScoped<IUserRepository, UserRepository>();
        builder.Services.AddScoped<ISaleRepository, SaleRepository>();

        // Outbox options — configurable via appsettings.json "Outbox" section
        builder.Services.Configure<OutboxOptions>(
            builder.Configuration.GetSection(OutboxOptions.SectionName));

        // Event publisher — structured JSON logging (honest default: no broker dependency).
        // To integrate a real broker, register a different IEventPublisher implementation here:
        //   e.g. builder.Services.AddScoped<IEventPublisher, RabbitMqEventPublisher>();
        builder.Services.AddScoped<IEventPublisher, LoggingEventPublisher>();

        // Outbox background services
        builder.Services.AddHostedService<OutboxProcessor>();
        builder.Services.AddHostedService<OutboxCleanupJob>();
    }
}
