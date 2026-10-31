using Application.DTOs;
using Application.Interfaces;
using Domain.Exceptions;
using Domain.Entities;

namespace Application.Services;

public class SugestaoService : ISugestaoService
{
    private readonly ISugestaoRepository _repository;

    public SugestaoService(ISugestaoRepository repository)
    {
        _repository = repository;
    }

    public async Task<SugestaoResponse> GerarSugestaoAsync(CriarSugestaoRequest request, CancellationToken cancellationToken = default)
    {
        if (request.DiagnosticoIds == null || !request.DiagnosticoIds.Any())
        {
            throw new DomainException("DIAGNOSTICO_OBRIGATORIO", "Informe ao menos um diagnóstico.");
        }

        var veiculo = await _repository.ObterVeiculoPorIdAsync(request.VeiculoId, cancellationToken)
            ?? throw new DomainException("VEICULO_NAO_ENCONTRADO", "Veículo não encontrado.");

        var diagnosticos = await _repository.ObterDiagnosticosPorIdsAsync(request.DiagnosticoIds, cancellationToken);
        if (!diagnosticos.Any())
        {
            throw new DomainException("DIAGNOSTICO_NAO_ENCONTRADO", "Nenhum dos diagnósticos informados foi encontrado.");
        }

        var servicosDto = new List<ServicoSugestaoDto>();
        var servicosProcessados = new HashSet<Guid>();

        foreach (var diagnostico in diagnosticos)
        {
            foreach (var ds in diagnostico.DiagnosticoServicos)
            {
                var servico = ds.Servico;

                if (!servicosProcessados.Add(servico.Id))
                    continue;

                var pecasDto = new List<PecaTipoSugestaoDto>();

                foreach (var stp in servico.ServicoTipoPecas)
                {
                    var pecasCompativeis = await _repository.ObterPecasCompativeisAsync(
                        stp.TipoPecaId,
                        veiculo.ModeloId,
                        veiculo.Ano,
                        cancellationToken
                    );

                    var opcoesDto = pecasCompativeis
                        .OrderByDescending(p => p.Preferencial)
                        .ThenBy(p => p.Preco)
                        .Select(p => new PecaOpcaoDto(
                            PecaId: p.Id,
                            Codigo: p.CodigoFabricante,
                            Descricao: p.Descricao,
                            Preco: p.Preco,
                            Preferencial: p.Preferencial
                        ))
                        .ToList();

                    bool pecaPendente = !opcoesDto.Any();

                    pecasDto.Add(new PecaTipoSugestaoDto(
                        TipoPeca: stp.TipoPecaNome,
                        Quantidade: stp.QuantidadePadrao,
                        PecaPendente: pecaPendente,
                        Opcoes: opcoesDto
                    ));
                }

                servicosDto.Add(new ServicoSugestaoDto(
                    ServicoId: servico.Id,
                    Descricao: servico.Descricao,
                    OrigemDiagnosticoId: diagnostico.Id,
                    TempoHoras: servico.TempoPadraoHoras,
                    ValorMaoDeObra: servico.ValorMaoDeObra,
                    Pecas: pecasDto
                ));
            }
        }

        return new SugestaoResponse(
            Veiculo: new VeiculoSugestaoDto(
                Id: veiculo.Id,
                Marca: veiculo.Marca.Nome,
                Modelo: veiculo.Modelo.Nome,
                Ano: veiculo.Ano
            ),
            Servicos: servicosDto
        );
    }
}
