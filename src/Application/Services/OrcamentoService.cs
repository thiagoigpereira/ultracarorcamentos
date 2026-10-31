using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Common;
using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;

namespace Application.Services;

public class OrcamentoService : IOrcamentoService
{
    private readonly IOrcamentoRepository _orcamentoRepository;
    private readonly IOutboxRepository _outboxRepository;
    private readonly IIdempotencyRepository _idempotencyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public OrcamentoService(
        IOrcamentoRepository orcamentoRepository,
        IOutboxRepository outboxRepository,
        IIdempotencyRepository idempotencyRepository,
        IUnitOfWork unitOfWork)
    {
        _orcamentoRepository = orcamentoRepository;
        _outboxRepository = outboxRepository;
        _idempotencyRepository = idempotencyRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<Orcamento> CriarOrcamentoAsync(CriarOrcamentoRequest request, CancellationToken ct = default)
    {
        var orcamento = new Orcamento(request.OficinaId, request.OrdemServicoId, request.VeiculoId, request.ClienteId);
        var (result, _) = await _orcamentoRepository.ObterOuCriarPorOSAsync(orcamento, ct);
        return result;
    }

    public async Task<(Orcamento Orcamento, bool FoiCriado)> ObterOuCriarPorOSAsync(int oficinaId, int ordemServicoId, CriarOrcamentoRequest request, CancellationToken ct = default)
    {
        var orcamento = new Orcamento(oficinaId, ordemServicoId, request.VeiculoId, request.ClienteId);
        return await _orcamentoRepository.ObterOuCriarPorOSAsync(orcamento, ct);
    }

    public Task<Orcamento?> ObterPorIdAsync(Guid id, CancellationToken ct = default)
        => _orcamentoRepository.ObterPorIdAsync(id, ct);

    public Task<ResultadoOperacao> AtualizarItensAsync(Guid id, int oficinaId, byte[] rowVersion, int usuarioId, AtualizarItensRequest request, CancellationToken ct = default)
        => _unitOfWork.ExecuteAsync(
            cancellationToken => AtualizarItensEmTransacaoAsync(id, oficinaId, rowVersion, usuarioId, request, cancellationToken),
            ct);

    private async Task<ResultadoOperacao> AtualizarItensEmTransacaoAsync(Guid id, int oficinaId, byte[] rowVersion, int usuarioId, AtualizarItensRequest request, CancellationToken ct)
    {
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(id, ct);
        if (orcamento is null || orcamento.OficinaId != oficinaId)
            return ResultadoOperacao.Falha("ORC_NAO_ENCONTRADO", "Orçamento não encontrado.");
        if (orcamento.Status != StatusOrcamento.Rascunho)
            return await RegistrarConflitoEdicaoAsync(orcamento, usuarioId, "ORC_EDICAO_BLOQUEADA", ct);
        if (!RowVersionConfere(orcamento, rowVersion))
        {
            return await RegistrarConflitoEdicaoAsync(orcamento, usuarioId, "ORC_VERSAO_DESATUALIZADA", ct);
        }

        try
        {
            var itens = request.Itens.Select(item => new OrcamentoItem(orcamento.Id, item.Tipo, item.ReferenciaId, item.ItemPaiId, item.Descricao, item.Quantidade, item.ValorUnitario, item.PecaPendente)).ToList();
            Orcamento.ValidarDesconto(request.Desconto, request.DescontoPercentual);
            var novaRowVersion = Guid.NewGuid().ToByteArray();
            if (!await _orcamentoRepository.TentarAtualizarRascunhoAsync(orcamento, rowVersion, novaRowVersion, ct))
            {
                var atual = await _orcamentoRepository.ObterPorIdAtualAsync(id, ct);
                if (atual is null || atual.OficinaId != oficinaId)
                    return ResultadoOperacao.Falha("ORC_NAO_ENCONTRADO", "Orçamento não encontrado.");

                var codigoErro = atual.Status == StatusOrcamento.Rascunho
                    ? "ORC_VERSAO_DESATUALIZADA"
                    : "ORC_EDICAO_BLOQUEADA";
                return await RegistrarConflitoEdicaoAsync(atual, usuarioId, codigoErro, ct);
            }

            var statusAnterior = orcamento.Status;
            orcamento.SubstituirItens(itens, request.Desconto, request.DescontoPercentual, novaRowVersion);
            await _orcamentoRepository.AdicionarItensAsync(itens, ct);
            await _orcamentoRepository.AdicionarHistoricoAsync(new OrcamentoHistorico(
                id,
                statusAnterior,
                orcamento.Status,
                usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                "Edicao",
                DateTime.UtcNow,
                "Itens e desconto atualizados."), ct);
            await _orcamentoRepository.SalvarAlteracoesAsync(ct);
            return ResultadoOperacao.OK(Mapear(orcamento));
        }
        catch (DomainException ex)
        {
            return ResultadoOperacao.Falha(ex.Code, ex.Message);
        }
    }

    private async Task<ResultadoOperacao> RegistrarConflitoEdicaoAsync(Orcamento orcamento, int usuarioId, string codigoErro, CancellationToken ct)
    {
        var detalhe = codigoErro == "ORC_VERSAO_DESATUALIZADA"
            ? "Tentativa de edição rejeitada: ETag desatualizado."
            : "Tentativa de edição rejeitada: orçamento fora de rascunho.";
        await _orcamentoRepository.AdicionarHistoricoAsync(new OrcamentoHistorico(
            orcamento.Id,
            orcamento.Status,
            orcamento.Status,
            usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "Edicao",
            DateTime.UtcNow,
            detalhe), ct);
        await _orcamentoRepository.SalvarAlteracoesAsync(ct);
        var mensagem = codigoErro == "ORC_VERSAO_DESATUALIZADA"
            ? "O orçamento foi modificado por outro usuário."
            : "Apenas orçamentos em rascunho podem ser editados.";
        return ResultadoOperacao.Falha(codigoErro, mensagem);
    }

    public async Task<ResultadoEnvio> EnviarAsync(Guid id, int oficinaId, byte[] rowVersion, string idempotencyKey, int usuarioId, EnviarOrcamentoRequest request, CancellationToken ct = default)
        => await _unitOfWork.ExecuteAsync(
            cancellationToken => EnviarEmTransacaoAsync(id, oficinaId, rowVersion, idempotencyKey, usuarioId, request, cancellationToken),
            ct);

    private async Task<ResultadoEnvio> EnviarEmTransacaoAsync(Guid id, int oficinaId, byte[] rowVersion, string idempotencyKey, int usuarioId, EnviarOrcamentoRequest request, CancellationToken ct)
    {
        var hashRequest = Hash(request);
        var rota = $"/orcamentos/{id}/envio";
        var existente = await _idempotencyRepository.ObterAsync(oficinaId, idempotencyKey, ct);
        if (existente is not null)
            return existente.HashRequest == hashRequest && existente.Rota == rota
                ? ResultadoEnvio.Reprocessado(existente.RespostaJson)
                : ResultadoEnvio.Falha("IDEMPOTENCY_KEY_REUTILIZADA", "A chave de idempotência já foi usada com outra requisição.");

        var orcamento = await _orcamentoRepository.ObterPorIdAsync(id, ct);
        if (orcamento is null)
            return ResultadoEnvio.Falha("ORC_NAO_ENCONTRADO", "Orçamento não encontrado.");
        if (!RowVersionConfere(orcamento, rowVersion))
        {
            var codigo = orcamento.Status == StatusOrcamento.Rascunho
                ? "ORC_VERSAO_DESATUALIZADA"
                : "ORC_JA_ENVIADO";
            return await RegistrarConflitoEnvioAsync(orcamento, usuarioId, request.Canal, codigo, ct);
        }

        var agora = DateTime.UtcNow;
        try
        {
            orcamento.ValidarEnvio();
        }
        catch (DomainException ex)
        {
            await _orcamentoRepository.AdicionarHistoricoAsync(new OrcamentoHistorico(
                id,
                orcamento.Status,
                orcamento.Status,
                usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                request.Canal,
                agora,
                $"Tentativa de envio rejeitada ({ex.Code}): {ex.Message}"), ct);
            await _orcamentoRepository.SalvarAlteracoesAsync(ct);
            return ResultadoEnvio.Falha(ex.Code, ex.Message);
        }

        if (!await _orcamentoRepository.TentarMarcarEnviadoAsync(id, oficinaId, rowVersion, usuarioId, agora, ct))
        {
            var atual = await _orcamentoRepository.ObterPorIdAtualAsync(id, ct);
            if (atual is null)
                return ResultadoEnvio.Falha("ORC_NAO_ENCONTRADO", "Orçamento não encontrado.");

            var codigo = atual.Status == StatusOrcamento.Rascunho
                ? "ORC_VERSAO_DESATUALIZADA"
                : "ORC_JA_ENVIADO";
            return await RegistrarConflitoEnvioAsync(atual, usuarioId, request.Canal, codigo, ct);
        }

        orcamento.Enviar(usuarioId, agora, request.Canal);
        await _orcamentoRepository.AdicionarHistoricoAsync(new OrcamentoHistorico(id, StatusOrcamento.Rascunho, StatusOrcamento.Enviado, usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture), request.Canal, agora, request.Mensagem), ct);
        var deduplicationKey = $"{id:N}:{orcamento.Versao}";
        await _outboxRepository.AdicionarAsync(new OutboxMessage(
            "OrcamentoEnviado",
            JsonSerializer.Serialize(new { OrcamentoId = id, orcamento.Versao, orcamento.TokenPublico }),
            agora,
            deduplicationKey), ct);
        var linkCliente = $"/public/v1/orcamentos/{orcamento.TokenPublico}";
        var enviadoPor = new UsuarioInfoResponse(usuarioId, $"Usuário {usuarioId}");
        var respostaJson = JsonSerializer.Serialize(new
        {
            status = "Enviado",
            enviadoPor = new { id = enviadoPor.Id, nome = enviadoPor.Nome },
            enviadoEm = agora,
            linkCliente
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await _idempotencyRepository.AdicionarAsync(new IdempotencyKey(idempotencyKey, oficinaId, rota, hashRequest, respostaJson, 200, agora), ct);
        await _orcamentoRepository.SalvarAlteracoesAsync(ct);
        return ResultadoEnvio.OK(linkCliente, enviadoPor, agora, respostaJson);
    }

    public async Task<Orcamento> CriarRevisaoAsync(Guid id, int usuarioId, CancellationToken ct = default)
    {
        var orcamento = await _orcamentoRepository.ObterPorIdAsync(id, ct) ?? throw new DomainException("ORC_NAO_ENCONTRADO", "Orçamento não encontrado.");
        var anterior = orcamento.Status;
        orcamento.Revisar();
        await _orcamentoRepository.AdicionarHistoricoAsync(new OrcamentoHistorico(id, anterior, StatusOrcamento.Rascunho, usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture), "Revisao", DateTime.UtcNow), ct);
        await _orcamentoRepository.SalvarAlteracoesAsync(ct);
        return orcamento;
    }

    public async Task<IEnumerable<OrcamentoHistorico>> ObterHistoricoAsync(Guid id, CancellationToken ct = default)
        => await _orcamentoRepository.ObterHistoricoAsync(id, ct);

    private static bool RowVersionConfere(Orcamento orcamento, byte[] rowVersion)
        => rowVersion.Length > 0 && CryptographicOperations.FixedTimeEquals(orcamento.RowVersion, rowVersion);

    private static string Hash(object request)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request))));

    private async Task<ResultadoEnvio> RegistrarConflitoEnvioAsync(
        Orcamento orcamento,
        int usuarioId,
        string canal,
        string codigoErro,
        CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var jaEnviado = orcamento.Status != StatusOrcamento.Rascunho;
        var detalhes = codigoErro == "ORC_JA_ENVIADO"
            ? "Tentativa de envio rejeitada: orçamento já enviado."
            : "Tentativa de envio rejeitada: ETag desatualizado.";
        await _orcamentoRepository.AdicionarHistoricoAsync(new OrcamentoHistorico(
            orcamento.Id,
            orcamento.Status,
            orcamento.Status,
            usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            canal,
            agora,
            detalhes), ct);
        await _orcamentoRepository.SalvarAlteracoesAsync(ct);

        var enviadoPor = int.TryParse(orcamento.EnviadoPor, out var id)
            ? new UsuarioInfoResponse(id, $"Usuário {id}")
            : null;
        return ResultadoEnvio.Falha(
            codigoErro,
            codigoErro == "ORC_JA_ENVIADO"
                ? "Este orçamento já foi enviado por outro usuário."
                : "O orçamento foi alterado por outro usuário.",
            jaEnviado ? enviadoPor : null,
            jaEnviado ? orcamento.EnviadoEm : null);
    }

    private static OrcamentoResponse Mapear(Orcamento orcamento)
        => new(orcamento.Id, orcamento.OficinaId, orcamento.OrdemServicoId, orcamento.VeiculoId, orcamento.ClienteId, orcamento.Status.ToString(), orcamento.Versao, orcamento.Total, orcamento.Desconto, orcamento.DescontoPercentual, orcamento.ValorAprovado, Convert.ToBase64String(orcamento.RowVersion), orcamento.Itens.Select(item => new ItemOrcamentoResponse(item.Id, item.Tipo, item.ReferenciaId, item.ItemPaiId, item.Descricao, item.Quantidade, item.ValorUnitario, item.ValorTotal, item.PecaPendente, item.Decisao)).ToList());
}
