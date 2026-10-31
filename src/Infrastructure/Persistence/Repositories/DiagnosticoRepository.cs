using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class DiagnosticoRepository : IDiagnosticoRepository
{
    private readonly AppDbContext _context;

    public DiagnosticoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<Diagnostico>> BuscarAsync(string? termo, CancellationToken ct = default)
    {
        var query = _context.Diagnosticos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(termo))
        {
            var termoLower = termo.ToLower().Trim();
            query = query.Where(d =>
                d.Codigo.ToLower().Contains(termoLower) ||
                d.Descricao.ToLower().Contains(termoLower) ||
                d.Sistema.ToLower().Contains(termoLower));
        }

        return await query.ToListAsync(ct);
    }
}
