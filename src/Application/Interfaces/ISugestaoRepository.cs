using Domain.Entities;

namespace Application.Interfaces;

public interface ISugestaoRepository
{
    Task<Veiculo?> ObterVeiculoPorIdAsync(Guid veiculoId, CancellationToken cancellationToken = default);
    Task<List<Diagnostico>> ObterDiagnosticosPorIdsAsync(List<Guid> diagnosticoIds, CancellationToken cancellationToken = default);
    Task<List<Peca>> ObterPecasCompativeisAsync(Guid tipoPecaId, Guid modeloId, int anoVeiculo, CancellationToken cancellationToken = default);
}
