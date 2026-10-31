using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class OrcamentoItemConfiguration : IEntityTypeConfiguration<OrcamentoItem>
{
    public void Configure(EntityTypeBuilder<OrcamentoItem> builder)
    {
        builder.ToTable("OrcamentoItens");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.Tipo)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(i => i.PecaPendente)
            .IsRequired();

        builder.Property(i => i.Descricao)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(i => i.ValorUnitario)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(i => i.Decisao)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        
        builder.HasOne<OrcamentoItem>()
            .WithMany()
            .HasForeignKey(i => i.ItemPaiId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
