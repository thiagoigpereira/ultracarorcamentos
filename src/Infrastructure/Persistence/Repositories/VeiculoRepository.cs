using Application.Interfaces;
using Domain.Entities;
using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class VeiculoRepository : IVeiculoRepository
{
    private readonly AppDbContext _context;

    public VeiculoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Veiculo?> ObterPorPlacaEOficinaAsync(string placa, int oficinaId, CancellationToken ct = default)
    {
        var placaNormalizada = placa.ToUpper().Trim();

        return await _context.Veiculos
            .AsNoTracking()
            .Include(v => v.Marca)
            .Include(v => v.Modelo)
            .FirstOrDefaultAsync(v => v.Placa == placaNormalizada && v.OficinaId == oficinaId, ct);
    }
}
