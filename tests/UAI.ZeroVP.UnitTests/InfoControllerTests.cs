using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Moq;
using System.Diagnostics;
using UAI.ZeroVP.API.Controllers;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.DTO;
using Xunit;

namespace UAI.ZeroVP.API.Tests.Controllers;

public class InfoControllerTests
{
    // Mocky pro závislosti
    private readonly Mock<ITodoRepository> _repositoryMock;
    private readonly Mock<IHostEnvironment> _environmentMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    
    // System Under Test (to co testujeme)
    private readonly InfoController _sut;

    public InfoControllerTests()
    {
        // Inicializace mocků
        _repositoryMock = new Mock<ITodoRepository>();
        _environmentMock = new Mock<IHostEnvironment>();
        _timeProviderMock = new Mock<TimeProvider>();

        // Vytvoření instance kontroleru s mocknutými závislostmi
        _sut = new InfoController(
            _repositoryMock.Object,
            _environmentMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task Get_ReturnsOkResult_WithExpectedInstanceInfo()
    {
        // Arrange (Příprava)
        var expectedEnvironment = "TestingEnvironment";
        var expectedTodoCount = 42;
        var expectedMachineName = Environment.MachineName; // Reálný název stroje, na kterém běží test

        // Pro zabránění záporného Uptimu musíme nastavit mockovaný čas do budoucnosti, 
        // protože Process.StartTime ukazuje na reálný čas spuštění testovacího runneru.
        var mockUtcNow = DateTimeOffset.UtcNow.AddHours(1);

        _environmentMock.Setup(e => e.EnvironmentName).Returns(expectedEnvironment);
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(mockUtcNow);
        _repositoryMock.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
                       .ReturnsAsync(expectedTodoCount);

        // Act (Akce)
        var actionResult = await _sut.Get(CancellationToken.None);

        // Assert (Ověření - za použití FluentAssertions pro lepší čitelnost)
        
        // 1. Ověříme, že návratový typ je 200 OK (OkObjectResult)
        var okResult = actionResult.Result.Should().BeOfType<OkObjectResult>().Subject;
        
        // 2. Ověříme, že datový typ uvnitř je náš DTO
        var dto = okResult.Value.Should().BeOfType<InstanceInfoDto>().Subject;

        // 3. Rozpad vlastností DTO a jejich kontrola
        dto.Should().NotBeNull();
        dto.MachineName.Should().Be(expectedMachineName);
        dto.Environment.Should().Be(expectedEnvironment);
        dto.TodoCount.Should().Be(expectedTodoCount);
        dto.UtcNow.Should().Be(mockUtcNow);
        
        // Pro Assembly verzi pouze ověříme, že se naplnila (v testu se bere verze testovací assembly)
        dto.Version.Should().NotBeNullOrWhiteSpace(); 
        dto.Version.Should().NotBe("neznámá");

        // Uptime musí být validní (kladný časový úsek)
        dto.Uptime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Get_PassesCancellationToken_ToRepository()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        // Nastavíme jen to nejnutnější, aby metoda nehavarovala na NullReference
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(DateTimeOffset.UtcNow.AddHours(1));

        // Act
        await _sut.Get(token);

        // Assert
        // Profesionální kontrola: Skutečně jsme Cancellation token předali dál do databáze?
        _repositoryMock.Verify(r => r.CountAsync(token), Times.Once, 
            "CancellationToken musí být předán do repozitáře pro správné ukončení probíhajících dotazů.");
    }

    [Fact]
    public async Task Get_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var expectedException = new TimeoutException("Database is dead");
        
        // Simulace havárie databáze
        _repositoryMock.Setup(r => r.CountAsync(It.IsAny<CancellationToken>()))
                       .ThrowsAsync(expectedException);
                       
        // Act
        // Zachytíme funkci jako delegát, abychom mohli testovat, že vyhodí výjimku
        Func<Task> act = async () => await _sut.Get(CancellationToken.None);

        // Assert
        // Kontroler by neměl výjimku spolknout, ale nechat ji probublat do middleware
        await act.Should().ThrowAsync<TimeoutException>()
                 .WithMessage("Database is dead");
    }
}   