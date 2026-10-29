using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Selvana.Desempeno.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Inicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "desempeno");

            migrationBuilder.CreateTable(
                name: "ciclos",
                schema: "desempeno",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    nombre = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    tipo = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    alcance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    desde = table.Column<DateOnly>(type: "date", nullable: false),
                    hasta = table.Column<DateOnly>(type: "date", nullable: false),
                    peso_objetivos = table.Column<int>(type: "integer", nullable: false),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    creado_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    formularios = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ciclos", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "evaluaciones",
                schema: "desempeno",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    empresa_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ciclo_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alcance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    peso_objetivos = table.Column<int>(type: "integer", nullable: false),
                    empleado_id = table.Column<Guid>(type: "uuid", nullable: false),
                    codigo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    nombre = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    documento = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    cargo = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sede = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    empresa_usuaria_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estado = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    comentario_lider = table.Column<string>(type: "text", nullable: false),
                    comentario_auto = table.Column<string>(type: "text", nullable: false),
                    autoevaluada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    calificada_por = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    calificada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    potencial = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    puntaje = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    creada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizada_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ajuste = table.Column<string>(type: "jsonb", nullable: true),
                    borrador = table.Column<string>(type: "jsonb", nullable: true),
                    competencias = table.Column<string>(type: "jsonb", nullable: true),
                    objetivos = table.Column<string>(type: "jsonb", nullable: true),
                    plan = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evaluaciones", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "inbox_messages",
                schema: "desempeno",
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
                schema: "desempeno",
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
                name: "ix_ciclos_empresa_id_creado_en",
                schema: "desempeno",
                table: "ciclos",
                columns: new[] { "empresa_id", "creado_en" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluaciones_documento",
                schema: "desempeno",
                table: "evaluaciones",
                column: "documento");

            migrationBuilder.CreateIndex(
                name: "ix_evaluaciones_empresa_id_ciclo_id_nombre",
                schema: "desempeno",
                table: "evaluaciones",
                columns: new[] { "empresa_id", "ciclo_id", "nombre" });

            migrationBuilder.CreateIndex(
                name: "ix_evaluaciones_empresa_id_empleado_id",
                schema: "desempeno",
                table: "evaluaciones",
                columns: new[] { "empresa_id", "empleado_id" });

            migrationBuilder.CreateIndex(
                name: "ix_inbox_messages_processed_on",
                schema: "desempeno",
                table: "inbox_messages",
                column: "processed_on");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_on",
                schema: "desempeno",
                table: "outbox_messages",
                column: "processed_on");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ciclos",
                schema: "desempeno");

            migrationBuilder.DropTable(
                name: "evaluaciones",
                schema: "desempeno");

            migrationBuilder.DropTable(
                name: "inbox_messages",
                schema: "desempeno");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "desempeno");
        }
    }
}
