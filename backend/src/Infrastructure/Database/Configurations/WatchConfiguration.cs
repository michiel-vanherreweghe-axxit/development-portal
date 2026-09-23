using Domain.Watches;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public sealed class WatchConfiguration : EntityConfiguration<Watch>
{
    public override void Configure(EntityTypeBuilder<Watch> builder)
    {
        base.Configure(builder);

        builder.Property(watch => watch.Owner)
            .HasMaxLength(Watch.OwnerMaxLength)
            .IsRequired();

        builder.Property(watch => watch.Repository)
            .HasMaxLength(Watch.RepositoryMaxLength)
            .IsRequired();

        builder.Property(watch => watch.WorkflowFile)
            .HasMaxLength(Watch.WorkflowFileMaxLength)
            .IsRequired();

        builder.Property(watch => watch.DisplayName)
            .HasMaxLength(Watch.DisplayNameMaxLength);

        builder.Ignore(watch => watch.FullName);

        builder.HasIndex(watch => new { watch.UserId, watch.Owner, watch.Repository, watch.WorkflowFile })
            .IsUnique();
    }
}
