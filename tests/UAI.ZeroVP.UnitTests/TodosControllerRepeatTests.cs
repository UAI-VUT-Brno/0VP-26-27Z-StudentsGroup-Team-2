using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using UAI.ZeroVP.API.Controllers;
using UAI.ZeroVP.Data.Abstractions;
using UAI.ZeroVP.Data.Entities;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Unit tests for the recurring task creation endpoint (POST /api/todos/repeat).
/// Verifies the HTTP status code (201 Created), returned occurrences, repository interactions,
/// and model validation handling.
/// </summary>
public sealed class TodosControllerRepeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstDue = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private readonly Mock<ITodoRepository> _repository = new(MockBehavior.Strict);
    private readonly TodosController _controller;

    public TodosControllerRepeatTests()
    {
        _controller = new TodosController(
            _repository.Object,
            new FixedTimeProvider(Now),
            NullLogger<TodosController>.Instance);
    }

    private static RepeatTodoItemRequest CreateRequest(
        int repeatCount = 3,
        DateTimeOffset? firstDueAtUtc = null,
        string title = "  Sprint report  ",
        string? description = "  Weekly sprint summary  ",
        TodoPriority priority = TodoPriority.High) => new()
    {
        Title = title,
        Description = description,
        Priority = priority,
        FirstDueAtUtc = firstDueAtUtc ?? FirstDue,
        RepeatCount = repeatCount
    };

    private static (bool IsValid, IList<ValidationResult> Results) ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        var isValid = Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return (isValid, results);
    }

    // --- POST /api/todos/repeat - Success scenarios ---------------------------

    [Fact]
    public async Task Repeat_ValidRequest_ReturnsCreatedAtActionWithOccurrences()
    {
        IReadOnlyCollection<TodoItem>? capturedEntities = null;
        _repository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<TodoItem>, CancellationToken>((items, _) => capturedEntities = items)
            .ReturnsAsync((IReadOnlyCollection<TodoItem> items, CancellationToken _) => items.ToList());

        var request = CreateRequest(repeatCount: 3);

        var result = await _controller.Repeat(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Equal(nameof(TodosController.GetAll), created.ActionName);

        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(created.Value);

        // DELIBERATE TEMPORARY ERROR TO TEST CI GATE:
        // Expected count is 4, but assert 999 to verify GitHub Actions CI fails on test failure.
        Assert.Equal(999, dtos.Count);

        Assert.Equal(4, capturedEntities!.Count);

        // Verify unique IDs
        Assert.Equal(dtos.Count, dtos.Select(d => d.Id).Distinct().Count());

        // Verify weekly dates 7 days apart
        Assert.Equal(
            [FirstDue, FirstDue.AddDays(7), FirstDue.AddDays(14), FirstDue.AddDays(21)],
            dtos.Select(d => d.DueAtUtc!.Value));

        // Verify copied fields
        Assert.All(dtos, d =>
        {
            Assert.Equal("Sprint report", d.Title);
            Assert.Equal("Weekly sprint summary", d.Description);
            Assert.Equal(TodoPriority.High, d.Priority);
            Assert.False(d.IsCompleted);
            Assert.Null(d.CompletedAtUtc);
            Assert.Equal(Now, d.CreatedAtUtc);
        });

        _repository.Verify(
            r => r.AddRangeAsync(It.Is<IReadOnlyCollection<TodoItem>>(c => c.Count == 4), CancellationToken.None),
            Times.Once);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(52)]
    public async Task Repeat_BoundaryRepeatCount_StoresRepeatCountPlusOneItems(int repeatCount)
    {
        _repository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyCollection<TodoItem> items, CancellationToken _) => items.ToList());

        var request = CreateRequest(repeatCount: repeatCount);

        var result = await _controller.Repeat(request, CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dtos = Assert.IsAssignableFrom<IReadOnlyList<TodoItemDto>>(created.Value);

        Assert.Equal(repeatCount + 1, dtos.Count);
        Assert.Equal(FirstDue.AddDays(7 * repeatCount), dtos[^1].DueAtUtc);

        _repository.Verify(
            r => r.AddRangeAsync(It.Is<IReadOnlyCollection<TodoItem>>(c => c.Count == repeatCount + 1), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Repeat_PassesCancellationTokenToRepository()
    {
        using var cts = new CancellationTokenSource();
        _repository
            .Setup(r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), cts.Token))
            .ReturnsAsync((IReadOnlyCollection<TodoItem> items, CancellationToken _) => items.ToList());

        var request = CreateRequest(repeatCount: 1);

        await _controller.Repeat(request, cts.Token);

        _repository.Verify(
            r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), cts.Token),
            Times.Once);
    }

    // --- Validation & Error Handling (Invalid requests create no tasks) --------

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    [InlineData(-1)]
    public void InvalidRequest_RepeatCountOutOfRange_FailsValidation_AndRepositoryNeverCalled(int repeatCount)
    {
        var request = CreateRequest(repeatCount: repeatCount);

        var (isValid, results) = ValidateModel(request);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RepeatTodoItemRequest.RepeatCount)));
        _repository.Verify(
            r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void InvalidRequest_MissingFirstDueAtUtc_FailsValidation_AndRepositoryNeverCalled()
    {
        var request = CreateRequest(firstDueAtUtc: null) with { FirstDueAtUtc = null };

        var (isValid, results) = ValidateModel(request);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RepeatTodoItemRequest.FirstDueAtUtc)));
        _repository.Verify(
            r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void InvalidRequest_EmptyTitle_FailsValidation_AndRepositoryNeverCalled(string title)
    {
        var request = CreateRequest(title: title);

        var (isValid, results) = ValidateModel(request);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RepeatTodoItemRequest.Title)));
        _repository.Verify(
            r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void InvalidRequest_TitleExceedingMaxLength_FailsValidation_AndRepositoryNeverCalled()
    {
        var request = CreateRequest(title: new string('A', 201));

        var (isValid, results) = ValidateModel(request);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RepeatTodoItemRequest.Title)));
        _repository.Verify(
            r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public void InvalidRequest_DescriptionExceedingMaxLength_FailsValidation_AndRepositoryNeverCalled()
    {
        var request = CreateRequest(description: new string('B', 1001));

        var (isValid, results) = ValidateModel(request);

        Assert.False(isValid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(RepeatTodoItemRequest.Description)));
        _repository.Verify(
            r => r.AddRangeAsync(It.IsAny<IReadOnlyCollection<TodoItem>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
