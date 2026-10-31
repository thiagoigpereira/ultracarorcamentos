using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Data;

public static class DbInitializer
{
    public static readonly Guid VeiculoGolId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid DiagnosticoFreioId = Guid.Parse("22222222-2222-2222-2222-111111111111");

    public static async Task SeedAsync(AppDbContext context)
    {
        if (await context.Marcas.AnyAsync())
            return;

        var marcaVW = new Marca("Volkswagen");
        await context.Marcas.AddAsync(marcaVW);

        var modeloGol = new Modelo("Gol 1.0", marcaVW.Id);
        await context.Modelos.AddAsync(modeloGol);

        var veiculoGol = new Veiculo(VeiculoGolId, 42, "ABC1D23", marcaVW.Id, modeloGol.Id, 2019);
        await context.Veiculos.AddAsync(veiculoGol);

        var diagFreio = new Diagnostico(DiagnosticoFreioId, "DIAG-FREIO-01", "Ruído na frenagem", "Freio");
        await context.Diagnosticos.AddAsync(diagFreio);

        var servicoPastilhas = new Servico("Substituição de pastilhas dianteiras", 1.0m);
        await context.Servicos.AddAsync(servicoPastilhas);

        var diagServicoFreio = new DiagnosticoServico(diagFreio.Id, servicoPastilhas.Id);
        await context.DiagnosticoServicos.AddAsync(diagServicoFreio);

        var tipoPecaPastilhaId = Guid.NewGuid();
        var stpPastilha = new ServicoTipoPeca(servicoPastilhas.Id, tipoPecaPastilhaId, "Jogo de pastilhas dianteiras", 1);
        await context.ServicoTipoPecas.AddAsync(stpPastilha);

        var pecaBosch = new Peca("BB-1234", "Pastilha Bosch", tipoPecaPastilhaId, 189.90m, preferencial: true);
        await context.Pecas.AddAsync(pecaBosch);

        var compBoschGol = new PecaCompatibilidade(pecaBosch.Id, modeloGol.Id, 2015, 2025);
        await context.PecaCompatibilidades.AddAsync(compBoschGol);

        await context.SaveChangesAsync();
    }
}
