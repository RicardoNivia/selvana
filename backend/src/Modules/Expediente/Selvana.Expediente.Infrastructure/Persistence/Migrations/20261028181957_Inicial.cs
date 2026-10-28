using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Expediente.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "expediente");

            migrationBuilder.CreateTable(
                name: "archivos",
                schema: "expediente",
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
                    table.PrimaryKey("pk_archivos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "empleados",
                schema: "expediente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    celular = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    ciudad = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    origen = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    proceso_vinculacion_id = table.Column<Guid>(type: "uuid", nullable: true),
                    candidato_id = table.Column<Guid>(type: "uuid", nullable: true),
                    cargo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    centro_costo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sede = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empresa_usuaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    empresa_usuaria = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    tipo_contrato = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    salario_cop = table.Column<decimal>(type: "numeric", nullable: false),
                    fecha_ingreso = table.Column<DateOnly>(type: "date", nullable: false),
                    fecha_fin_contrato = table.Column<DateOnly>(type: "date", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    fecha_retiro = table.Column<DateOnly>(type: "date", nullable: true),
                    motivo_retiro = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    proximo_vencimiento = table.Column<DateOnly>(type: "date", nullable: true),
                    afiliaciones_completas = table.Column<bool>(type: "boolean", nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    afiliaciones = table.Column<string>(type: "jsonb", nullable: false),
                    documentos = table.Column<string>(type: "jsonb", nullable: true),
                    novedades = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_empleados", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "expediente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    received_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    intentos = table.Column<int>(type: "integer", nullable: false),
                    error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_inbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "expediente",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_on = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_archivos_empresa_id",
                schema: "expediente",
                table: "archivos",
                column: "empresa_id");

            migrationBuilder.CreateIndex(
                name: "ix_empleados_codigo",
                schema: "expediente",
                table: "empleados",
                column: "codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_empleados_empresa_id_documento",
                schema: "expediente",
                table: "empleados",
                columns: new[] { "empresa_id", "documento" });

            migrationBuilder.CreateIndex(
                name: "ix_empleados_empresa_id_estado_nombre",
                schema: "expediente",
                table: "empleados",
                columns: new[] { "empresa_id", "estado", "nombre" });

            migrationBuilder.CreateIndex(
                name: "ix_empleados_proceso_vinculacion_id",
                schema: "expediente",
                table: "empleados",
                column: "proceso_vinculacion_id");

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "expediente",
                table: "inbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "expediente",
                table: "outbox_messages",
                column: "processed_on");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "archivos",
                schema: "expediente");

            migrationBuilder.DropTable(
                name: "empleados",
                schema: "expediente");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "expediente");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "expediente");
        }
    }
}
