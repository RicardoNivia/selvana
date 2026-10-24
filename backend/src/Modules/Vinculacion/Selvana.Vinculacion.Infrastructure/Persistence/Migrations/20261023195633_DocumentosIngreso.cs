using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Vinculacion.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DocumentosIngreso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "documentos",
                schema: "vinculacion",
                table: "procesos",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "archivos_documentos",
                schema: "vinculacion",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    tipo_contenido = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    tamano = table.Column<int>(type: "integer", nullable: false),
                    contenido_cifrado = table.Column<byte[]>(type: "bytea", nullable: false),
                    subido_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_archivos_documentos", x => x.id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archivos_documentos",
                schema: "vinculacion");

            migrationBuilder.DropColumn(
                name: "documentos",
                schema: "vinculacion",
                table: "procesos");
        }
    }
}
