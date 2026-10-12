using System.Diagnostics;
using System.Globalization;

namespace UAI.ZeroVP.API.Middleware;

/// <summary>
/// Měří dobu zpracování každého HTTP požadavku. Výsledek posílá klientovi
/// v hlavičce <c>X-Response-Time-Ms</c> a při překročení limitu zapíše varování do logu.
/// </summary>
public sealed class ResponseTimeMiddleware
{
    /// <summary>Název hlavičky s naměřenou dobou odezvy v milisekundách.</summary>
    public const string HeaderName = "X-Response-Time-Ms";

    /// <summary>Limit odezvy dle zadání – nad ním se loguje varování.</summary>
    public const long ThresholdMs = 500;

    private readonly RequestDelegate _next;
    private readonly ILogger<ResponseTimeMiddleware> _logger;

    /// <summary>Vytvoří middleware; <paramref name="next"/> je další krok pipeline.</summary>
    public ResponseTimeMiddleware(RequestDelegate next, ILogger<ResponseTimeMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Zpracuje požadavek a změří, jak dlouho trval.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        // Hlavičky lze měnit jen do chvíle, než se začne odesílat tělo odpovědi,
        // proto se hodnota zapisuje těsně před odesláním hlaviček.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] =
                stopwatch.Elapsed.TotalMilliseconds.ToString("0.0000", CultureInfo.InvariantCulture);
            return Task.CompletedTask;
        });

        await _next(context);

        stopwatch.Stop();

        if (stopwatch.ElapsedMilliseconds > ThresholdMs)
        {
            _logger.LogWarning(
                "Pomalá odezva: HTTP {RequestMethod} {RequestPath} trval {ElapsedMs} ms (limit {ThresholdMs} ms)",
                context.Request.Method,
                context.Request.Path,
                stopwatch.ElapsedMilliseconds,
                ThresholdMs);
        }
    }
}
