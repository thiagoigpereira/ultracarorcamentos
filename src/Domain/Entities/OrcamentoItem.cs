namespace Domain.Entities;

public class OrcamentoItem : Entity
{
    public Guid OrcamentoId { get; private set; }
    public string Tipo { get; private set; } = string.Empty;
    public Guid? ReferenciaId { get; private set; }
    public Guid? ItemPaiId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public int Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public bool PecaPendente { get; private set; }
    public string Decisao { get; private set; } = "Pendente";
    public decimal ValorTotal => Math.Round(Quantidade * ValorUnitario, 2, MidpointRounding.AwayFromZero);

    public OrcamentoItem() { }

    public OrcamentoItem(
        Guid orcamentoId,
        string tipo,
        Guid? referenciaId,
        Guid? itemPaiId,
        string descricao,
        int quantidade,
        decimal valorUnitario,
        bool pecaPendente = false)
    {
        if (quantidade <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantidade));
        if (valorUnitario < 0)
            throw new ArgumentOutOfRangeException(nameof(valorUnitario));

        OrcamentoId = orcamentoId;
        Tipo = tipo;
        ReferenciaId = referenciaId;
        ItemPaiId = itemPaiId;
        Descricao = descricao;
        Quantidade = quantidade;
        ValorUnitario = valorUnitario;
        PecaPendente = pecaPendente;
    }

    public void DefinirDecisao(string decisao)
    {
        if (Decisao != "Pendente")
            throw new Domain.Exceptions.DomainException("ORC_DECISAO_JA_REGISTRADA", "A decisão deste item já foi registrada.");

        if (decisao is not ("Aprovado" or "Recusado"))
            throw new Domain.Exceptions.DomainException("ORC_DECISAO_INVALIDA", "A decisão do item é inválida.");

        Decisao = decisao;
    }

    internal void LimparDecisao() => Decisao = "Pendente";
}
