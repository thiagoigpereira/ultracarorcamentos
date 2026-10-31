using Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20261103000000_RepairValorMaoDeObraToServicos")]
public partial class RepairValorMaoDeObraToServicos : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            "DO $$ BEGIN IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = 'Servicos' AND column_name = 'ValorMaoDeObra') THEN ALTER TABLE \"Servicos\" ADD COLUMN \"ValorMaoDeObra\" numeric NOT NULL DEFAULT 0; END IF; END $$;");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
