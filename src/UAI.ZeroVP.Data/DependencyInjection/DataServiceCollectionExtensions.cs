using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.InMemory;

namespace UAI.ZeroVP.Data.DependencyInjection;

/// <summary>
/// Registrace datové vrstvy do kontejneru služeb.
/// API tak nemusí znát konkrétní implementaci úložiště – zná jen <see cref="ITodoRepository"/>.
/// </summary>
public static class DataServiceCollectionExtensions
{
    /// <summary>
    /// Zaregistruje paměťové úložiště úkolů jako singleton.
    /// <para>
    /// Singleton je zde nutnost: data drží samotná instance repozitáře,
    /// takže scoped registrace by znamenala prázdné úložiště pro každý požadavek.
    /// </para>
    /// <para>
    /// <see cref="TodoStorageHostedService"/> loads the tasks from
    /// <see cref="InMemoryTodoOptions.StorageFilePath"/> on startup and saves them on shutdown.
    /// </para>
    /// </summary>
    /// <param name="services">Kolekce služeb.</param>
    /// <param name="configure">Volitelná úprava nastavení úložiště.</param>
    public static IServiceCollection AddInMemoryTodoData(
        this IServiceCollection services,
        Action<InMemoryTodoOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new InMemoryTodoOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);
        services.AddSingleton(sp => new InMemoryTodoRepository(
            sp.GetRequiredService<InMemoryTodoOptions>(),
            sp.GetService<TimeProvider>(),
            sp.GetService<ILogger<InMemoryTodoRepository>>()));
        services.AddSingleton<ITodoRepository>(sp => sp.GetRequiredService<InMemoryTodoRepository>());
        services.AddHostedService<TodoStorageHostedService>();

        return services;
    }
}
