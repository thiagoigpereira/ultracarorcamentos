using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Application.Common;
using Npgsql;

namespace Infrastructure.Persistence.Repositories;

public class OrcamentoRepository : IOrcamentoRepository
{
    private readonly AppDbContext _context;

    public OrcamentoRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Orcamento?> ObterAtivoPorOSAsync(int oficinaId, int ordemServicoId, CancellationToken cancellationToken = default)
        => _context.Orcamentos.Include(o => o.Itens).FirstOrDefaultAsync(o => o.OficinaId == oficinaId && o.OrdemServicoId == ordemServicoId && o.Status != StatusOrcamento.Cancelado, cancellationToken);

    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Orcamentos.Include(o => o.Itens).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Orcamento?> ObterPorIdAtualAsync(Guid id, CancellationToken cancellationToken = default)
        => _context.Orcamentos.AsNoTracking().Include(o => o.Itens).FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    public Task<Orcamento?> ObterPorTokenAsync(string token, CancellationToken cancellationToken = default)
        => _context.Orcamentos.Include(o => o.Itens).FirstOrDefaultAsync(o => o.TokenPublico == token, cancellationToken);

    public async Task<IReadOnlyList<OrcamentoHistorico>> ObterHistoricoAsync(Guid orcamentoId, CancellationToken cancellationToken = default)
        => await _context.OrcamentoHistoricos.AsNoTracking().Where(item => item.OrcamentoId == orcamentoId).OrderBy(item => item.Data).ToListAsync(cancellationToken);

    public async Task RegistrarTentativaPublicaAsync(Guid orcamentoId, string detalhes, DateTime data, CancellationToken cancellationToken = default)
    {
        _context.ChangeTracker.Clear();
        var orcamento = await _context.Orcamentos.AsNoTracking()
            .Where(item => item.Id == orcamentoId)
            .Select(item => new { item.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (orcamento is null)
            return;

        await _context.OrcamentoHistoricos.AddAsync(new OrcamentoHistorico(
            orcamentoId,
            orcamento.Status,
            orcamento.Status,
            null,
            "LinkPublico",
            data,
            detalhes), cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task AdicionarAsync(Orcamento orcamento, CancellationToken cancellationToken = default)
        => await _context.Orcamentos.AddAsync(orcamento, cancellationToken);

    public async Task<(Orcamento Orcamento, bool FoiCriado)> ObterOuCriarPorOSAsync(Orcamento orcamento, CancellationToken cancellationToken = default)
    {
        var existing = await ObterAtivoPorOSAsync(orcamento.OficinaId, orcamento.OrdemServicoId, cancellationToken);
        if (existing is not null)
            return (existing, false);

        await _context.Orcamentos.AddAsync(orcamento, cancellationToken);
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return (orcamento, true);
        }
        catch (DbUpdateException exception) when (IsActiveOrderUniqueViolation(exception))
        {
            _context.Entry(orcamento).State = EntityState.Detached;
            var winner = await ObterAtivoPorOSAsync(orcamento.OficinaId, orcamento.OrdemServicoId, cancellationToken);
            if (winner is null)
                throw;

            return (winner, false);
        }
    }

    public async Task AdicionarHistoricoAsync(OrcamentoHistorico historico, CancellationToken cancellationToken = default)
        => await _context.OrcamentoHistoricos.AddAsync(historico, cancellationToken);

    public Task AdicionarItensAsync(IEnumerable<OrcamentoItem> itens, CancellationToken cancellationToken = default)
    {
        _context.OrcamentoItens.AddRange(itens);
        return Task.CompletedTask;
    }

    public async Task<bool> TentarAtualizarRascunhoAsync(Orcamento orcamento, byte[] rowVersionAtual, byte[] novaRowVersion, CancellationToken cancellationToken = default)
    {
        var afetadas = await _context.Orcamentos
            .Where(item => item.Id == orcamento.Id
                && item.OficinaId == orcamento.OficinaId
                && item.Status == StatusOrcamento.Rascunho
                && item.RowVersion == rowVersionAtual)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.RowVersion, novaRowVersion), cancellationToken);

        if (afetadas == 1)
            _context.Entry(orcamento).Property(item => item.RowVersion).OriginalValue = novaRowVersion;

        return afetadas == 1;
    }

    public async Task<bool> TentarMarcarEnviadoAsync(Guid id, int oficinaId, byte[] rowVersion, int usuarioId, DateTime enviadoEm, CancellationToken cancellationToken = default)
    {
        var afetadas = await _context.Orcamentos
            .Where(o => o.Id == id && o.OficinaId == oficinaId && o.Status == StatusOrcamento.Rascunho && o.RowVersion == rowVersion)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(o => o.Status, StatusOrcamento.Enviado)
                .SetProperty(o => o.EnviadoPor, usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture))
                .SetProperty(o => o.EnviadoEm, enviadoEm), cancellationToken);
        return afetadas == 1;
    }

    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
        => SalvarComControleConcorrenciaAsync(cancellationToken);

    private async Task SalvarComControleConcorrenciaAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            _context.ChangeTracker.Clear();
            throw new OptimisticConcurrencyException("O orçamento foi atualizado por outra requisição.", exception);
        }
    }

    private static bool IsActiveOrderUniqueViolation(DbUpdateException exception)
        => exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_Orcamentos_OficinaId_OrdemServicoId"
        };
}
