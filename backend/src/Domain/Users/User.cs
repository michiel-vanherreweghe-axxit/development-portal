using Ardalis.Result;

namespace Domain.Users;

public sealed class User : Entity
{
    public const int LoginMaxLength = 100;
    public const int AvatarUrlMaxLength = 500;
    public const int DefaultRunLimit = 10;
    public const int MinimumRunLimit = 1;
    public const int MaximumRunLimit = 100;

    private readonly List<Watches.Watch> _watches = [];

    private User()
    {
    }

    public long GitHubId { get; private set; }

    public string Login { get; private set; } = null!;

    public string? AvatarUrl { get; private set; }

    public int RunLimit { get; private set; } = DefaultRunLimit;

    public DateTimeOffset LastSignedInAt { get; private set; }

    public IReadOnlyCollection<Watches.Watch> Watches => _watches;

    public static Result<User> Create(long gitHubId, string login, string? avatarUrl, DateTimeOffset signedInAt)
    {
        var errors = Validate(gitHubId, login, avatarUrl);

        return errors.Count > 0
            ? Result<User>.Invalid(errors)
            : Result<User>.Success(new User
            {
                GitHubId = gitHubId,
                Login = login,
                AvatarUrl = avatarUrl,
                LastSignedInAt = signedInAt,
            });
    }

    public Result SignIn(string login, string? avatarUrl, DateTimeOffset signedInAt)
    {
        var errors = Validate(GitHubId, login, avatarUrl);
        if (errors.Count > 0)
        {
            return Result.Invalid(errors);
        }

        Login = login;
        AvatarUrl = avatarUrl;
        LastSignedInAt = signedInAt;

        return Result.Success();
    }

    public Result UpdateRunLimit(int runLimit)
    {
        if (runLimit is < MinimumRunLimit or > MaximumRunLimit)
        {
            return Result.Invalid(new ValidationError
            {
                Identifier = nameof(RunLimit),
                ErrorCode = "User.RunLimitOutOfRange",
                ErrorMessage = $"Minimum={MinimumRunLimit};Maximum={MaximumRunLimit}",
            });
        }

        RunLimit = runLimit;

        return Result.Success();
    }

    private static List<ValidationError> Validate(long gitHubId, string login, string? avatarUrl)
    {
        var errors = new List<ValidationError>();

        if (gitHubId <= 0)
        {
            errors.Add(new ValidationError { Identifier = nameof(GitHubId), ErrorCode = "User.GitHubIdInvalid" });
        }

        if (string.IsNullOrWhiteSpace(login) || login.Length > LoginMaxLength)
        {
            errors.Add(new ValidationError { Identifier = nameof(Login), ErrorCode = "User.LoginInvalid" });
        }

        if (avatarUrl?.Length > AvatarUrlMaxLength)
        {
            errors.Add(new ValidationError { Identifier = nameof(AvatarUrl), ErrorCode = "User.AvatarUrlTooLong" });
        }

        return errors;
    }
}
