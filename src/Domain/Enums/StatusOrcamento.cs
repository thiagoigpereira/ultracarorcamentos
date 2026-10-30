using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Enums
{
    public enum StatusOrcamento
    {
        Rascunho = 1,
        Enviado = 2,
        AprovadoTotal = 3,
        AprovadoParcial = 4,
        Recusado = 5,
        Cancelado = 6
    }
}
