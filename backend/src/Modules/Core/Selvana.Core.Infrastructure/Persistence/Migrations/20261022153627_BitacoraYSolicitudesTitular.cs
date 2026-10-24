using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Core.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BitacoraYSolicitudesTitular : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "bitacora",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    tipo_actor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    accion = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    detalle = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    nota = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    modulo = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    entidad = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    entidad_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dato_sensible = table.Column<bool>(type: "boolean", nullable: false),
                    huella_anterior = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    huella = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_bitacora", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "solicitudes_titular",
                schema: "core",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tipo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    titular = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    contacto = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    canal = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    descripcion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    recibida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    vence = table.Column<DateOnly>(type: "date", nullable: false),
                    registrada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    borrador_respuesta = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    respuesta = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    respondida_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    respondida_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    supresion_aprobada = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_solicitudes_titular", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bitacora_empresa_id_fecha",
                schema: "core",
                table: "bitacora",
                columns: new[] { "empresa_id", "fecha" });

            migrationBuilder.CreateIndex(
                name: "ix_bitacora_empresa_id_huella",
                schema: "core",
                table: "bitacora",
                columns: new[] { "empresa_id", "huella" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_bitacora_empresa_id_huella_anterior",
                schema: "core",
                table: "bitacora",
                columns: new[] { "empresa_id", "huella_anterior" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_solicitudes_titular_empresa_id_estado_vence",
                schema: "core",
                table: "solicitudes_titular",
                columns: new[] { "empresa_id", "estado", "vence" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bitacora",
                schema: "core");

            migrationBuilder.DropTable(
                name: "solicitudes_titular",
                schema: "core");
        }
    }
}
