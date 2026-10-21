using System.ComponentModel.DataAnnotations;
using UAI.ZeroVP.API.Mapping;
using UAI.ZeroVP.DTO;

namespace UAI.ZeroVP.UnitTests;

/// <summary>
/// Testy týdenní řady úkolů – výpočet termínů v <c>ToEntities</c>,
/// mapování <c>DueAtUtc</c> do DTO a validační atributy <see cref="RepeatTodoItemRequest"/>.
/// </summary>
public sealed class TodoMapperRepeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 2, 12, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FirstDue = new(2026, 10, 19, 9, 0, 0, TimeSpan.Zero);

    private static RepeatTodoItemRequest CreateRequest(int repeatCount = 3, DateTimeOffset? firstDueAtUtc = null) => new()
    {
        Title = "  Report  ",
        Description = "  Týdenní shrnutí  ",
        Priority = TodoPriority.High,
        FirstDueAtUtc = firstDueAtUtc ?? FirstDue,
        RepeatCount = repeatCount
    };

    private static bool IsValid(RepeatTodoItemRequest request) =>
        Validator.TryValidateObject(request, new ValidationContext(request), new List<ValidationResult>(), validateAllProperties: true);

    // --- ToEntities -------------------------------------------------------------

    [Fact]
    public void ToEntities_RepeatCountThree_CreatesFourItemsSevenDaysApart()
    {
        var items = CreateRequest(repeatCount: 3).ToEntities(Now);

        Assert.Equal(
            [FirstDue, FirstDue.AddDays(7), FirstDue.AddDays(14), FirstDue.AddDays(21)],
            items.Select(i => i.DueAtUtc!.Value));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(52)]
    public void ToEntities_Boundaries_CreateRepeatCountPlusOneItems(int repeatCount)
    {
        var items = CreateRequest(repeatCount).ToEntities(Now);

        Assert.Equal(repeatCount + 1, items.Count);
        Assert.Equal(FirstDue.AddDays(7 * repeatCount), items[^1].DueAtUtc);
    }

    [Fact]
    public void ToEntities_RollsOverMonthAndYear()
    {
        var items = CreateRequest(repeatCount: 2, firstDueAtUtc: new DateTimeOffset(2026, 12, 25, 9, 0, 0, TimeSpan.Zero)).ToEntities(Now);

        Assert.Equal(new DateTimeOffset(2027, 1, 1, 9, 0, 0, TimeSpan.Zero), items[1].DueAtUtc);
        Assert.Equal(new DateTimeOffset(2027, 1, 8, 9, 0, 0, TimeSpan.Zero), items[2].DueAtUtc);
    }

    [Fact]
    public void ToEntities_ConvertsFirstDueToUtc()
    {
        var items = CreateRequest(repeatCount: 1, firstDueAtUtc: new DateTimeOffset(2026, 10, 19, 11, 0, 0, TimeSpan.FromHours(2))).ToEntities(Now);

        Assert.Equal(TimeSpan.Zero, items[0].DueAtUtc!.Value.Offset);
        Assert.Equal(FirstDue, items[0].DueAtUtc);
    }

    [Fact]
    public void ToEntities_GivesEachItemOwnId()
    {
        var items = CreateRequest(repeatCount: 5).ToEntities(Now);

        Assert.Equal(items.Count, items.Select(i => i.Id).Distinct().Count());
        Assert.DoesNotContain(Guid.Empty, items.Select(i => i.Id));
    }

    [Fact]
    public void ToEntities_CopiesTrimmedFieldsAndStartsIncomplete()
    {
        foreach (var item in CreateRequest().ToEntities(Now))
        {
            Assert.Equal("Report", item.Title);
            Assert.Equal("Týdenní shrnutí", item.Description);
            Assert.Equal(TodoPriority.High, item.Priority);
            Assert.False(item.IsCompleted);
            Assert.Null(item.CompletedAtUtc);
            Assert.Equal(Now, item.CreatedAtUtc);
            Assert.Equal(Now, item.UpdatedAtUtc);
        }
    }

    [Fact]
    public void ToEntities_WhitespaceDescription_BecomesNull()
    {
        var request = CreateRequest() with { Description = "   " };

        Assert.All(request.ToEntities(Now), i => Assert.Null(i.Description));
    }

    // --- DueAtUtc v DTO ---------------------------------------------------------

    [Fact]
    public void ToDto_MapsDueAtUtc()
    {
        var entity = CreateRequest(repeatCount: 1).ToEntities(Now)[1];

        Assert.Equal(FirstDue.AddDays(7), entity.ToDto().DueAtUtc);
    }

    [Fact]
    public void ToDto_TaskCreatedWithoutDueDate_HasNullDueAtUtc()
    {
        var entity = new CreateTodoItemRequest { Title = "Úkol" }.ToEntity(Now);

        Assert.Null(entity.ToDto().DueAtUtc);
    }

    // --- Validace požadavku -----------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(52)]
    public void Validation_AcceptsRepeatCountBoundaries(int repeatCount)
    {
        Assert.True(IsValid(CreateRequest(repeatCount)));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    [InlineData(-1)]
    public void Validation_RejectsRepeatCountOutOfRange(int repeatCount)
    {
        Assert.False(IsValid(CreateRequest(repeatCount)));
    }

    [Fact]
    public void Validation_RejectsMissingFirstDueAtUtc()
    {
        Assert.False(IsValid(CreateRequest() with { FirstDueAtUtc = null }));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Validation_RejectsMissingTitle(string? title)
    {
        Assert.False(IsValid(CreateRequest() with { Title = title! }));
    }

    [Fact]
    public void Validation_RejectsTooLongTitleAndDescription()
    {
        Assert.False(IsValid(CreateRequest() with { Title = new string('x', 201) }));
        Assert.False(IsValid(CreateRequest() with { Description = new string('x', 1001) }));
    }

    [Fact]
    public void Validation_RejectsUndefinedPriority()
    {
        Assert.False(IsValid(CreateRequest() with { Priority = (TodoPriority)999 }));
    }
}
