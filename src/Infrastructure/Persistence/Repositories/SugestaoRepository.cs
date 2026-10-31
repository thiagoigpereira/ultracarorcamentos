using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SugestaoRepository : ISugestaoRepository
{
    private readonly AppDbContext _context;

    public SugestaoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Veiculo?> ObterVeiculoPorIdAsync(Guid veiculoId, CancellationToken cancellationToken = default)
    {
        return await _context.Veiculos
            .Include(v => v.Marca)
            .Include(v => v.Modelo)
            .FirstOrDefaultAsync(v => v.Id == veiculoId, cancellationToken);
    }

    public async Task<List<Diagnostico>> ObterDiagnosticosPorIdsAsync(List<Guid> diagnosticoIds, CancellationToken cancellationToken = default)
    {
        return await _context.Diagnosticos
            .Include(d => d.DiagnosticoServicos)
                .ThenInclude(ds => ds.Servico)
                    .ThenInclude(s => s.ServicoTipoPecas)
            .Where(d => diagnosticoIds.Contains(d.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<Peca>> ObterPecasCompativeisAsync(Guid tipoPecaId, Guid modeloId, int anoVeiculo, CancellationToken cancellationToken = default)
    {
        return await _context.Pecas
            .Where(p => p.TipoPecaId == tipoPecaId)
            .Where(p => p.Compatibilidades.Any(c =>
                c.ModeloId == modeloId &&
                anoVeiculo >= c.AnoInicio &&
                anoVeiculo <= c.AnoFim))
            .ToListAsync(cancellationToken);
    }
}
