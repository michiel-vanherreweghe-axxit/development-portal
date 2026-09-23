namespace Domain;

public abstract class Entity
{
    public int Id { get; init; }

    public DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset UpdatedAt { get; init; }

    protected Entity()
    {
    }
}
