using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConcurrencyAuditAndDiscountDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_OccurredOnUtc",
                table: "OutboxMessages");

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseExpiresOnUtc",
                table: "OutboxMessages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProcessingLeaseId",
                table: "OutboxMessages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EnviadoPor",
                table: "Orcamentos",
                type: "text",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DescontoPercentual",
                table: "Orcamentos",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MotivoRecusa",
                table: "Orcamentos",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "UsuarioId",
                table: "OrcamentoHistoricos",
                type: "text",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_LeaseExpiresOnUtc_OccurredOnU~",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "LeaseExpiresOnUtc", "OccurredOnUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_LeaseExpiresOnUtc_OccurredOnU~",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "LeaseExpiresOnUtc",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "ProcessingLeaseId",
                table: "OutboxMessages");

            migrationBuilder.DropColumn(
                name: "DescontoPercentual",
                table: "Orcamentos");

            migrationBuilder.DropColumn(
                name: "MotivoRecusa",
                table: "Orcamentos");

            migrationBuilder.AlterColumn<Guid>(
                name: "EnviadoPor",
                table: "Orcamentos",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "UsuarioId",
                table: "OrcamentoHistoricos",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_ProcessedOnUtc_OccurredOnUtc",
                table: "OutboxMessages",
                columns: new[] { "ProcessedOnUtc", "OccurredOnUtc" });
        }
    }
}
