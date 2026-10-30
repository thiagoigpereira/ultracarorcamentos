using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Entities;

public class Orcamento
{
    public Guid Id { get; private set; }
    public int OficinaId { get; private set; }
    public int OrdemServicoId { get; private set; }
    public Guid VeiculoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public StatusOrcamento Status { get; private set; }
    public int Versao { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>(); 
    public string? TokenPublico { get; private set; }
    public DateTime? TokenExpiraEm { get; private set; }
    public Guid? EnviadoPor { get; private set; }
    public DateTime? EnviadoEm { get; private set; }
    public decimal Total { get; private set; }
    public decimal ValorAprovado { get; private set; }

    private readonly List<OrcamentoItem> _itens = new();
    public IReadOnlyCollection<OrcamentoItem> Itens => _itens.AsReadOnly();

    private Orcamento() { } 

    public Orcamento(int oficinaId, int ordemServicoId, Guid veiculoId, Guid clienteId)
    {
        Id = Guid.NewGuid();
        OficinaId = oficinaId;
        OrdemServicoId = ordemServicoId;
        VeiculoId = veiculoId;
        ClienteId = clienteId;
        Status = StatusOrcamento.Rascunho;
        Versao = 1;
    }

    
    public void SubstituirItens(IEnumerable<OrcamentoItem> novosItens, decimal descontoPercentual = 0, decimal descontoValor = 0)
    {
        ValidarModificacaoRascunho();

        _itens.Clear();
        _itens.AddRange(novosItens);

        RecalcularTotais(descontoPercentual, descontoValor);
    }

    
    public void Enviar(Guid usuarioId, DateTime agora)
    {
        ValidarModificacaoRascunho();

        if (!_itens.Any())
            throw new DomainException("ORC_VAZIO", "Orçamento sem itens.");

        if (_itens.Any(i => i.PecaPendente))
            throw new DomainException("ORC_PECA_PENDENTE", "Há itens sem peça definida.");

        Status = StatusOrcamento.Enviado;
        EnviadoPor = usuarioId;
        EnviadoEm = agora;
        TokenPublico = Guid.NewGuid().ToString("N");
        TokenExpiraEm = agora.AddDays(7);
    }

    
    public void RegistrarDecisaoCliente(IDictionary<Guid, DecisaoItem> decisoes, string? motivoRecusa, DateTime agora)
    {
        if (Status != StatusOrcamento.Enviado)
            throw new DomainException("ORC_DECISAO_INVALIDA", $"Não é possível decidir um orçamento em estado {Status}.");

        if (TokenExpiraEm.HasValue && agora > TokenExpiraEm.Value)
            throw new DomainException("ORC_LINK_EXPIRADO", "O link do orçamento expirou.");

        
        foreach (var (itemId, decisao) in decisoes)
        {
            var item = _itens.FirstOrDefault(i => i.Id == itemId)
                ?? throw new DomainException("ORC_ITEM_NAO_ENCONTRADO", $"Item {itemId} não encontrado.");

            if (item.ItemPaiId.HasValue && decisao == DecisaoItem.Aprovado)
            {
                var decisaoServicoPai = decisoes.TryGetValue(item.ItemPaiId.Value, out var dPai) ? dPai : DecisaoItem.Pendente;
                if (decisaoServicoPai == DecisaoItem.Recusado)
                {
                    throw new DomainException("ORC_PECA_SEM_SERVICO", "Não é possível aprovar a peça se o serviço correspondente foi recusado.");
                }
            }

            item.DefinirDecisao(decisao);
        }

        
        var aprovados = _itens.Count(i => i.Decisao == DecisaoItem.Aprovado);
        var recusados = _itens.Count(i => i.Decisao == DecisaoItem.Recusado);

        if (aprovados == _itens.Count)
            Status = StatusOrcamento.AprovadoTotal;
        else if (recusados == _itens.Count)
            Status = StatusOrcamento.Recusado;
        else
            Status = StatusOrcamento.AprovadoParcial;

        
        ValorAprovado = Math.Round(_itens.Where(i => i.Decisao == DecisaoItem.Aprovado).Sum(i => i.ValorTotal), 2, MidpointRounding.AwayFromZero);
    }

    
    public void Revisar()
    {
        if (Status != StatusOrcamento.Enviado)
            throw new DomainException("ORC_REVISAO_INVALIDA", "Apenas orçamentos enviados podem ser revisados.");

        Status = StatusOrcamento.Rascunho;
        Versao++;
        TokenPublico = null;
        TokenExpiraEm = null;
    }

    private void ValidarModificacaoRascunho()
    {
        if (Status != StatusOrcamento.Rascunho)
            throw new DomainException("ORC_JA_ENVIADO", $"Orçamento está em estado {Status} e não pode ser alterado.");
    }

    
    private void RecalcularTotais(decimal descontoPercentual, decimal descontoValor)
    {
        var somaBruta = _itens.Sum(i => i.ValorTotal);
        var totalComDesconto = somaBruta;

        if (descontoPercentual > 0)
            totalComDesconto -= (somaBruta * (descontoPercentual / 100m));

        totalComDesconto -= descontoValor;

        Total = Math.Round(Math.Max(0, totalComDesconto), 2, MidpointRounding.AwayFromZero);
    }
}