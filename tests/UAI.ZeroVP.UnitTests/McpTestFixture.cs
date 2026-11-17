using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Client;
using UAI.ZeroVP.Data.InMemory;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Shared test fixture starting the ASP.NET Core API on a dynamic Kestrel port
/// with sample data seeded in memory and disk persistence disabled.
/// </summary>
/// <remarks>
/// Exactly one host is built and started. <see cref="WebApplicationFactory{TEntryPoint}.Services"/>
/// and the MCP endpoint at <see cref="ServerAddress"/> therefore share one service provider
/// and one in-memory repository. The host is owned by <see cref="WebApplicationFactory{TEntryPoint}"/>,
/// which stops and disposes it in <see cref="WebApplicationFactory{TEntryPoint}.DisposeAsync"/>.
/// </remarks>
public sealed class McpTestFixture : WebApplicationFactory<Program>
{
    /// <summary>Switches the factory from the in-memory TestServer to a real Kestrel server.</summary>
    public McpTestFixture()
    {
        // Built-in Kestrel mode (.NET 10): the factory adds UseKestrel() to the host builder
        // and no longer expects a TestServer behind the single host returned by CreateHost.
        UseKestrel();
    }

    /// <summary>Base address of the running Kestrel server, e.g. <c>http://127.0.0.1:51234</c>.</summary>
    public string ServerAddress { get; private set; } = string.Empty;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Todo:StorageFilePath", "");
        builder.UseSetting("Todo:SeedSampleData", "true");

        builder.ConfigureServices(services =>
        {
            services.PostConfigure<InMemoryTodoOptions>(options =>
            {
                options.SeedSampleData = true;
                options.StorageFilePath = null;
            });
        });

        // Port 0 lets the OS pick a free port, so parallel test runs never collide.
        builder.UseUrls("http://127.0.0.1:0");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = builder.Build();
        host.Start();

        var addresses = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("The server does not expose IServerAddressesFeature.");
        ServerAddress = addresses.Addresses.First();

        return host;
    }

    /// <summary>Creates a connected MCP client pointing to the dynamic test server.</summary>
    public async Task<McpClient> CreateMcpClientAsync()
    {
        StartServer();

        var transport = new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = new Uri($"{ServerAddress}/mcp"),
            TransportMode = HttpTransportMode.AutoDetect
        });

        return await McpClient.CreateAsync(transport);
    }
}
