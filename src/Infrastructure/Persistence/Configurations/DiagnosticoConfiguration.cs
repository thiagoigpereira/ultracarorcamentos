using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class DiagnosticoConfiguration : IEntityTypeConfiguration<Diagnostico>
{
    public void Configure(EntityTypeBuilder<Diagnostico> builder)
    {
        builder.ToTable("Diagnosticos");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Codigo).HasMaxLength(50).IsRequired();
        builder.HasIndex(d => d.Codigo).IsUnique();
        builder.Property(d => d.Descricao).HasMaxLength(250).IsRequired();
        builder.Property(d => d.Sistema).HasMaxLength(100).IsRequired();
    }
}

public class DiagnosticoServicoConfiguration : IEntityTypeConfiguration<DiagnosticoServico>
{
    public void Configure(EntityTypeBuilder<DiagnosticoServico> builder)
    {
        builder.ToTable("DiagnosticoServicos");
        builder.HasKey(ds => new { ds.DiagnosticoId, ds.ServicoId }); 

        builder.HasOne(ds => ds.Diagnostico)
            .WithMany(d => d.DiagnosticoServicos)
            .HasForeignKey(ds => ds.DiagnosticoId);

        builder.HasOne(ds => ds.Servico)
            .WithMany()
            .HasForeignKey(ds => ds.ServicoId);
    }
}

public class ServicoTipoPecaConfiguration : IEntityTypeConfiguration<ServicoTipoPeca>
{
    public void Configure(EntityTypeBuilder<ServicoTipoPeca> builder)
    {
        builder.ToTable("ServicoTipoPecas");
        builder.HasKey(stp => new { stp.ServicoId, stp.TipoPecaId }); 

        builder.Property(stp => stp.TipoPecaNome).HasMaxLength(100).IsRequired();
    }
}

public class PecaCompatibilidadeConfiguration : IEntityTypeConfiguration<PecaCompatibilidade>
{
    public void Configure(EntityTypeBuilder<PecaCompatibilidade> builder)
    {
        builder.ToTable("PecaCompatibilidades");
        builder.HasKey(pc => new { pc.PecaId, pc.ModeloId, pc.AnoInicio, pc.AnoFim });

        builder.HasIndex(pc => new { pc.ModeloId, pc.AnoInicio, pc.AnoFim });
    }
}

public class IdempotencyKeyConfiguration : IEntityTypeConfiguration<IdempotencyKey>
{
    public void Configure(EntityTypeBuilder<IdempotencyKey> builder)
    {
        builder.ToTable("IdempotencyKeys");
        builder.HasKey(ik => new { ik.OficinaId, ik.Chave }); 
        builder.Property(ik => ik.Rota).HasMaxLength(200).IsRequired();
        builder.Property(ik => ik.HashRequest).HasMaxLength(64).IsRequired();
    }
}

public class OrcamentoHistoricoConfiguration : IEntityTypeConfiguration<OrcamentoHistorico>
{
    public void Configure(EntityTypeBuilder<OrcamentoHistorico> builder)
    {
        builder.ToTable("OrcamentoHistoricos");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Canal).HasMaxLength(50).IsRequired();
        builder.Property(h => h.De).HasConversion<string>().HasMaxLength(30);
        builder.Property(h => h.Para).HasConversion<string>().HasMaxLength(30).IsRequired();
    }
}