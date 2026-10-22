using Domain.Planning;
using Domain.Users;
using Domain.Watches;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Database.Configurations;

public sealed class PlanItemConfiguration : EntityConfiguration<PlanItem>
{
    public override void Configure(EntityTypeBuilder<PlanItem> builder)
    {
        base.Configure(builder);

        builder.Property(item => item.Title)
            .HasMaxLength(PlanItem.TitleMaxLength)
            .IsRequired();

        builder.Property(item => item.Description)
            .HasMaxLength(PlanItem.DescriptionMaxLength);

        builder.Property(item => item.Owner)
            .HasMaxLength(Watch.OwnerMaxLength);

        builder.Property(item => item.Repository)
            .HasMaxLength(Watch.RepositoryMaxLength);

        builder.Ignore(item => item.IsIssue);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(item => item.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(item => new { item.UserId, item.Owner, item.Repository, item.IssueNumber, item.Span, item.Start })
            .IsUnique();
    }
}
