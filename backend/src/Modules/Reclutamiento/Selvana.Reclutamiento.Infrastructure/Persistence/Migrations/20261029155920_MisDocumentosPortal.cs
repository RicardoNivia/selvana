using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class MisDocumentosPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "documentos",
                schema: "reclutamiento",
                table: "cuentas_candidato",
                type: "jsonb",
                nullable: true);

            // Las cuentas que ya existían quedan con la lista vacía
            migrationBuilder.Sql("UPDATE reclutamiento.cuentas_candidato SET documentos = '[]'::jsonb WHERE documentos IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "documentos",
                schema: "reclutamiento",
                table: "cuentas_candidato");
        }
    }
}
