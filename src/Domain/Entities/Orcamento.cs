using Domain.Enums;
using Domain.Exceptions;
using System.Security.Cryptography;

namespace Domain.Entities;

public class Orcamento
{
    private readonly List<OrcamentoItem> _itens = new();

    public Guid Id { get; private set; }
    public int OficinaId { get; private set; }
    public int OrdemServicoId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public DateTime DataCriacao { get; private set; }
    public StatusOrcamento Status { get; private set; } = StatusOrcamento.Rascunho;
    public int Versao { get; private set; } = 1;
    public byte[] RowVersion { get; private set; } = Guid.NewGuid().ToByteArray();

    public string? TokenPublico { get; private set; }
    public DateTime? TokenExpiraEm { get; private set; }
    public string? EnviadoPor { get; private set; }
    public DateTime? EnviadoEm { get; private set; }

    public decimal Total { get; private set; }
    public decimal Desconto { get; private set; }
    public bool DescontoPercentual { get; private set; }
    public decimal? ValorAprovado { get; private set; }
    public string? MotivoRecusa { get; private set; }
    public IReadOnlyCollection<OrcamentoItem> Itens => _itens.AsReadOnly();

    private Orcamento() { }

    public Orcamento(int oficinaId, int ordemServicoId, Guid veiculoId, Guid clienteId)
    {
        Id = Guid.NewGuid();
        OficinaId = oficinaId;
        OrdemServicoId = ordemServicoId;
        VeiculoId = veiculoId;
        ClienteId = clienteId;
        DataCriacao = DateTime.UtcNow;
    }

    public void SubstituirItens(IEnumerable<OrcamentoItem> itens, decimal desconto = 0m, bool descontoPercentual = false)
        => SubstituirItens(itens, desconto, descontoPercentual, Guid.NewGuid().ToByteArray());

    public void SubstituirItens(IEnumerable<OrcamentoItem> itens, decimal desconto, bool descontoPercentual, byte[] rowVersion)
    {
        GarantirRascunho();
        ValidarDesconto(desconto, descontoPercentual);

        _itens.Clear();
        _itens.AddRange(itens);
        Desconto = Arredondar(desconto);
        DescontoPercentual = descontoPercentual;
        RecalcularTotal();
        RowVersion = rowVersion;
    }

    public static void ValidarDesconto(decimal desconto, bool descontoPercentual)
    {
        if (desconto < 0)
            throw new DomainException("ORC_DESCONTO_INVALIDO", "O desconto não pode ser negativo.");
        if (descontoPercentual && desconto > 100)
            throw new DomainException("ORC_DESCONTO_INVALIDO", "O desconto percentual não pode exceder 100%.");
    }

    public void Enviar(int usuarioId, DateTime agoraUtc, string canal)
    {
        ValidarEnvio();

        TokenPublico = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        TokenExpiraEm = agoraUtc.AddDays(7);
        EnviadoPor = usuarioId.ToString(System.Globalization.CultureInfo.InvariantCulture);
        EnviadoEm = agoraUtc;
        Status = StatusOrcamento.Enviado;
        AtualizarRowVersion();
    }

    public void ValidarEnvio()
    {
        if (Status != StatusOrcamento.Rascunho)
            throw new DomainException("ORC_JA_ENVIADO", "O orçamento não está em rascunho.");
        if (_itens.Count == 0)
            throw new DomainException("ORC_VAZIO", "O orçamento não possui itens.");
        if (_itens.Any(i => i.PecaPendente))
            throw new DomainException("ORC_PECA_PENDENTE", "O orçamento possui peças pendentes.");
    }

    public void Revisar()
    {
        if (Status != StatusOrcamento.Enviado)
            throw new DomainException("ORC_REVISAO_INVALIDA", "Apenas orçamentos enviados podem ser revisados.");

        Status = StatusOrcamento.Rascunho;
        Versao++;
        TokenPublico = null;
        TokenExpiraEm = null;
        EnviadoPor = null;
        EnviadoEm = null;
        ValorAprovado = null;
        MotivoRecusa = null;
        foreach (var item in _itens)
            item.LimparDecisao();
        AtualizarRowVersion();
    }

