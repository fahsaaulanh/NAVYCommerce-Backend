using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NAVYCommerce.Infrastructure.Migrations
{
    public partial class FixVariantAttributesColumnType : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DO $migration$
                DECLARE
                    current_type text;
                BEGIN
                    SELECT udt_name
                    INTO current_type
                    FROM information_schema.columns
                    WHERE table_schema = 'public'
                      AND table_name = 'product_variants'
                      AND column_name = 'VariantAttributes';

                    IF current_type = 'hstore' THEN
                        ALTER TABLE public.product_variants
                            ALTER COLUMN "VariantAttributes"
                            DROP DEFAULT;

                        ALTER TABLE public.product_variants
                            ALTER COLUMN "VariantAttributes"
                            TYPE jsonb
                            USING hstore_to_jsonb("VariantAttributes");

                    ELSIF current_type = 'jsonb' THEN
                        NULL;

                    ELSE
                        RAISE EXCEPTION
                            'Tipe VariantAttributes tidak sesuai: %',
                            current_type;
                    END IF;
                END;
                $migration$;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            throw new NotSupportedException(
                "Rollback perbaikan tipe kolom ini harus direncanakan "
                + "secara manual agar database tidak kembali "
                + "bertentangan dengan mapping jsonb.");
        }
    }
}