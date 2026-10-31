using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class OutboxRepository : IOutboxRepository
{
    private readonly AppDbContext _context;

    public OutboxRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task AdicionarAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        => await _context.OutboxMessages.AddAsync(message, cancellationToken);

    public async Task<IReadOnlyList<OutboxMessage>> ReivindicarPendentesAsync(
        int quantidade,
        Guid leaseId,
        TimeSpan duracaoLease,
        CancellationToken cancellationToken = default)
    {
        if (quantidade <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantidade));
        if (duracaoLease <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(duracaoLease));

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        var messages = await _context.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT *
                FROM "OutboxMessages"
                WHERE "ProcessedOnUtc" IS NULL
                  AND ("LeaseExpiresOnUtc" IS NULL OR "LeaseExpiresOnUtc" <= NOW())
                ORDER BY "OccurredOnUtc"
                LIMIT {quantidade}
                FOR UPDATE SKIP LOCKED
                """)
            .AsTracking()
            .ToListAsync(cancellationToken);

        var expiration = DateTime.UtcNow.Add(duracaoLease);
        foreach (var message in messages)
            message.Claim(leaseId, expiration);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return messages;
    }

    public Task SalvarAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
