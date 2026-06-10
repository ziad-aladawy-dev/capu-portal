using CapitalUniversity.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        // Lookup-by-hash is the hot path during /auth/reset-password.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        // Superseding prior tokens for a user filters by (UserId, ConsumedAt).
        builder.HasIndex(x => new { x.UserId, x.ConsumedAt });

        builder.Ignore(x => x.IsActive);
    }
}
