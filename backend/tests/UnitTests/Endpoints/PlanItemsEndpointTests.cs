using System.Net;
using System.Net.Http.Json;
using Application.Common;
using Ardalis.Result;
using Domain.Planning;
using Infrastructure.Services.GitHub;
using Infrastructure.Services.GitHub.Models;
using Xunit;

namespace UnitTests.Endpoints;

public class PlanItemsEndpointTests : IClassFixture<PortalApiFactory>
{
    private const string BaseUrl = "/api/v1/plan-items";

    // 2026-10-21 is a Wednesday; its week starts on Monday 2026-10-19.
    private static readonly DateOnly Wednesday = new(2026, 10, 21);
    private static readonly DateOnly Monday = new(2026, 10, 19);

    private readonly PortalApiFactory _factory;

    public PlanItemsEndpointTests(PortalApiFactory factory)
    {
        _factory = factory;

        // The fake is shared by the class; each test starts from the plain one.
        factory.GitHub.Issue = FakeGitHubService.DefaultIssue;
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Get_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?from=2026-10-19&to=2026-10-25", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForARange_ReturnsDayAndWeekItemsOfTheWeekOnly()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        await Seed(userId, "Today", PlanSpan.Day, Wednesday);
        await Seed(userId, "This week", PlanSpan.Week, Wednesday);
        await Seed(userId, "Next week", PlanSpan.Day, Wednesday.AddDays(7));

        // Act
        var items = await Read(client, "2026-10-21", "2026-10-25");

        // Assert
        Assert.Equal(["This week", "Today"], items.Select(item => item.Title).Order());
        Assert.Equal(Monday, items.Single(item => item.Title == "This week").Date);
    }

    [Theory]
    [InlineData("2026-10-25", "2026-10-19")]
    [InlineData("1900-01-01", "2026-10-19")]
    [InlineData("2026-01-01", "2027-12-31")]
    public async Task Get_WithAnInvalidRange_ReturnsBadRequest(string from, string to)
    {
        // Arrange
        var (client, _) = await _factory.CreateUserAsync();

        // Act
        var response = await client.GetAsync($"{BaseUrl}?from={from}&to={to}", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_ForAnIssueEntry_ReportsTheLiveStateAndRefreshesTheTitle()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/portal", 5, "Old title", PlanSpan.Day, Wednesday).Value);
        _factory.GitHub.Issue = (owner, repository, number) => Result<GitHubIssue>.Success(new GitHubIssue(
            number, "New title", "closed", $"https://gh/{owner}/{repository}/issues/{number}", "https://api.gh/repos/axxitbe/portal", null));

        // Act
        var items = await Read(client, "2026-10-19", "2026-10-25");

        // Assert
        var item = Assert.Single(items);
        Assert.Equal(PlanItemKind.Issue, item.Kind);
        Assert.Equal(IssueState.Closed, item.IssueState);
        Assert.Equal("New title", item.Title);
        Assert.Equal("axxitbe/portal", item.Repository);
        Assert.Equal("https://gh/axxitbe/portal/issues/5", item.IssueUrl);
        Assert.Equal("New title", (await _factory.ReadPlanItemsAsync()).Single(row => row.UserId == userId).Title);
    }

    [Fact]
    public async Task Get_ForAnIssueGitHubNoLongerShows_KeepsTheEntryAsUnavailable()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/gone", 5, "Snapshot", PlanSpan.Day, Wednesday).Value);
        _factory.GitHub.Issue = (_, _, _) => Result<GitHubIssue>.NotFound(GitHubErrors.Unavailable);

        // Act
        var response = await client.GetAsync($"{BaseUrl}?from=2026-10-19&to=2026-10-25", CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var item = Assert.Single(await response.Content.ReadFromJsonAsync<List<PlanItemDto>>(PortalJson.Options, CancellationToken) ?? []);
        Assert.Equal(IssueState.Unavailable, item.IssueState);
        Assert.Equal("Snapshot", item.Title);
    }

    [Fact]
    public async Task Get_WhenGitHubFails_StillReturnsTheItemWithAnUnknownState()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/flaky", 5, "Snapshot", PlanSpan.Day, Wednesday).Value);
        _factory.GitHub.Issue = (_, _, _) => FakeGitHubService.Unavailable<GitHubIssue>();

        // Act
        var items = await Read(client, "2026-10-19", "2026-10-25");

        // Assert
        var item = Assert.Single(items);
        Assert.Equal(IssueState.Unknown, item.IssueState);
        Assert.Equal("Snapshot", item.Title);
    }

