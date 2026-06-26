using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wrapsfer.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureEntitlementActiveWindowExclusion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Guarantee at most one *active* entitlement per (tenant, feature) for any given validity
            // window. This is the database-level backstop for the stock-report checkout race: two
            // concurrent checkouts both pass the GetActiveAsync pre-check and create PendingPayment
            // rows, but only one can ever be activated for the same month — the second activation
            // violates this constraint instead of granting overlapping paid access. Non-overlapping
            // windows (different months) and Canceled rows are unaffected, so re-purchasing next month
            // or after a refund still works. Requires btree_gist for the equality columns in a GiST
            // exclusion constraint.
            // Fail fast with an actionable message if the table already violates the invariant, rather
            // than letting ADD CONSTRAINT surface a raw exclusion-violation that names no rows. Active
            // overlaps for the same tenant/feature must be reconciled (cancel the duplicates) first.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM "FeatureEntitlements" a
                        JOIN "FeatureEntitlements" b
                          ON a."TenantId" = b."TenantId"
                         AND a."Feature" = b."Feature"
                         AND a."Id" <> b."Id"
                        WHERE a."Status" = 'Active'
                          AND b."Status" = 'Active'
                          AND tstzrange(a."ValidFromUtc", a."ValidToUtc")
                              && tstzrange(b."ValidFromUtc", b."ValidToUtc")
                    ) THEN
                        RAISE EXCEPTION 'Cannot add EX_FeatureEntitlements_ActiveWindow_NoOverlap: overlapping Active entitlements exist for the same tenant/feature. Cancel the duplicate entitlements before applying this migration.';
                    END IF;
                END $$;
                """);

            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(
                """
                ALTER TABLE "FeatureEntitlements"
                ADD CONSTRAINT "EX_FeatureEntitlements_ActiveWindow_NoOverlap"
                EXCLUDE USING gist (
                    "TenantId" WITH =,
                    "Feature" WITH =,
                    tstzrange("ValidFromUtc", "ValidToUtc") WITH &&
                ) WHERE ("Status" = 'Active');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                ALTER TABLE "FeatureEntitlements"
                DROP CONSTRAINT IF EXISTS "EX_FeatureEntitlements_ActiveWindow_NoOverlap";
                """);

            // btree_gist is intentionally left installed: other objects may depend on it and dropping
            // an extension that was already present would be surprising on rollback.
        }
    }
}
