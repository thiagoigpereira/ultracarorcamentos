using Application.DTOs;

namespace Application.Common;

public class ResultadoOperacao
{
    public bool Sucesso { get; protected set; }
    public string? CodigoErro { get; protected set; }
    public string? MensagemErro { get; protected set; }
    public OrcamentoResponse? Orcamento { get; protected set; }

    protected ResultadoOperacao(bool sucesso, string? codigoErro = null, string? mensagemErro = null, OrcamentoResponse? orcamento = null)
    {
        Sucesso = sucesso;
        CodigoErro = codigoErro;
        MensagemErro = mensagemErro;
        Orcamento = orcamento;
    }

    public static ResultadoOperacao OK(OrcamentoResponse orcamento) => new(true, orcamento: orcamento);
    public static ResultadoOperacao OK() => new(true);
    public static ResultadoOperacao Falha(string codigo, string mensagem) => new(false, codigo, mensagem);
}

public class ResultadoEnvio : ResultadoOperacao
{
    public string? LinkCliente { get; private set; }
    public UsuarioInfoResponse? EnviadoPor { get; private set; }
    public DateTime? EnviadoEm { get; private set; }
    public bool FoiReprocessado { get; private set; }
    public string? RespostaJson { get; private set; }

    private ResultadoEnvio(bool sucesso, string? codigoErro = null, string? mensagemErro = null, string? linkCliente = null, UsuarioInfoResponse? enviadoPor = null, DateTime? enviadoEm = null, bool foiReprocessado = false, string? respostaJson = null)
        : base(sucesso, codigoErro, mensagemErro)
    {
        LinkCliente = linkCliente;
        EnviadoPor = enviadoPor;
        EnviadoEm = enviadoEm;
        FoiReprocessado = foiReprocessado;
        RespostaJson = respostaJson;
    }

    public static ResultadoEnvio OK(string linkCliente, UsuarioInfoResponse enviadoPor, DateTime enviadoEm, string respostaJson)
        => new(true, linkCliente: linkCliente, enviadoPor: enviadoPor, enviadoEm: enviadoEm, respostaJson: respostaJson);

    public static ResultadoEnvio Reprocessado(string respostaJson)
        => new(true, linkCliente: respostaJson, foiReprocessado: true, respostaJson: respostaJson);

    public static ResultadoEnvio Falha(string codigo, string mensagem, UsuarioInfoResponse? enviadoPor = null, DateTime? enviadoEm = null)
        => new(false, codigoErro: codigo, mensagemErro: mensagem, enviadoPor: enviadoPor, enviadoEm: enviadoEm);
}

public record UsuarioInfoResponse(int Id, string Nome);
