using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAVYCommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeUserEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM users
                        GROUP BY lower(btrim(""Email""))
                        HAVING COUNT(*) > 1
                    ) THEN
                        RAISE EXCEPTION
                            'Normalisasi email dibatalkan: terdapat email duplikat setelah normalisasi.';
                    END IF;
                END $$;
            ");

            migrationBuilder.Sql(@"
                UPDATE users
                SET ""Email"" = lower(btrim(""Email""))
                WHERE ""Email"" IS DISTINCT FROM lower(btrim(""Email""));
            ");

            migrationBuilder.AddCheckConstraint(
                name: "CK_users_Email_Normalized",
                table: "users",
                sql: "\"Email\" = lower(btrim(\"Email\"))");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_users_Email_Normalized",
                table: "users");
        }
    }
}
