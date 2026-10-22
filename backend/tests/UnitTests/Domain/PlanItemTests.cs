using Domain.Planning;
using Xunit;

namespace UnitTests.Domain;

public class PlanItemTests
{
    private static readonly DateOnly Sunday = new(2026, 10, 25);

    [Fact]
    public void CreateTodo_ForADay_KeepsTheDate()
    {
        // Act
        var result = PlanItem.CreateTodo(1, "  Write report ", "  ", PlanSpan.Day, Sunday);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Write report", result.Value.Title);
        Assert.Null(result.Value.Description);
        Assert.Equal(Sunday, result.Value.Start);
        Assert.False(result.Value.IsIssue);
    }

    [Theory]
    [InlineData(2026, 10, 25)]
    [InlineData(2026, 10, 19)]
    [InlineData(2026, 10, 22)]
    public void CreateTodo_ForAWeek_NormalisesStartToMonday(int year, int month, int day)
    {
        // Act
        var result = PlanItem.CreateTodo(1, "Plan", null, PlanSpan.Week, new DateOnly(year, month, day));

        // Assert
        Assert.Equal(new DateOnly(2026, 10, 19), result.Value.Start);
    }

    [Fact]
    public void MondayOf_AcrossAYearBoundary_ReturnsThePreviousYear()
    {
        // Act
        var monday = PlanItem.MondayOf(new DateOnly(2027, 1, 1));

        // Assert
        Assert.Equal(new DateOnly(2026, 12, 28), monday);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public void CreateTodo_WithoutATitle_ReturnsAValidationError(string? title)
    {
        // Act
        var result = PlanItem.CreateTodo(1, title, null, PlanSpan.Day, Sunday);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("PlanItem.TitleRequired", result.ValidationErrors.Single().ErrorCode);
    }

    [Fact]
    public void CreateTodo_WithTooLongTextFields_ReturnsBothValidationErrors()
    {
        // Act
        var result = PlanItem.CreateTodo(
            1,
            new string('a', PlanItem.TitleMaxLength + 1),
            new string('b', PlanItem.DescriptionMaxLength + 1),
            PlanSpan.Day,
            Sunday);

        // Assert
        Assert.Equal(
            ["PlanItem.TitleTooLong", "PlanItem.DescriptionTooLong"],
            result.ValidationErrors.Select(error => error.ErrorCode));
    }

    [Fact]
    public void CreateIssue_WithAValidReference_SplitsOwnerAndRepository()
    {
        // Act
        var result = PlanItem.CreateIssue(1, "axxitbe/portal", 12, "Fix it", PlanSpan.Day, Sunday);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("axxitbe", result.Value.Owner);
        Assert.Equal("portal", result.Value.Repository);
        Assert.Equal(12, result.Value.IssueNumber);
        Assert.True(result.Value.IsIssue);
    }

    [Fact]
    public void CreateIssue_WithAnInvalidReference_ReturnsValidationErrors()
    {
        // Act
        var result = PlanItem.CreateIssue(1, "no-slash", 0, "Fix it", PlanSpan.Day, Sunday);

        // Assert
        Assert.Equal(
            ["PlanItem.RepositoryInvalid", "PlanItem.IssueNumberInvalid"],
            result.ValidationErrors.Select(error => error.ErrorCode));
    }

    [Fact]
    public void CreateIssue_WithATitleLongerThanATodoAllows_CutsTheSnapshot()
    {
        // Act
        var result = PlanItem.CreateIssue(1, "axxitbe/portal", 12, new string('a', 250), PlanSpan.Day, Sunday);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(PlanItem.TitleMaxLength, result.Value.Title.Length);
    }

    [Fact]
    public void MoveTo_AWeek_NormalisesStartToMonday()
    {
        // Arrange
        var item = PlanItem.CreateTodo(1, "Plan", null, PlanSpan.Day, Sunday).Value;

        // Act
        item.MoveTo(PlanSpan.Week, new DateOnly(2026, 10, 28));

        // Assert
        Assert.Equal(PlanSpan.Week, item.Span);
        Assert.Equal(new DateOnly(2026, 10, 26), item.Start);
    }

    [Fact]
    public void SetCompleted_StampsAndClearsCompletedAt()
    {
        // Arrange
        var item = PlanItem.CreateTodo(1, "Plan", null, PlanSpan.Day, Sunday).Value;

        // Act
        item.SetCompleted(true);
        var completedAt = item.CompletedAt;
        item.SetCompleted(false);

        // Assert
        Assert.NotNull(completedAt);
        Assert.False(item.IsCompleted);
        Assert.Null(item.CompletedAt);
    }

    [Fact]
    public void Edit_OnATodo_ChangesTitleAndDescription()
    {
        // Arrange
        var item = PlanItem.CreateTodo(1, "Plan", null, PlanSpan.Day, Sunday).Value;

        // Act
        var result = item.Edit("Plan better", "More detail");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("Plan better", item.Title);
        Assert.Equal("More detail", item.Description);
    }

    [Fact]
    public void Edit_OnAnIssueEntry_IsRejected()
    {
        // Arrange
        var item = PlanItem.CreateIssue(1, "axxitbe/portal", 12, "Fix it", PlanSpan.Day, Sunday).Value;

        // Act
        var result = item.Edit("Other", null);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Fix it", item.Title);
        Assert.Equal("PlanItem.IssueNotEditable", result.ValidationErrors.Single().ErrorCode);
    }
}
