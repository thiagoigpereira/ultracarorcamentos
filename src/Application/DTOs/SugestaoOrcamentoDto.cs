namespace Application.DTOs;

public record SugestaoOrcamentoResponse(
    int OficinaId,
    int OrdemServicoId,
    Guid VeiculoId,
    string PlacaVeiculo,
    string ModeloVeiculo,
    int AnoVeiculo,
    string DiagnosticoCodigo,
    string DiagnosticoDescricao,
    List<ItemSugestaoResponse> Itens,
    decimal TotalEstimado
);

public record ItemSugestaoResponse(
    string Tipo,
    Guid ReferenciaId,
    Guid? ItemPaiId,
    string Descricao,
    int Quantidade,
    decimal ValorUnitario,
    decimal ValorTotal,
    bool PecaPendente
);
