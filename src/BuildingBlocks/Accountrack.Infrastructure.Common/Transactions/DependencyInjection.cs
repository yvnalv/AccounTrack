using Accountrack.Modules.Contracts.Transactions;
using Microsoft.Extensions.DependencyInjection;

namespace Accountrack.Infrastructure.Common.Transactions;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the shared connection + cross-module unit of work (INTEGRATION_EVENTS.md §2).
    /// Modules that take part in atomic flows resolve <see cref="ISharedDbConnection"/> for their
    /// DbContext and register themselves as <see cref="ITransactionalDbContext"/>.
    /// </summary>
    public static IServiceCollection AddCrossModuleTransactions(
        this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISharedDbConnection>(_ => new SharedDbConnection(connectionString));
        services.AddScoped<ICrossModuleUnitOfWork, CrossModuleUnitOfWork>();
        return services;
    }
}
