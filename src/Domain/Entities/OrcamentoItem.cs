using Domain.Enums;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Domain.Entities
{
    public class OrcamentoItem
    {
        public Guid Id { get; private set; }
        public Guid OrcamentoId { get; private set; }
        public string Tipo { get; private set; } = string.Empty;
        public Guid ReferenciaId { get; private set; }
        public Guid? ItemPaiId { get; private set; }
        public string Descricao { get; private set; } = string.Empty;
        public int Quantidade { get; private set; }
        public decimal ValorUnitario { get; private set; }
        public bool PecaPendente { get; private set; }
        public DecisaoItem Decisao { get; private set; } = DecisaoItem.Pendente;

        public decimal ValorTotal => Quantidade * ValorUnitario;

        private OrcamentoItem() { }

        public OrcamentoItem(string tipo, Guid referenciaId, string descricao, int quantidade, decimal valorUnitario, Guid? itemPaiId = null, bool pecaPendente = false)
        {
            Id = Guid.NewGuid();
            Tipo = tipo;
            ReferenciaId = referenciaId;
            Descricao = descricao;
            Quantidade = quantidade;
            ValorUnitario = valorUnitario;
            ItemPaiId = itemPaiId;
            PecaPendente = pecaPendente;
            Decisao = DecisaoItem.Pendente;
        }

        public void DefinirDecisao(DecisaoItem decisao)
        {
            Decisao = decisao;
        }
    }
}
