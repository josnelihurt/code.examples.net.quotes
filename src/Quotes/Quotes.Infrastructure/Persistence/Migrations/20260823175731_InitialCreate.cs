using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Quotes.Infrastructure.Persistence.Migrations;

/// <inheritdoc />
public partial class InitialCreate : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "quotes",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Text = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                Author = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                NormalizedFingerprint = table.Column<string>(type: "character varying(280)", maxLength: 280, nullable: false),
                CreatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_quotes", x => x.Id);
            });

        migrationBuilder.InsertData(
            table: "quotes",
            columns: new[] { "Id", "Author", "CreatedAtUtc", "NormalizedFingerprint", "Text" },
            values: new object[,]
            {
                { "1", "Leonardo da Vinci", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "simplicity is the ultimate sophistication", "Simplicity is the ultimate sophistication." },
                { "2", "Cory House", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "code is like humor when you have to explain it it s bad", "Code is like humor. When you have to explain it, it's bad." },
                { "3", "John Johnson", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "first solve the problem then write the code", "First, solve the problem. Then, write the code." },
                { "4", "Oscar Wilde", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "experience is the name everyone gives to their mistakes", "Experience is the name everyone gives to their mistakes." },
                { "5", "Robert C. Martin", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "the only way to go fast is to go well", "The only way to go fast is to go well." },
                { "6", "Kent Beck", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "make it work make it right make it fast", "Make it work, make it right, make it fast." },
                { "7", "Harold Abelson", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "programs must be written for people to read", "Programs must be written for people to read." },
                { "8", "Linus Torvalds", new DateTimeOffset(new DateTime(2024, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "talk is cheap show me the code", "Talk is cheap. Show me the code." }
            });

        migrationBuilder.CreateIndex(
            name: "IX_quotes_NormalizedFingerprint",
            table: "quotes",
            column: "NormalizedFingerprint",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "quotes");
    }
}
