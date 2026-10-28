using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Reclutamiento.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CuentasDelPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cuentas_candidato",
                schema: "reclutamiento",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    contrasena_hash = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    archivo_hoja_de_vida_id = table.Column<Guid>(type: "uuid", nullable: true),
                    nombre_archivo_hoja_de_vida = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    hoja_de_vida_actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ultimo_ingreso = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos_fallidos = table.Column<int>(type: "integer", nullable: false),
                    bloqueada_hasta = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    autorizacion = table.Column<string>(type: "jsonb", nullable: false),
                    datos = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cuentas_candidato", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cuentas_candidato_correo",
                schema: "reclutamiento",
                table: "cuentas_candidato",
                column: "correo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cuentas_candidato_documento",
                schema: "reclutamiento",
                table: "cuentas_candidato",
                column: "documento");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cuentas_candidato",
                schema: "reclutamiento");
        }
    }
}
