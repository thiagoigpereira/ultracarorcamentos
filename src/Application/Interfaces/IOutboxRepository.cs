using Domain.Entities;

namespace Application.Interfaces;

public interface IOutboxRepository
{
    Task AdicionarAsync(OutboxMessage message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OutboxMessage>> ReivindicarPendentesAsync(int quantidade, Guid leaseId, TimeSpan duracaoLease, CancellationToken cancellationToken = default);
    Task SalvarAsync(CancellationToken cancellationToken = default);
}
