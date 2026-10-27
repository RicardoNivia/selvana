using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogoMunicipiosDane : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "municipios",
                schema: "core",
                columns: table => new
                {
                    codigo = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    nombre = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    codigo_departamento = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    departamento = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    subregion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    busqueda = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_municipios", x => x.codigo);
                });

            migrationBuilder.CreateIndex(
                name: "ix_municipios_busqueda",
                schema: "core",
                table: "municipios",
                column: "busqueda");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "municipios",
                schema: "core");
        }
    }
}
