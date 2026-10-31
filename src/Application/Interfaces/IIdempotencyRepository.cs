using Domain.Entities;

namespace Application.Interfaces;

public interface IIdempotencyRepository
{
    Task<IdempotencyKey?> ObterAsync(int oficinaId, string chave, CancellationToken cancellationToken = default);
    Task AdicionarAsync(IdempotencyKey item, CancellationToken cancellationToken = default);
}
