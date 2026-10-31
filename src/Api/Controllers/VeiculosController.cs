using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

[ApiController]
[Route("api/v1/veiculos")]
public class VeiculosController : ControllerBase
{
    private readonly IVeiculoRepository _veiculoRepository;

    public VeiculosController(IVeiculoRepository veiculoRepository)
    {
        _veiculoRepository = veiculoRepository;
    }

    [HttpGet("{placa}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObterPorPlaca([FromRoute] string placa, [FromHeader(Name = "X-Oficina-Id")] int? oficinaId, CancellationToken ct)
    {
        if (!oficinaId.HasValue || oficinaId.Value <= 0)
            return BadRequest(new { code = "HEADER_INVALIDO", message = "X-Oficina-Id deve conter um identificador numérico positivo." });

        var veiculo = await _veiculoRepository.ObterPorPlacaEOficinaAsync(placa, oficinaId.Value, ct);
        if (veiculo == null)
            return NotFound(new { code = "VEICULO_NAO_ENCONTRADO", message = "VeÃ­culo nÃ£o encontrado para a oficina." });

        return Ok(new
        {
            id = veiculo.Id,
            placa = veiculo.Placa,
            marca = veiculo.Marca.Nome,
            modelo = veiculo.Modelo.Nome,
            ano = veiculo.Ano
        });
    }
}
