using CapitalUniversity.Core.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CapitalUniversity.Core.Infrastructure.Persistence.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserType)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.TokenHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.ReplacedByTokenHash)
            .HasMaxLength(128);

        builder.Property(x => x.ReasonRevoked)
            .HasMaxLength(64);

        // Lookup-by-hash is the hot path during /auth/refresh. Unique guards against
        // duplicate insertions on the (vanishingly small) chance of a hash collision
        // or accidental re-issue.
        builder.HasIndex(x => x.TokenHash).IsUnique();

        // Revocation sweeps (logout / password-change / replay chain) filter by user.
        builder.HasIndex(x => new { x.UserId, x.RevokedAt });

        builder.Ignore(x => x.IsActive);
    }
}
