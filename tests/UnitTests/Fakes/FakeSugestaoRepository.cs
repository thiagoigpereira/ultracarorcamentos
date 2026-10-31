using Application.Interfaces;
using Domain.Entities;

namespace UnitTests.Fakes;

public sealed class FakeSugestaoRepository : ISugestaoRepository
{
    public Veiculo? Veiculo { get; set; }
    public List<Diagnostico> Diagnosticos { get; } = new();
    public Dictionary<Guid, List<Peca>> PecasPorTipo { get; } = new();
    public Dictionary<Guid, List<PecaCompatibilidade>> CompatibilidadesPorPeca { get; } = new();

    public Task<Veiculo?> ObterVeiculoPorIdAsync(Guid veiculoId, CancellationToken cancellationToken = default)
        => Task.FromResult(Veiculo?.Id == veiculoId ? Veiculo : null);

    public Task<List<Diagnostico>> ObterDiagnosticosPorIdsAsync(List<Guid> diagnosticoIds, CancellationToken cancellationToken = default)
        => Task.FromResult(Diagnosticos.Where(d => diagnosticoIds.Contains(d.Id)).ToList());

    public Task<List<Peca>> ObterPecasCompativeisAsync(Guid tipoPecaId, Guid modeloId, int anoVeiculo, CancellationToken cancellationToken = default)
        => Task.FromResult(PecasPorTipo.TryGetValue(tipoPecaId, out var pecas)
            ? pecas.Where(peca => CompatibilidadesPorPeca.TryGetValue(peca.Id, out var compatibilidades)
                && compatibilidades.Any(compatibilidade => compatibilidade.EhCompativelCom(modeloId, anoVeiculo)))
                .ToList()
            : new List<Peca>());
}
