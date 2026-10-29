using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Clima.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "clima");

            migrationBuilder.CreateTable(
                name: "encuestas",
                schema: "clima",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    titulo = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    incluye_enps = table.Column<bool>(type: "boolean", nullable: false),
                    pregunta_abierta = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    areas = table.Column<List<string>>(type: "text[]", nullable: false),
                    sedes = table.Column<List<string>>(type: "text[]", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    token_hash = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    abierta_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cerrada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    creada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    analisis = table.Column<string>(type: "jsonb", nullable: true),
                    preguntas = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_encuestas", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "clima",
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
                schema: "clima",
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

            migrationBuilder.CreateTable(
                name: "pulsos",
                schema: "clima",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cargo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empresa_usuaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fecha_ingreso = table.Column<DateOnly>(type: "date", nullable: false),
                    hito = table.Column<int>(type: "integer", nullable: false),
                    pendiente_desde = table.Column<DateOnly>(type: "date", nullable: false),
                    alerta = table.Column<bool>(type: "boolean", nullable: false),
                    respuesta = table.Column<string>(type: "jsonb", nullable: true),
                    seguimiento = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pulsos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "respuestas",
                schema: "clima",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    encuesta_id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sede = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    enps = table.Column<int>(type: "integer", nullable: true),
                    comentario = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    fecha = table.Column<DateOnly>(type: "date", nullable: false),
                    puntajes = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_respuestas", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_encuestas_empresa_id_creada_en",
                schema: "clima",
                table: "encuestas",
                columns: new[] { "empresa_id", "creada_en" });

            migrationBuilder.CreateIndex(
                name: "ix_encuestas_token_hash",
                schema: "clima",
                table: "encuestas",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "clima",
                table: "inbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "clima",
                table: "outbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_pulsos_documento",
                schema: "clima",
                table: "pulsos",
                column: "documento");

            migrationBuilder.CreateIndex(
                name: "ix_pulsos_empresa_id_empleado_id_hito",
                schema: "clima",
                table: "pulsos",
                columns: new[] { "empresa_id", "empleado_id", "hito" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_respuestas_encuesta_id",
                schema: "clima",
                table: "respuestas",
                column: "encuesta_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "encuestas",
                schema: "clima");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "clima");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "clima");

            migrationBuilder.DropTable(
                name: "pulsos",
                schema: "clima");

            migrationBuilder.DropTable(
                name: "respuestas",
                schema: "clima");
        }
    }
}
