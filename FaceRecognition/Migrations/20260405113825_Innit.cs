using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FaceRecognition.Migrations;

/// <inheritdoc />
public partial class Innit : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "FaceEmbeddings",
            columns: table => new
            {
                EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                EmployeeName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                EmbeddingBytes = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_FaceEmbeddings", x => x.EmployeeId);
            });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "FaceEmbeddings");
    }
}
