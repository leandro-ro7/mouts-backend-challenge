using Ambev.DeveloperEvaluation.Application;
using Ambev.DeveloperEvaluation.Common.Security;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Ambev.DeveloperEvaluation.IoC.ModuleInitializers;

public class ApplicationModuleInitializer : IModuleInitializer
{
    public void Initialize(WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // Register all FluentValidation validators from Application assembly.
        // This activates the ValidationBehavior MediatR pipeline — no manual `new Validator()`
        // calls are needed in handlers.
        builder.Services.AddValidatorsFromAssembly(
            typeof(ApplicationLayer).Assembly,
            ServiceLifetime.Scoped);
    }
}
