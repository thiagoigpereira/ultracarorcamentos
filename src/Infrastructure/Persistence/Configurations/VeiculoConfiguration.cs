using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Persistence.Configurations;

public class VeiculoConfiguration : IEntityTypeConfiguration<Veiculo>
{
    public void Configure(EntityTypeBuilder<Veiculo> builder)
    {
        builder.ToTable("Veiculos");
        builder.HasKey(v => v.Id);

        builder.Property(v => v.Placa)
            .HasMaxLength(10)
            .IsRequired();

        
        builder.HasIndex(v => new { v.OficinaId, v.Placa })
            .IsUnique();

        builder.HasOne(v => v.Marca)
            .WithMany()
            .HasForeignKey(v => v.MarcaId);

        builder.HasOne(v => v.Modelo)
            .WithMany()
            .HasForeignKey(v => v.ModeloId);
    }
}

public class MarcaConfiguration : IEntityTypeConfiguration<Marca>
{
    public void Configure(EntityTypeBuilder<Marca> builder)
    {
        builder.ToTable("Marcas");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Nome).HasMaxLength(100).IsRequired();
    }
}

public class ModeloConfiguration : IEntityTypeConfiguration<Modelo>
{
    public void Configure(EntityTypeBuilder<Modelo> builder)
    {
        builder.ToTable("Modelos");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Nome).HasMaxLength(100).IsRequired();

        builder.HasOne(m => m.Marca)
            .WithMany(m => m.Modelos)
            .HasForeignKey(m => m.MarcaId);
    }
}