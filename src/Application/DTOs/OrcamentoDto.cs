namespace Application.DTOs;

public record CriarOrcamentoRequest(
    int OficinaId,
    int OrdemServicoId,
    Guid VeiculoId,
    Guid ClienteId
);

public record AtualizarItensRequest(
    List<ItemOrcamentoInputDto> Itens,
    decimal Desconto = 0m,
    bool DescontoPercentual = false
);

public record ItemOrcamentoInputDto(
    string Tipo,
    Guid ReferenciaId,
    Guid? ItemPaiId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    bool PecaPendente = false
);

public record EnviarOrcamentoRequest(
    string Canal,
    string Mensagem
);

public record OrcamentoResponse(
    Guid Id,
    int OficinaId,
    int OrdemServicoId,
    Guid VeiculoId,
    Guid ClienteId,
    string Status,
    int Versao,
    decimal Total,
    decimal Desconto,
    bool DescontoPercentual,
    decimal? ValorAprovado,
    string RowVersionBase64,
    List<ItemOrcamentoResponse> Itens
);

public record ItemOrcamentoResponse(
    Guid Id,
    string Tipo,
    Guid? ReferenciaId,
    Guid? ItemPaiId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotal,
    bool PecaPendente,
    string Decisao
);
