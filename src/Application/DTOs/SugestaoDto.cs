namespace Application.DTOs;

public record CriarSugestaoRequest(
    Guid VeiculoId,
    List<Guid> DiagnosticoIds
);

public record SugestaoResponse(
    VeiculoSugestaoDto Veiculo,
    List<ServicoSugestaoDto> Servicos
);

public record VeiculoSugestaoDto(
    Guid Id,
    string Marca,
    string Modelo,
    int Ano
);

public record ServicoSugestaoDto(
    Guid ServicoId,
    string Descricao,
    Guid OrigemDiagnosticoId,
    decimal TempoHoras,
    decimal ValorMaoDeObra,
    List<PecaTipoSugestaoDto> Pecas
);

public record PecaTipoSugestaoDto(
    string TipoPeca,
    int Quantidade,
    bool PecaPendente,
    List<PecaOpcaoDto> Opcoes
);

public record PecaOpcaoDto(
    Guid PecaId,
    string Codigo,
    string Descricao,
    decimal Preco,
    bool Preferencial
);
