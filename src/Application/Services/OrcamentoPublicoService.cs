using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using Application.Common;

namespace Application.Services;

public class OrcamentoPublicoService : IOrcamentoPublicoService
{
    private readonly IOrcamentoRepository _orcamentoRepository;

    public OrcamentoPublicoService(IOrcamentoRepository orcamentoRepository)
    {
        _orcamentoRepository = orcamentoRepository;
    }

    public async Task<ResultadoConsultaPublica> ObterPorTokenAsync(string token, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorTokenAsync(token, ct);
        if (orcamento is null)
            return ResultadoConsultaPublica.NaoEncontrado();
        if (orcamento.TokenExpiraEm is null || orcamento.TokenExpiraEm <= DateTime.UtcNow)
            return ResultadoConsultaPublica.Expirado();

        return ResultadoConsultaPublica.Ok(new
        {
            orcamento.Id,
            orcamento.Status,
            orcamento.Versao,
            orcamento.Total,
            orcamento.ValorAprovado,
            orcamento.MotivoRecusa,
            Itens = orcamento.Itens.Select(item => new
            {
                item.Id,
                item.Tipo,
                item.Descricao,
                item.Quantidade,
                item.ValorUnitario,
                item.ValorTotal,
                item.ItemPaiId,
                item.Decisao
            })
        });
    }

    public async Task<ResultadoDecisao> RegistrarDecisaoAsync(string token, DecisaoClienteRequest request, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorTokenAsync(token, ct);
        if (orcamento is null)
            return ResultadoDecisao.Falha("ORC_NAO_ENCONTRADO", "Orçamento não encontrado.");
        if (orcamento.TokenExpiraEm is null || orcamento.TokenExpiraEm <= DateTime.UtcNow)
            return ResultadoDecisao.Expirado();

        try
        {
            var decisoes = request.Itens?.ToDictionary(item => item.ItemId, item => item.Decisao);
            var statusAnterior = orcamento.Status;
            orcamento.AplicarDecisao(request.Tipo, decisoes, request.MotivoRecusa);
            await _orcamentoRepository.AdicionarHistoricoAsync(new OrcamentoHistorico(
                orcamento.Id,
                statusAnterior,
                orcamento.Status,
                null,
                "LinkPublico",
                DateTime.UtcNow,
                request.MotivoRecusa ?? $"Decisão do cliente: {request.Tipo}."), ct);
            await _orcamentoRepository.SalvarAlteracoesAsync(ct);
            return ResultadoDecisao.Ok(orcamento.Status.ToString());
        }
        catch (DomainException ex)
        {
            await _orcamentoRepository.RegistrarTentativaPublicaAsync(
                orcamento.Id,
                $"Decisão do cliente rejeitada ({ex.Code}): {ex.Message}",
                DateTime.UtcNow,
                ct);
            return ResultadoDecisao.Falha(ex.Code, ex.Message);
        }
        catch (OptimisticConcurrencyException)
        {
            await _orcamentoRepository.RegistrarTentativaPublicaAsync(
                orcamento.Id,
                "Decisão do cliente rejeitada (ORC_DECISAO_JA_REGISTRADA): outra requisição já atualizou o orçamento.",
                DateTime.UtcNow,
                ct);
            return ResultadoDecisao.JaRegistrada();
        }
    }
}
