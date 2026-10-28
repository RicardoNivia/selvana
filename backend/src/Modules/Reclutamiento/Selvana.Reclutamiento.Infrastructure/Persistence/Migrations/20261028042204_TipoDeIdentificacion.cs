using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TipoDeIdentificacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tipo_documento",
                schema: "reclutamiento",
                table: "cuentas_candidato",
                type: "character varying(4)",
                maxLength: 4,
                nullable: false,
                defaultValue: "CC");

            migrationBuilder.CreateIndex(
                name: "ix_cuentas_candidato_tipo_documento_documento",
                schema: "reclutamiento",
                table: "cuentas_candidato",
                columns: new[] { "tipo_documento", "documento" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_cuentas_candidato_tipo_documento_documento",
                schema: "reclutamiento",
                table: "cuentas_candidato");

            migrationBuilder.DropColumn(
                name: "tipo_documento",
                schema: "reclutamiento",
                table: "cuentas_candidato");
        }
    }
}
