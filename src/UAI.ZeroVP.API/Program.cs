using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using UAI.ZeroVP.Data.DependencyInjection;
using UAI.ZeroVP.Data.InMemory;
using Serilog;
using Serilog.Events;

Log.Logger = new LoggerConfiguration()
    // Zaznamenává zprávy od úrovně 'Information' výše (Info, Warning, Error, Fatal).
    .MinimumLevel.Information()
    // globalne zaznamenavame zpravu od urovne 'Information', ale pro tento framework az od urovne Warning (Warning, Error, Fatal)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    // Umožňuje automaticky připojovat k záznamům dynamická metadata
    .Enrich.FromLogContext()
    // Vypis do terminalu
    .WriteTo.Console()
    // Zapis do souboru /logs
    .WriteTo.File(
        // umisteni cilove slozky
        path: "logs/app-.log",
        // kazdy den o se vytvori novy soubor pro logs
        rollingInterval: RollingInterval.Day,
        // presna struktura 
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [Event: {SourceContext}] Result: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.ForContext("SourceContext", "Program").Information("Aplikace startuje");

    var builder = WebApplication.CreateBuilder(args);

    // Předání správy logování frameworku do rukou Serilogu
    builder.Host.UseSerilog();

    // --- Služby ---------------------------------------------------------------

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            // Výčty posílá API jako řetězce ("High"), ne jako čísla – čitelnější v curl i ve Swaggeru.
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

    // Jednotné chybové odpovědi ve formátu ProblemDetails (RFC 9457).
    builder.Services.AddProblemDetails();

    // Zdroj času jako služba – kontrolery i repozitář si ho berou z DI.
    builder.Services.AddSingleton(TimeProvider.System);

    // Datová vrstva: paměťové úložiště. Nastavení se čte ze sekce "Todo",
    // v kontejneru ho lze přepsat proměnnou prostředí Todo__SeedSampleData.
    builder.Services.AddInMemoryTodoData(options =>
        builder.Configuration.GetSection(InMemoryTodoOptions.SectionName).Bind(options));

    // Kontrola zdraví aplikace – později ji využije HEALTHCHECK v Dockerfile.
    builder.Services.AddHealthChecks();

    // Popis API ve formátu OpenAPI. Dokument generuje vestavěná knihovna .NET 10
    // (Microsoft.AspNetCore.OpenApi) a vystavuje ho na /openapi/v1.json.
    // Swagger UI je pak jen prohlížeč tohoto dokumentu – žádný druhý generátor.
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer((document, context, cancellationToken) =>
        {
            document.Info = new OpenApiInfo
            {
                Title = "UAI.ZeroVP – ToDo API",
                Version = "v1",
                Description = "Ukázkové API pro výuku Dockeru. Data jsou uložena pouze "
                            + "v paměti procesu a po zastavení kontejneru se ztratí."
            };

            return Task.CompletedTask;
        });
    });

    var app = builder.Build();

    // Swagger UI je ve vývoji zapnutý; v kontejneru (prostředí Production) se dá
    // zapnout proměnnou prostředí Swagger__Enabled=true.
    var swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment());

    // --- HTTP pipeline --------------------------------------------------------

    // Bez přesměrování na HTTPS: v kontejneru se vystavuje pouze HTTP port
    // a TLS řeší až reverzní proxy před ním.
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    // Tento řádek zachytí veškeré aktivity na API:
    app.UseSerilogRequestLogging(options =>
    {
        options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    });

    if (swaggerEnabled)
    {
        // Strojově čitelný popis API.
        app.MapOpenApi();

        // Interaktivní dokumentace nad tímto popisem – http://localhost:5258/swagger
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/openapi/v1.json", "UAI.ZeroVP – ToDo API v1");
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "UAI.ZeroVP – ToDo API";
        });
    }

    app.MapHealthChecks("/health");
    app.MapControllers();

    // Kořen vede na Swagger, ať má "docker run" hned co ukázat.
    // Když je Swagger vypnutý, přesměruje rovnou na seznam úkolů.
    app.MapGet("/", () => Results.Redirect(swaggerEnabled ? "/swagger" : "/api/todos"))
    .ExcludeFromDescription();

    app.Run();
}
catch (Exception ex)
{
    // zaznamenani fatalni chyby pri startu
    Log.ForContext("SourceContext", "Program").Fatal(ex, "Aplikace selhala při startu");
    throw;
}
finally
{
    Log.ForContext("SourceContext", "Program").Information("Aplikace ukončena");
    Log.CloseAndFlush();
}