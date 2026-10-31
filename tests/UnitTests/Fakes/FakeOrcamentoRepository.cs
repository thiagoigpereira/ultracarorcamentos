using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;

namespace UnitTests.Fakes;

public sealed class FakeOrcamentoRepository : IOrcamentoRepository
{
    public List<Orcamento> Orcamentos { get; } = new();
    public List<OrcamentoHistorico> Historicos { get; } = new();
    public bool EnvioPermitido { get; set; } = true;
    public int? UsuarioQueVenceuAoFalhar { get; set; }

    public Task<Orcamento?> ObterAtivoPorOSAsync(int oficinaId, int ordemServicoId, CancellationToken cancellationToken = default)
        => Task.FromResult(Orcamentos.FirstOrDefault(x => x.OficinaId == oficinaId && x.OrdemServicoId == ordemServicoId && x.Status != StatusOrcamento.Cancelado));

    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Orcamentos.FirstOrDefault(x => x.Id == id));

    public Task<Orcamento?> ObterPorIdAtualAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Orcamentos.FirstOrDefault(x => x.Id == id));

    public Task<Orcamento?> ObterPorTokenAsync(string token, CancellationToken cancellationToken = default)
        => Task.FromResult(Orcamentos.FirstOrDefault(x => x.TokenPublico == token));

    public Task<IReadOnlyList<OrcamentoHistorico>> ObterHistoricoAsync(Guid orcamentoId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<OrcamentoHistorico>>(Historicos.Where(x => x.OrcamentoId == orcamentoId).ToList());

    public Task AdicionarAsync(Orcamento orcamento, CancellationToken cancellationToken = default)
    {
        Orcamentos.Add(orcamento);
        return Task.CompletedTask;
    }

    public async Task<(Orcamento Orcamento, bool FoiCriado)> ObterOuCriarPorOSAsync(Orcamento orcamento, CancellationToken cancellationToken = default)
    {
        var existing = await ObterAtivoPorOSAsync(orcamento.OficinaId, orcamento.OrdemServicoId, cancellationToken);
        if (existing is not null)
            return (existing, false);
        await AdicionarAsync(orcamento, cancellationToken);
        return (orcamento, true);
    }

    public Task AdicionarHistoricoAsync(OrcamentoHistorico historico, CancellationToken cancellationToken = default)
    {
        Historicos.Add(historico);
        return Task.CompletedTask;
    }

    public Task AdicionarItensAsync(IEnumerable<OrcamentoItem> itens, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<bool> TentarAtualizarRascunhoAsync(Orcamento orcamento, byte[] rowVersionAtual, byte[] novaRowVersion, CancellationToken cancellationToken = default)
        => Task.FromResult(orcamento.Status == StatusOrcamento.Rascunho && orcamento.RowVersion.SequenceEqual(rowVersionAtual));

    public Task RegistrarTentativaPublicaAsync(Guid orcamentoId, string detalhes, DateTime data, CancellationToken cancellationToken = default)
    {
        var budget = Orcamentos.FirstOrDefault(item => item.Id == orcamentoId);
        if (budget is not null)
            Historicos.Add(new OrcamentoHistorico(orcamentoId, budget.Status, budget.Status, null, "LinkPublico", data, detalhes));
        return Task.CompletedTask;
    }

    public Task<bool> TentarMarcarEnviadoAsync(Guid id, int oficinaId, byte[] rowVersion, int usuarioId, DateTime enviadoEm, CancellationToken cancellationToken = default)
    {
        if (!EnvioPermitido && UsuarioQueVenceuAoFalhar.HasValue)
        {
            var budget = Orcamentos.FirstOrDefault(item => item.Id == id);
            budget?.Enviar(UsuarioQueVenceuAoFalhar.Value, enviadoEm, "Email");
        }
        return Task.FromResult(EnvioPermitido);
    }

    public Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
