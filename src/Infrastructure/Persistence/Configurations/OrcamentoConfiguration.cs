using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class OrcamentoConfiguration : IEntityTypeConfiguration<Orcamento>
{
    public void Configure(EntityTypeBuilder<Orcamento> builder)
    {
        builder.ToTable("Orcamentos");

        builder.HasKey(o => o.Id);

        builder.Property(o => o.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(o => o.Total)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.ValorAprovado)
            .HasPrecision(18, 2)
            .IsRequired(false);

        builder.Property(o => o.Desconto)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(o => o.MotivoRecusa)
            .HasMaxLength(1000);

        builder.Property(o => o.EnviadoPor)
            .HasColumnType("text");

        builder.Property(o => o.TokenPublico)
            .HasMaxLength(64);

        builder.Property(o => o.RowVersion)
            .IsConcurrencyToken()
            .ValueGeneratedNever();

        builder.HasIndex(o => new { o.OficinaId, o.OrdemServicoId })
            .IsUnique()
            .HasFilter("\"Status\" <> 'Cancelado'");

        builder.HasMany(o => o.Itens)
            .WithOne()
            .HasForeignKey(i => i.OrcamentoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
