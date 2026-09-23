using Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public sealed class UserConfiguration : EntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);

        builder.HasIndex(user => user.GitHubId).IsUnique();

        builder.Property(user => user.Login)
            .HasMaxLength(User.LoginMaxLength)
            .IsRequired();

        builder.Property(user => user.AvatarUrl)
            .HasMaxLength(User.AvatarUrlMaxLength);

        builder.HasMany(user => user.Watches)
            .WithOne()
            .HasForeignKey(watch => watch.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(User.Watches))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
