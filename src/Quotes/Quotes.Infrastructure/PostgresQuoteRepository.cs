using Microsoft.EntityFrameworkCore;
using Npgsql;
using Quotes.Domain;
using Quotes.Domain.Abstractions;
using Quotes.Infrastructure.Mapping;
using Quotes.Infrastructure.Persistence;

namespace Quotes.Infrastructure;

/// <summary>
/// PostgreSQL adapter for <see cref="IQuoteRepository"/>. Duplicate detection leans on the
/// unique fingerprint index instead of a check-then-insert race: the insert either wins or
/// fails with a 23505 unique violation, which maps to <see cref="QuoteAddOutcome.DuplicateFingerprint"/>.
/// Scoped — one unit of work per request over the scoped <see cref="QuotesDbContext"/>.
/// </summary>
public sealed class PostgresQuoteRepository(QuotesDbContext context) : IQuoteRepository
{
    private const string _uniqueViolation = "23505";

    public async Task<Quote?> GetRandomAsync(CancellationToken cancellationToken)
    {
        // Random pick happens inside PostgreSQL. The catalog is PoC-sized, so a full sort
        // per pick is the simple, correct tool.
        var record = await context.Quotes
            .FromSql($"select * from quotes order by random() limit 1")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);

        return record?.ToDomain();
    }

    public async Task<Quote?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var record = await context.Quotes
            .AsNoTracking()
            .SingleOrDefaultAsync(quote => quote.Id == id, cancellationToken);

        return record?.ToDomain();
    }

    public async Task<QuotePage> ListAsync(int skip, int take, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(take);

        // Stable catalog order: seeds first (they share a fixed timestamp), then created
        // quotes in creation order, with the id as a deterministic tiebreaker.
        var query = context.Quotes
            .AsNoTracking()
            .OrderBy(quote => quote.CreatedAtUtc)
            .ThenBy(quote => quote.Id);

        var total = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return new QuotePage([.. items.Select(record => record.ToDomain())], total);
    }

    public async Task<QuoteAddOutcome> AddAsync(Quote quote, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(quote);

        context.Quotes.Add(quote.ToRecord(DateTimeOffset.UtcNow));
        try
        {
            await context.SaveChangesAsync(cancellationToken);
            return QuoteAddOutcome.Added;
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
        {
            SqlState: _uniqueViolation
        })
        {
            // Either the fingerprint index or the primary key collided; both mean the quote
            // already exists, and the id is generated so only a broken caller hits the pk case.
            context.ChangeTracker.Clear();
            return QuoteAddOutcome.DuplicateFingerprint;
        }
    }
}
