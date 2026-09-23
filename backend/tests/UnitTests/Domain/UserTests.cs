using Domain.Users;
using Xunit;

namespace UnitTests.Domain;

public class UserTests
{
    [Fact]
    public void Create_WithAGitHubAccount_StartsAtTheDefaultRunLimit()
    {
        // Act
        var result = User.Create(4242, "tester", "https://avatars/1", DateTimeOffset.UtcNow);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(User.DefaultRunLimit, result.Value!.RunLimit);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(User.MaximumRunLimit + 1)]
    public void UpdateRunLimit_OutsideTheAllowedRange_IsRejected(int runLimit)
    {
        // Arrange
        var user = User.Create(4242, "tester", null, DateTimeOffset.UtcNow).Value!;

        // Act
        var result = user.UpdateRunLimit(runLimit);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("User.RunLimitOutOfRange", result.ValidationErrors.First().ErrorCode);
        Assert.Equal(User.DefaultRunLimit, user.RunLimit);
    }

    [Fact]
    public void SignIn_WithAChangedLogin_KeepsTheAccountAndStampsTheMoment()
    {
        // Arrange
        var user = User.Create(4242, "old-login", null, DateTimeOffset.UtcNow.AddDays(-1)).Value!;
        var signedInAt = DateTimeOffset.UtcNow;

        // Act
        var result = user.SignIn("new-login", "https://avatars/2", signedInAt);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("new-login", user.Login);
        Assert.Equal(signedInAt, user.LastSignedInAt);
    }
}
