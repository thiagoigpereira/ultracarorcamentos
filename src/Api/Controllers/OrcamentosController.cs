using Application.DTOs;
using Application.Interfaces;
using Domain.Entities;
using Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/orcamentos")]
public class OrcamentosController : ControllerBase
{
    private readonly ISugestaoService _sugestaoService;
    private readonly IOrcamentoService _orcamentoService;

    public OrcamentosController(ISugestaoService sugestaoService, IOrcamentoService orcamentoService)
    {
        _sugestaoService = sugestaoService;
        _orcamentoService = orcamentoService;
    }

    [HttpPost("sugerir")]
    [HttpPost("~/api/v1/sugestoes")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Sugerir([FromBody] CriarSugestaoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _sugestaoService.GerarSugestaoAsync(request, cancellationToken);
            return Ok(resultado);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Criar([FromBody] CriarOrcamentoRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var resultado = await _orcamentoService.CriarOrcamentoAsync(request, cancellationToken);
            var response = MapearOrcamentoResponse(resultado);
            Response.Headers.ETag = $"\"{response.RowVersionBase64}\"";
            return CreatedAtAction(nameof(ObterPorId), new { id = resultado.Id }, response);
        }
        catch (DomainException ex)
        {
            return BadRequest(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpPost("/api/v1/ordens-servico/{osId:int}/orcamento")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterOuCriarPorOS(
        [FromRoute] int osId,
        [FromHeader(Name = "X-Oficina-Id")] int? oficinaId,
        [FromBody] CriarOrcamentoRequest request,
        CancellationToken ct)
    {
        if (!oficinaId.HasValue || oficinaId.Value <= 0)
            return BadRequest(new { code = "HEADER_INVALIDO", message = "X-Oficina-Id deve conter um identificador numérico positivo." });

        var (orcamento, foiCriado) = await _orcamentoService.ObterOuCriarPorOSAsync(oficinaId.Value, osId, request, ct);

        var response = MapearOrcamentoResponse(orcamento);
        Response.Headers.ETag = $"\"{response.RowVersionBase64}\"";

        if (foiCriado)
            return CreatedAtAction(nameof(ObterPorId), new { id = orcamento.Id }, response);

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorId([FromRoute] Guid id, CancellationToken ct)
    {
        var orcamento = await _orcamentoService.ObterPorIdAsync(id, ct);
        if (orcamento == null)
            return NotFound(new { code = "ORCAMENTO_NAO_ENCONTRADO", message = "Orçamento não encontrado." });

        var response = MapearOrcamentoResponse(orcamento);
        Response.Headers.ETag = $"\"{response.RowVersionBase64}\"";

        return Ok(response);
    }

    [HttpPut("{id:guid}/itens")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AtualizarItens(
        [FromRoute] Guid id,
        [FromHeader(Name = "X-Oficina-Id")] int? oficinaId,
        [FromHeader(Name = "If-Match")] string ifMatch,
        [FromHeader(Name = "X-User-Id")] int? usuarioId,
        [FromBody] AtualizarItensRequest request,
        CancellationToken ct)
    {
        if (!oficinaId.HasValue || oficinaId.Value <= 0)
            return BadRequest(new { code = "HEADER_INVALIDO", message = "X-Oficina-Id deve conter um identificador numérico positivo." });
        if (!usuarioId.HasValue || usuarioId.Value <= 0)
            return BadRequest(new { code = "HEADER_INVALIDO", message = "X-User-Id deve conter um identificador numérico positivo." });

        var rowVersion = ExtrairRowVersion(ifMatch);

        var resultado = await _orcamentoService.AtualizarItensAsync(id, oficinaId.Value, rowVersion, usuarioId.Value, request, ct);
        if (!resultado.Sucesso)
        {
            if (resultado.CodigoErro == "ORC_NAO_ENCONTRADO")
                return NotFound(new { code = resultado.CodigoErro, message = resultado.MensagemErro });

            if (resultado.CodigoErro == "ORC_VERSAO_DESATUALIZADA")
                return StatusCode(StatusCodes.Status412PreconditionFailed, new { code = "ORC_VERSAO_DESATUALIZADA", message = "O orçamento foi modificado por outro usuário." });

            return BadRequest(new { code = resultado.CodigoErro, message = resultado.MensagemErro });
        }

        Response.Headers.ETag = $"\"{resultado.Orcamento!.RowVersionBase64}\"";
        return Ok(resultado.Orcamento);
    }

    [HttpPost("{id:guid}/envio")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Enviar(
        [FromRoute] Guid id,
        [FromHeader(Name = "X-Oficina-Id")] int? oficinaId,
        [FromHeader(Name = "If-Match")] string ifMatch,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromHeader(Name = "X-User-Id")] int? usuarioId,
        [FromBody] EnviarOrcamentoRequest request,
        CancellationToken ct)
    {
        if (!oficinaId.HasValue || oficinaId.Value <= 0)
            return BadRequest(new { code = "HEADER_INVALIDO", message = "X-Oficina-Id deve conter um identificador numérico positivo." });
        if (!usuarioId.HasValue || usuarioId.Value <= 0)
            return BadRequest(new { code = "HEADER_INVALIDO", message = "X-User-Id deve conter um identificador numérico positivo." });

        var rowVersion = ExtrairRowVersion(ifMatch);

        var resultado = await _orcamentoService.EnviarAsync(id, oficinaId.Value, rowVersion, idempotencyKey, usuarioId.Value, request, ct);

        if (!resultado.Sucesso)
        {
            if (resultado.CodigoErro == "ORC_JA_ENVIADO")
            {
                return Conflict(new
                {
                    code = "ORC_JA_ENVIADO",
                    message = "Este orçamento já foi enviado por outro usuário.",
                    enviadoPor = resultado.EnviadoPor,
                    enviadoEm = resultado.EnviadoEm
                });
            }

            if (resultado.CodigoErro == "ORC_VERSAO_DESATUALIZADA")
                return StatusCode(StatusCodes.Status412PreconditionFailed, new { code = "ORC_VERSAO_DESATUALIZADA", message = "O orçamento foi alterado por outro usuário." });

            return UnprocessableEntity(new { code = resultado.CodigoErro, message = resultado.MensagemErro });
        }

        return Content(resultado.RespostaJson!, "application/json");
    }

    [HttpPost("{id:guid}/revisao")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CriarRevisao(
        [FromRoute] Guid id,
        [FromHeader(Name = "X-User-Id")] int? usuarioId,
        CancellationToken ct)
    {
        try
        {
            if (!usuarioId.HasValue || usuarioId.Value <= 0)
                return BadRequest(new { code = "HEADER_INVALIDO", message = "X-User-Id deve conter um identificador numérico positivo." });

            var novoOrcamento = await _orcamentoService.CriarRevisaoAsync(id, usuarioId.Value, ct);
            var response = MapearOrcamentoResponse(novoOrcamento);
            Response.Headers.ETag = $"\"{response.RowVersionBase64}\"";
            return Ok(response);
        }
        catch (DomainException ex)
        {
            return Conflict(new { code = ex.Code, message = ex.Message });
        }
    }

    [HttpGet("{id:guid}/historico")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ObterHistorico([FromRoute] Guid id, CancellationToken ct)
    {
        var historico = await _orcamentoService.ObterHistoricoAsync(id, ct);
        return Ok(historico);
    }

    private static byte[] ExtrairRowVersion(string ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
            return Array.Empty<byte>();

        var limpo = ifMatch.Replace("\"", "").Trim();
        return Convert.FromBase64String(limpo);
    }

    private static OrcamentoResponse MapearOrcamentoResponse(Orcamento orcamento)
    {
        return new OrcamentoResponse(
            Id: orcamento.Id,
            OficinaId: orcamento.OficinaId,
            OrdemServicoId: orcamento.OrdemServicoId,
            VeiculoId: orcamento.VeiculoId,
            ClienteId: orcamento.ClienteId,
            Status: orcamento.Status.ToString(),
            Versao: orcamento.Versao,
            Total: orcamento.Total,
            Desconto: orcamento.Desconto,
            DescontoPercentual: orcamento.DescontoPercentual,
            ValorAprovado: orcamento.ValorAprovado,
            RowVersionBase64: orcamento.RowVersion != null ? Convert.ToBase64String(orcamento.RowVersion) : string.Empty,
            Itens: orcamento.Itens.Select(i => new ItemOrcamentoResponse(
                Id: i.Id,
                Tipo: i.Tipo,
                ReferenciaId: i.ReferenciaId,
                ItemPaiId: i.ItemPaiId,
                Descricao: i.Descricao,
                Quantidade: i.Quantidade,
                ValorUnitario: i.ValorUnitario,
                ValorTotal: i.ValorTotal,
                PecaPendente: i.PecaPendente,
                Decisao: i.Decisao
            )).ToList()
        );
    }
}
