using Microsoft.EntityFrameworkCore;

namespace Quotes.Infrastructure.Persistence;

/// <summary>
/// EF Core mapping for the quotes catalog. The persistence model stays <see cref="QuoteRecord"/>;
/// the Domain <c>Quote</c> keeps no persistence concerns (no EF attributes, no DbContext leakage).
/// </summary>
public sealed class QuotesDbContext(DbContextOptions<QuotesDbContext> options) : DbContext(options)
{
    public DbSet<QuoteRecord> Quotes => Set<QuoteRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<QuoteRecord>();

        entity.ToTable("quotes");
        entity.HasKey(record => record.Id);
        entity.Property(record => record.Id).HasMaxLength(64);
        entity.Property(record => record.Text).IsRequired().HasMaxLength(280);
        entity.Property(record => record.Author).IsRequired().HasMaxLength(80);
        entity.Property(record => record.NormalizedFingerprint).IsRequired().HasMaxLength(280);

        // Near-duplicate detection is enforced by the database, not by a check-then-insert
        // race: a unique index turns a conflicting insert into a 23505 violation the
        // repository maps to DuplicateFingerprint.
        entity.HasIndex(record => record.NormalizedFingerprint).IsUnique();

        entity.HasData(QuotesSeed.Records);
    }
}
