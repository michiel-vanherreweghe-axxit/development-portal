using Application.Common;
using Domain.Watches;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Services;

public class GitHubMapperTests
{
    [Fact]
    public void ToDto_ForARunThatIsStillGoing_FallsBackToItsStatus()
    {
        // Arrange
        var finished = new GitHubRun("success", "completed", "https://gh/1", DateTimeOffset.UtcNow, 2, "main");
        var running = new GitHubRun(null, "in_progress", "https://gh/2", DateTimeOffset.UtcNow, 1, "dev");

        // Act
        var mappedFinished = GitHubMapper.ToDto(finished);
        var mappedRunning = GitHubMapper.ToDto(running);

        // Assert
        Assert.Equal("success", mappedFinished.Conclusion);
        Assert.Equal("in_progress", mappedRunning.Conclusion);
        Assert.Equal("dev", mappedRunning.Branch);
    }

    [Fact]
    public void ToDto_ForAWorkflow_KeepsOnlyTheFileName()
    {
        // Arrange
        var workflow = new GitHubWorkflow("Release Api", ".github/workflows/release-api.yml");

        // Act
        var mapped = GitHubMapper.ToDto(workflow);

        // Assert
        Assert.Equal("release-api.yml", mapped.File);
    }

    [Fact]
    public void ToReviewState_WithSeveralReviewers_TakesTheLatestVerdictPerReviewer()
    {
        // Arrange: ann changed her mind, bob still wants changes.
        var reviews = new[]
        {
            Review("ann", "CHANGES_REQUESTED"),
            Review("ann", "APPROVED"),
            Review("bob", "CHANGES_REQUESTED"),
        };

        // Act
        var state = GitHubMapper.ToReviewState(reviews);

        // Assert
        Assert.Equal(ReviewState.ChangesRequested, state);
        Assert.Equal(ReviewState.Approved, GitHubMapper.ToReviewState(reviews[..2]));
        Assert.Equal(ReviewState.ReviewRequired, GitHubMapper.ToReviewState([Review("ann", "COMMENTED")]));
        Assert.Equal(ReviewState.ReviewRequired, GitHubMapper.ToReviewState([]));
    }

    private static GitHubReview Review(string login, string state) => new(state, new GitHubAccount(1, login, null));
}
