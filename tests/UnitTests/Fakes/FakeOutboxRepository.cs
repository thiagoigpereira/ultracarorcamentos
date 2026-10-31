using Application.Interfaces;
using Domain.Entities;

namespace UnitTests.Fakes;

public sealed class FakeOutboxRepository : IOutboxRepository
{
    public List<OutboxMessage> Messages { get; } = new();

    public Task AdicionarAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OutboxMessage>> ReivindicarPendentesAsync(int quantidade, Guid leaseId, TimeSpan duracaoLease, CancellationToken cancellationToken = default)
    {
        var messages = Messages
            .Where(message => message.ProcessedOnUtc is null && (message.LeaseExpiresOnUtc is null || message.LeaseExpiresOnUtc <= DateTime.UtcNow))
            .OrderBy(message => message.OccurredOnUtc)
            .Take(quantidade)
            .ToList();
        foreach (var message in messages)
            message.Claim(leaseId, DateTime.UtcNow.Add(duracaoLease));
        return Task.FromResult<IReadOnlyList<OutboxMessage>>(messages);
    }

    public Task SalvarAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
