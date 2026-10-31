namespace Application.DTOs;

public record DecisaoClienteRequest(
    string Tipo,
    List<DecisaoItemRequest>? Itens,
    string? MotivoRecusa
);

public record DecisaoItemRequest(
    Guid ItemId,
    string Decisao
);

public class ResultadoConsultaPublica
{
    public bool Sucesso { get; set; }
    public string? CodigoErro { get; set; }
    public object? Dados { get; set; }

    public static ResultadoConsultaPublica Ok(object dados) => new() { Sucesso = true, Dados = dados };
    public static ResultadoConsultaPublica Expirado() => new() { Sucesso = false, CodigoErro = "ORC_LINK_EXPIRADO" };
    public static ResultadoConsultaPublica NaoEncontrado() => new() { Sucesso = false, CodigoErro = "ORC_NAO_ENCONTRADO" };
}

public class ResultadoDecisao
{
    public bool Sucesso { get; set; }
    public string? CodigoErro { get; set; }
    public string? MensagemErro { get; set; }
    public string? StatusFinal { get; set; }

    public static ResultadoDecisao Ok(string statusFinal) => new() { Sucesso = true, StatusFinal = statusFinal };
    public static ResultadoDecisao JaRegistrada() => new() { Sucesso = false, CodigoErro = "ORC_DECISAO_JA_REGISTRADA" };
    public static ResultadoDecisao Expirado() => new() { Sucesso = false, CodigoErro = "ORC_LINK_EXPIRADO" };
    public static ResultadoDecisao Falha(string codigo, string mensagem) => new() { Sucesso = false, CodigoErro = codigo, MensagemErro = mensagem };
}
