using Microsoft.Extensions.Hosting;

namespace UAI.ZeroVP.Data.InMemory;

/// <summary>
/// Ties the JSON file of <see cref="InMemoryTodoRepository"/> to the application lifetime.
/// <para>
/// Startup: taking the repository in the constructor creates it while the host starts,
/// so the tasks are loaded from the file before the first request.
/// </para>
/// <para>
/// Shutdown: the tasks are saved in <see cref="StoppedAsync"/>, which runs after all hosted
/// services – including the web server – have stopped, so no request can change the data
/// after it has been written.
/// </para>
/// </summary>
public sealed class TodoStorageHostedService : IHostedLifecycleService
{
    private readonly InMemoryTodoRepository _repository;

    /// <summary>Creates the service for the given repository.</summary>
    /// <param name="repository">Repository whose tasks are saved on shutdown.</param>
    public TodoStorageHostedService(InMemoryTodoRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public Task StartingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task StoppedAsync(CancellationToken cancellationToken) =>
        // The token only signals that the shutdown timeout is running out; the file is small,
        // and aborting the write half-way would lose the data, so it is not passed on.
        _repository.SaveToFileAsync(CancellationToken.None);
}
