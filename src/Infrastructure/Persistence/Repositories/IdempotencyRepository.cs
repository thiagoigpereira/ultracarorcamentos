using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class IdempotencyRepository : IIdempotencyRepository
{
    private readonly AppDbContext _context;

    public IdempotencyRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<IdempotencyKey?> ObterAsync(int oficinaId, string chave, CancellationToken cancellationToken = default)
        => _context.IdempotencyKeys.FirstOrDefaultAsync(item => item.OficinaId == oficinaId && item.Chave == chave, cancellationToken);

    public async Task AdicionarAsync(IdempotencyKey item, CancellationToken cancellationToken = default)
        => await _context.IdempotencyKeys.AddAsync(item, cancellationToken);
}
