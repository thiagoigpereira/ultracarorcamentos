using Domain.Entities;

namespace Application.Interfaces;

public interface IOrcamentoRepository
{
    Task<Orcamento?> ObterAtivoPorOSAsync(int oficinaId, int ordemServicoId, CancellationToken cancellationToken = default);
    Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Orcamento?> ObterPorIdAtualAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Orcamento?> ObterPorTokenAsync(string token, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OrcamentoHistorico>> ObterHistoricoAsync(Guid orcamentoId, CancellationToken cancellationToken = default);
    Task RegistrarTentativaPublicaAsync(Guid orcamentoId, string detalhes, DateTime data, CancellationToken cancellationToken = default);
    Task AdicionarAsync(Orcamento orcamento, CancellationToken cancellationToken = default);
    Task<(Orcamento Orcamento, bool FoiCriado)> ObterOuCriarPorOSAsync(Orcamento orcamento, CancellationToken cancellationToken = default);
    Task AdicionarHistoricoAsync(OrcamentoHistorico historico, CancellationToken cancellationToken = default);
    Task AdicionarItensAsync(IEnumerable<OrcamentoItem> itens, CancellationToken cancellationToken = default);
    Task<bool> TentarAtualizarRascunhoAsync(Orcamento orcamento, byte[] rowVersionAtual, byte[] novaRowVersion, CancellationToken cancellationToken = default);
    Task<bool> TentarMarcarEnviadoAsync(Guid id, int oficinaId, byte[] rowVersion, int usuarioId, DateTime enviadoEm, CancellationToken cancellationToken = default);
    Task SalvarAlteracoesAsync(CancellationToken cancellationToken = default);
}
