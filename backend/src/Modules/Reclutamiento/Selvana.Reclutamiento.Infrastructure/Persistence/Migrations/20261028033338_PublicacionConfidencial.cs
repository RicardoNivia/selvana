using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PublicacionConfidencial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "empresa_confidencial",
                schema: "reclutamiento",
                table: "vacantes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "salario_oculto",
                schema: "reclutamiento",
                table: "vacantes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "empresa_confidencial",
                schema: "reclutamiento",
                table: "vacantes");

            migrationBuilder.DropColumn(
                name: "salario_oculto",
                schema: "reclutamiento",
                table: "vacantes");
        }
    }
}