    public void AplicarDecisao(string tipo, IReadOnlyDictionary<Guid, string>? decisoes = null, string? motivoRecusa = null)
    {
        if (Status != StatusOrcamento.Enviado && _itens.Any(i => i.Decisao != "Pendente"))
            throw new DomainException("ORC_DECISAO_JA_REGISTRADA", "A decisão já foi registrada.");
        GarantirPendenteAprovacao();
        if (TokenExpiraEm is null || TokenExpiraEm <= DateTime.UtcNow)
            throw new DomainException("ORC_LINK_EXPIRADO", "O link público expirou.");
        if (_itens.Any(i => i.Decisao != "Pendente"))
            throw new DomainException("ORC_DECISAO_JA_REGISTRADA", "A decisão já foi registrada.");

        if (tipo == "Total")
        {
            foreach (var item in _itens)
                item.DefinirDecisao("Aprovado");
            ValorAprovado = Total;
            Status = StatusOrcamento.AprovadoTotal;
            MotivoRecusa = null;
            AtualizarRowVersion();
            return;
        }

        if (tipo == "Recusa")
        {
            foreach (var item in _itens)
                item.DefinirDecisao("Recusado");
            ValorAprovado = 0m;
            Status = StatusOrcamento.Recusado;
            MotivoRecusa = motivoRecusa;
            AtualizarRowVersion();
            return;
        }

        if (tipo != "Parcial" || decisoes is null || decisoes.Count != _itens.Count)
            throw new DomainException("ORC_DECISAO_INVALIDA", "A decisão enviada é inválida.");

        var decisoesValidas = new Dictionary<Guid, string>();
        foreach (var item in _itens)
        {
            if (!decisoes.TryGetValue(item.Id, out var decisao))
                throw new DomainException("ORC_DECISAO_INVALIDA", "Todos os itens devem receber uma decisão.");
            if (decisao == "Aprovado" && item.ItemPaiId.HasValue)
            {
                var pai = _itens.SingleOrDefault(i => i.Id == item.ItemPaiId);
                if (pai?.Decisao == "Recusado" || decisoes.GetValueOrDefault(item.ItemPaiId.Value) == "Recusado")
                    throw new DomainException("ORC_PECA_SEM_SERVICO", "Uma peça não pode ser aprovada sem o serviço pai.");
            }
            if (decisao is not ("Aprovado" or "Recusado"))
                throw new DomainException("ORC_DECISAO_INVALIDA", "A decisão do item é inválida.");
            decisoesValidas.Add(item.Id, decisao);
        }

        var aprovados = decisoesValidas.Values.Count(decisao => decisao == "Aprovado");
        var recusados = decisoesValidas.Values.Count(decisao => decisao == "Recusado");
        if (aprovados == 0 || recusados == 0)
            throw new DomainException("ORC_DECISAO_INVALIDA", "A decisão parcial deve aprovar e recusar itens.");

        foreach (var item in _itens)
            item.DefinirDecisao(decisoesValidas[item.Id]);
        ValorAprovado = Arredondar(_itens.Where(i => i.Decisao == "Aprovado").Sum(i => i.ValorTotal));
        Status = StatusOrcamento.AprovadoParcial;
        MotivoRecusa = motivoRecusa;
        AtualizarRowVersion();
    }

    public void RecalcularTotal()
    {
        var subtotal = _itens.Sum(i => i.ValorTotal);
        var descontoAplicado = DescontoPercentual ? subtotal * Desconto / 100m : Desconto;
        Total = Arredondar(subtotal - descontoAplicado);
        if (Total < 0)
            Total = 0;
    }


    public void AprovarTotal()
    {
        GarantirPendenteAprovacao();

        ValorAprovado = Total;
        Status = StatusOrcamento.AprovadoTotal;
        AtualizarRowVersion();
    }

    public void AprovarParcialmente(decimal valorAprovado)
    {
        GarantirPendenteAprovacao();

        if (valorAprovado <= 0 || valorAprovado >= Total)
            throw new InvalidOperationException("O valor de aprovação parcial deve ser maior que zero e menor que o valor total.");

        ValorAprovado = valorAprovado;
        Status = StatusOrcamento.AprovadoParcial;
        AtualizarRowVersion();
    }

    public void Recusar()
    {
        GarantirPendenteAprovacao();

        ValorAprovado = 0m;
        Status = StatusOrcamento.Recusado;
        AtualizarRowVersion();
    }

    public void Cancelar()
    {
        if (Status == StatusOrcamento.AprovadoTotal || Status == StatusOrcamento.AprovadoParcial)
            throw new InvalidOperationException("Não é possível cancelar um orçamento já aprovado.");

        Status = StatusOrcamento.Cancelado;
        AtualizarRowVersion();
    }

    private void GarantirPendenteAprovacao()
    {
        if (Status != StatusOrcamento.Enviado)
            throw new InvalidOperationException("Apenas orçamentos no status 'Enviado' podem ser respondidos.");
    }

    private void GarantirRascunho()
    {
        if (Status != StatusOrcamento.Rascunho)
            throw new DomainException("ORC_EDICAO_BLOQUEADA", "Apenas orçamentos em rascunho podem ser editados.");
    }

    private void AtualizarRowVersion() => RowVersion = Guid.NewGuid().ToByteArray();

    private static decimal Arredondar(decimal valor) => Math.Round(valor, 2, MidpointRounding.AwayFromZero);
}
