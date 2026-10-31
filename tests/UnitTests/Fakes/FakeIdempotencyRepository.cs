using Application.Interfaces;
using Domain.Entities;

namespace UnitTests.Fakes;

public sealed class FakeIdempotencyRepository : IIdempotencyRepository
{
    public List<IdempotencyKey> Items { get; } = new();

    public Task<IdempotencyKey?> ObterAsync(int oficinaId, string chave, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.OficinaId == oficinaId && x.Chave == chave));

    public Task AdicionarAsync(IdempotencyKey item, CancellationToken cancellationToken = default)
    {
        Items.Add(item);
        return Task.CompletedTask;
    }
}