    [Fact]
    public async Task Get_ForAnotherUsersItems_ReturnsNothing()
    {
        // Arrange
        var (_, ownerId) = await _factory.CreateUserAsync();
        var (client, _) = await _factory.CreateUserAsync();
        await Seed(ownerId, "Private", PlanSpan.Day, Wednesday);

        // Act
        var items = await Read(client, "2026-10-19", "2026-10-25");

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public async Task Post_WithATodo_CreatesItWithoutTouchingGitHub()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        var reads = 0;
        _factory.GitHub.Issue = (_, _, _) =>
        {
            reads++;

            return FakeGitHubService.Unavailable<GitHubIssue>();
        };

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new { kind = "Todo", title = "Call the bank", description = "Before noon", span = "Week", date = "2026-10-21" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<PlanItemDto>(PortalJson.Options, CancellationToken);
        Assert.Equal(PlanItemKind.Todo, item!.Kind);
        Assert.Equal(Monday, item.Date);
        Assert.Equal(0, reads);
        Assert.Single(await _factory.ReadPlanItemsAsync(), row => row.UserId == userId);
    }

    [Theory]
    [InlineData("""{"kind":"Todo","title":" ","span":"Day","date":"2026-10-21"}""")]
    [InlineData("""{"kind":"Todo","title":"x","span":"Day","date":"0001-01-01"}""")]
    [InlineData("""{"kind":"Todo","title":"x","span":"Month","date":"2026-10-21"}""")]
    [InlineData("""{"kind":"Issue","repository":"nope","issueNumber":1,"span":"Day","date":"2026-10-21"}""")]
    [InlineData("""{"kind":"Issue","repository":"o/r","issueNumber":0,"span":"Day","date":"2026-10-21"}""")]
    public async Task Post_WithInvalidInput_ReturnsBadRequest(string json)
    {
        // Arrange
        var (client, _) = await _factory.CreateUserAsync();
        using var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        // Act
        var response = await client.PostAsync(BaseUrl, content, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_WithAnIssue_StoresAReferenceAndWritesNothingToGitHub()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new { kind = "Issue", repository = "axxitbe/portal", issueNumber = 12, span = "Day", date = "2026-10-21" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var item = await response.Content.ReadFromJsonAsync<PlanItemDto>(PortalJson.Options, CancellationToken);
        Assert.Equal("Issue 12", item!.Title);
        Assert.Equal(IssueState.Open, item.IssueState);
        var row = Assert.Single(await _factory.ReadPlanItemsAsync(), row => row.UserId == userId);
        Assert.Equal(12, row.IssueNumber);
        Assert.Empty(_factory.GitHub.Closed);
        Assert.Empty(_factory.GitHub.Merged);
    }

    [Fact]
    public async Task Post_WithTheSameIssueInTheSamePeriod_ReturnsConflict()
    {
        // Arrange
        var (client, _) = await _factory.CreateUserAsync();
        var body = new { kind = "Issue", repository = "axxitbe/portal", issueNumber = 12, span = "Day", date = "2026-10-21" };
        await client.PostAsJsonAsync(BaseUrl, body, CancellationToken);

        // Act
        var response = await client.PostAsJsonAsync(BaseUrl, body, CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("PlanItem.AlreadyPlanned", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Fact]
    public async Task Post_WithTheSameIssueOnAnotherDayOrWeek_Succeeds()
    {
        // Arrange
        var (client, _) = await _factory.CreateUserAsync();
        await client.PostAsJsonAsync(
            BaseUrl,
            new { kind = "Issue", repository = "axxitbe/portal", issueNumber = 12, span = "Day", date = "2026-10-21" },
            CancellationToken);

        // Act
        var otherDay = await client.PostAsJsonAsync(
            BaseUrl,
            new { kind = "Issue", repository = "axxitbe/portal", issueNumber = 12, span = "Day", date = "2026-10-22" },
            CancellationToken);
        var week = await client.PostAsJsonAsync(
            BaseUrl,
            new { kind = "Issue", repository = "axxitbe/portal", issueNumber = 12, span = "Week", date = "2026-10-21" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Created, otherDay.StatusCode);
        Assert.Equal(HttpStatusCode.Created, week.StatusCode);
    }

    [Fact]
    public async Task Post_WithAPullRequestNumber_ReturnsBadRequest()
    {
        // Arrange
        var (client, _) = await _factory.CreateUserAsync();
        _factory.GitHub.Issue = (owner, repository, number) => Result<GitHubIssue>.Success(new GitHubIssue(
            number, "A pull", "open", "https://gh/pull/1", "https://api.gh/repos/o/r", new GitHubPullRequestMarker("x")));

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new { kind = "Issue", repository = "axxitbe/portal", issueNumber = 3, span = "Day", date = "2026-10-21" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("PlanItem.NotAnIssue", await response.Content.ReadAsStringAsync(CancellationToken));
    }

    [Fact]
    public async Task Post_WithAnIssueThatCannotBeSeen_ReturnsNotFound()
    {
        // Arrange
        var (client, _) = await _factory.CreateUserAsync();
        _factory.GitHub.Issue = (_, _, _) => Result<GitHubIssue>.NotFound(GitHubErrors.Unavailable);

        // Act
        var response = await client.PostAsJsonAsync(
            BaseUrl,
            new { kind = "Issue", repository = "axxitbe/private", issueNumber = 3, span = "Day", date = "2026-10-21" },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Put_OnATodo_EditsMovesAndCompletesIt()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        var item = await Seed(userId, "Draft", PlanSpan.Day, Wednesday);

        // Act
        var response = await client.PutAsJsonAsync(
            $"{BaseUrl}/{item.Id}",
            new { title = "Final", description = "Notes", span = "Week", date = "2026-10-22", isCompleted = true },
            CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        var row = Assert.Single(await _factory.ReadPlanItemsAsync(), row => row.Id == item.Id);
        Assert.Equal("Final", row.Title);
        Assert.Equal("Notes", row.Description);
        Assert.Equal(PlanSpan.Week, row.Span);
        Assert.Equal(Monday, row.Start);
        Assert.True(row.IsCompleted);
        Assert.NotNull(row.CompletedAt);
    }

    [Fact]
    public async Task Put_OnAnIssueEntry_CompletesItWithoutChangingTheIssue()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        var item = await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/portal", 5, "Fix it", PlanSpan.Day, Wednesday).Value);

        // Act
        var response = await client.PutAsJsonAsync(
            $"{BaseUrl}/{item.Id}",
            new { span = "Day", date = "2026-10-21", isCompleted = true },
            CancellationToken);

        // Assert
        response.EnsureSuccessStatusCode();
        Assert.True((await _factory.ReadPlanItemsAsync()).Single(row => row.Id == item.Id).IsCompleted);
        Assert.Empty(_factory.GitHub.Closed);
        Assert.Empty(_factory.GitHub.Merged);
    }

    [Fact]
    public async Task Put_WithATitleOnAnIssueEntry_ReturnsBadRequest()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        var item = await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/portal", 5, "Fix it", PlanSpan.Day, Wednesday).Value);

        // Act
        var response = await client.PutAsJsonAsync(
            $"{BaseUrl}/{item.Id}",
            new { title = "Other", span = "Day", date = "2026-10-21", isCompleted = false },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Put_MovingAnIssueOntoAPeriodItIsAlreadyIn_ReturnsConflict()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/portal", 5, "Fix it", PlanSpan.Day, Wednesday).Value);
        var other = await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/portal", 5, "Fix it", PlanSpan.Day, Wednesday.AddDays(1)).Value);

        // Act
        var response = await client.PutAsJsonAsync(
            $"{BaseUrl}/{other.Id}",
            new { span = "Day", date = "2026-10-21", isCompleted = false },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Put_OnAnotherUsersItem_ReturnsNotFoundAndChangesNothing()
    {
        // Arrange
        var (_, ownerId) = await _factory.CreateUserAsync();
        var (client, _) = await _factory.CreateUserAsync();
        var item = await Seed(ownerId, "Private", PlanSpan.Day, Wednesday);

        // Act
        var response = await client.PutAsJsonAsync(
            $"{BaseUrl}/{item.Id}",
            new { title = "Hijacked", span = "Day", date = "2026-10-21", isCompleted = true },
            CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Private", (await _factory.ReadPlanItemsAsync()).Single(row => row.Id == item.Id).Title);
    }

    [Fact]
    public async Task Delete_OnAnIssueEntry_RemovesOnlyTheRow()
    {
        // Arrange
        var (client, userId) = await _factory.CreateUserAsync();
        var item = await _factory.SeedPlanItemAsync(
            PlanItem.CreateIssue(userId, "axxitbe/portal", 5, "Fix it", PlanSpan.Day, Wednesday).Value);

        // Act
        var response = await client.DeleteAsync($"{BaseUrl}/{item.Id}", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.DoesNotContain(await _factory.ReadPlanItemsAsync(), row => row.Id == item.Id);
        Assert.Empty(_factory.GitHub.Closed);
    }

    [Fact]
    public async Task Delete_OnAnotherUsersItem_ReturnsNotFoundAndKeepsIt()
    {
        // Arrange
        var (_, ownerId) = await _factory.CreateUserAsync();
        var (client, _) = await _factory.CreateUserAsync();
        var item = await Seed(ownerId, "Private", PlanSpan.Day, Wednesday);

        // Act
        var response = await client.DeleteAsync($"{BaseUrl}/{item.Id}", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains(await _factory.ReadPlanItemsAsync(), row => row.Id == item.Id);
    }

    [Fact]
    public async Task Delete_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.DeleteAsync($"{BaseUrl}/1", CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<List<PlanItemDto>> Read(HttpClient client, string from, string to) =>
        await client.GetFromJsonAsync<List<PlanItemDto>>(
            $"{BaseUrl}?from={from}&to={to}",
            PortalJson.Options,
            CancellationToken) ?? [];

    private Task<PlanItem> Seed(int userId, string title, PlanSpan span, DateOnly date) =>
        _factory.SeedPlanItemAsync(PlanItem.CreateTodo(userId, title, null, span, date).Value);
}
