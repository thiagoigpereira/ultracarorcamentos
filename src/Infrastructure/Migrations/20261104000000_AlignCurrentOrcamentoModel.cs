using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignCurrentOrcamentoModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "DataExpiracao",
                table: "Orcamentos",
                newName: "TokenExpiraEm");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValorAprovado",
                table: "Orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2);

            migrationBuilder.AddColumn<DateTime>(
                name: "EnviadoEm",
                table: "Orcamentos",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "EnviadoPor",
                table: "Orcamentos",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Versao",
                table: "Orcamentos",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EnviadoEm",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "EnviadoPor",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "Versao",
                table: "Orcamentos");

            migrationBuilder.RenameColumn(
                name: "TokenExpiraEm",
                table: "Orcamentos",
                newName: "DataExpiracao");

            migrationBuilder.AlterColumn<decimal>(
                name: "ValorAprovado",
                table: "Orcamentos",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)",
                oldPrecision: 18,
                oldScale: 2,
                oldNullable: true);
        }
    }
}
