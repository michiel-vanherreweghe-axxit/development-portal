using Domain.Watches;
using Xunit;

namespace UnitTests.Domain;

public class WatchTests
{
    [Theory]
    [InlineData("axxitbe/development-portal", true)]
    [InlineData("owner/repo.name_1-2", true)]
    [InlineData("owner/repo/../../secrets", false)]
    [InlineData("owner/repo?per_page=1", false)]
    [InlineData("no-slash", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidFullName_ForEveryShape_AcceptsOnlyOwnerSlashName(string? fullName, bool expected)
    {
        // Act
        var isValid = Watch.IsValidFullName(fullName);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Theory]
    [InlineData("release-api.yml", true)]
    [InlineData("ci.yaml", true)]
    [InlineData("../../etc/passwd", false)]
    [InlineData("nested/ci.yml", false)]
    [InlineData("..", false)]
    [InlineData("ci.txt", false)]
    [InlineData("", false)]
    public void IsValidWorkflowFile_ForEveryShape_AcceptsOnlyAFileName(string workflowFile, bool expected)
    {
        // Act
        var isValid = Watch.IsValidWorkflowFile(workflowFile);

        // Assert
        Assert.Equal(expected, isValid);
    }

    [Fact]
    public void Create_WithAValidRepository_SplitsOwnerAndRepository()
    {
        // Act
        var result = Watch.Create(1, "axxitbe/development-portal", "release-api.yml", "Release Api", 0);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("axxitbe", result.Value.Owner);
        Assert.Equal("development-portal", result.Value.Repository);
        Assert.Equal("axxitbe/development-portal", result.Value.FullName);
    }

    [Fact]
    public void Create_WithAMalformedRepository_ReturnsAValidationError()
    {
        // Act
        var result = Watch.Create(1, "not-a-repository", "release-api.yml", null, 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal("Watch.RepositoryInvalid", result.ValidationErrors.First().ErrorCode);
    }
}
