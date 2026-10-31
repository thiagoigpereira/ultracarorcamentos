using Application.DTOs;
using Domain.Entities;
using Application.Common;

namespace Application.Interfaces;

public interface IOrcamentoService
{
    Task<Orcamento> CriarOrcamentoAsync(CriarOrcamentoRequest request, CancellationToken ct = default);
    Task<(Orcamento Orcamento, bool FoiCriado)> ObterOuCriarPorOSAsync(int oficinaId, int ordemServicoId, CriarOrcamentoRequest request, CancellationToken ct = default);
    Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default);
    Task<ResultadoOperacao> AtualizarItensAsync(Guid id, int oficinaId, byte[] rowVersion, int usuarioId, AtualizarItensRequest request, CancellationToken ct = default);
    Task<ResultadoEnvio> EnviarAsync(Guid id, int oficinaId, byte[] rowVersion, string idempotencyKey, int usuarioId, EnviarOrcamentoRequest request, CancellationToken ct = default);
    Task<Orcamento> CriarRevisaoAsync(Guid id, int usuarioId, CancellationToken ct = default);
    Task<IEnumerable<OrcamentoHistorico>> ObterHistoricoAsync(Guid id, CancellationToken ct = default);
}
