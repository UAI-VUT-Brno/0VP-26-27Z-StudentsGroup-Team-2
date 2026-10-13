using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Moq;
using UAI.ZeroVP.API.Middleware;

namespace UAI.ZeroVP.UnitTests;

public class ResponseTimeMiddlewareTests
{
    private readonly Mock<ILogger<ResponseTimeMiddleware>> _loggerMock = new();

    [Fact]
    public async Task InvokeAsync_AddsResponseTimeHeader()
    {
        // Arrange
        var (context, responseFeature) = CreateContext();
        var sut = new ResponseTimeMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await sut.InvokeAsync(context);
        await responseFeature.StartAsync(); // simulace začátku odesílání odpovědi

        // Assert
        context.Response.Headers.Should().ContainKey(ResponseTimeMiddleware.HeaderName);
        var value = context.Response.Headers[ResponseTimeMiddleware.HeaderName].ToString();
        value.Should().NotBeNullOrWhiteSpace();
        double.Parse(value, System.Globalization.CultureInfo.InvariantCulture).Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task InvokeAsync_LogsWarning_WhenResponseIsSlow()
    {
        // Arrange
        var (context, _) = CreateContext();
        var sut = new ResponseTimeMiddleware(
            _ => Task.Delay(TimeSpan.FromMilliseconds(ResponseTimeMiddleware.ThresholdMs + 100)),
            _loggerMock.Object);

        // Act
        await sut.InvokeAsync(context);

        // Assert
        VerifyWarningLogged(Times.Once());
    }

    [Fact]
    public async Task InvokeAsync_DoesNotLogWarning_WhenResponseIsFast()
    {
        // Arrange
        var (context, _) = CreateContext();
        var sut = new ResponseTimeMiddleware(_ => Task.CompletedTask, _loggerMock.Object);

        // Act
        await sut.InvokeAsync(context);

        // Assert
        VerifyWarningLogged(Times.Never());
    }

    private void VerifyWarningLogged(Times times) =>
        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);

    private static (DefaultHttpContext Context, TestResponseFeature ResponseFeature) CreateContext()
    {
        // DefaultHttpContext callbacky z OnStarting sám nikdy nespustí,
        // proto se podstrčí feature, která si je uloží a test je zavolá ručně.
        var responseFeature = new TestResponseFeature();
        var context = new DefaultHttpContext();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/api/todos";
        return (context, responseFeature);
    }

    private sealed class TestResponseFeature : HttpResponseFeature
    {
        private readonly List<(Func<object, Task> Callback, object State)> _onStarting = new();

        public override void OnStarting(Func<object, Task> callback, object state) =>
            _onStarting.Add((callback, state));

        public async Task StartAsync()
        {
            foreach (var (callback, state) in _onStarting)
            {
                await callback(state);
            }
        }
    }
}
