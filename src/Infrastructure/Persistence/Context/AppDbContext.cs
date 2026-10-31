using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Orcamento> Orcamentos => Set<Orcamento>();
    public DbSet<OrcamentoItem> OrcamentoItens => Set<OrcamentoItem>();
    public DbSet<OrcamentoHistorico> OrcamentoHistoricos => Set<OrcamentoHistorico>();
    public DbSet<Veiculo> Veiculos => Set<Veiculo>();
    public DbSet<Marca> Marcas => Set<Marca>();
    public DbSet<Modelo> Modelos => Set<Modelo>();
    public DbSet<Diagnostico> Diagnosticos => Set<Diagnostico>();
    public DbSet<Servico> Servicos => Set<Servico>();
    public DbSet<DiagnosticoServico> DiagnosticoServicos => Set<DiagnosticoServico>();
    public DbSet<ServicoTipoPeca> ServicoTipoPecas => Set<ServicoTipoPeca>();
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<PecaCompatibilidade> PecaCompatibilidades => Set<PecaCompatibilidade>();
    public DbSet<IdempotencyKey> IdempotencyKeys => Set<IdempotencyKey>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
