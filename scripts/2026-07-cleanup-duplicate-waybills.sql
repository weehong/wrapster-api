-- =============================================================================
-- Cleanup: duplicate waybill numbers across tenants
--
-- MUST be run (and duplicates resolved) BEFORE applying the
-- MakeWaybillNumberGloballyUnique migration — creating the global unique index
-- fails with SQLSTATE 23505 while duplicates remain.
--
-- Run each section separately. Sections 3 and 4 are templates: review the
-- report output first, fill in the Ids, and keep this script plus the query
-- output as the audit record — raw SQL bypasses the EF audit interceptors,
-- so no AuditLog / stock-movement entries are produced.
--
-- Stock caveats:
--   * WaybillItems rows are removed automatically
--     (FK_WaybillItems_Waybills_WaybillId is ON DELETE CASCADE).
--   * Deleting a waybill row does NOT adjust product stock:
--       - HandedOff rows already consumed stock (StockQuantity was decremented
--         and the reservation released at hand-off). Deleting the row does not
--         restore stock — any correction is a separate, per-row manual decision.
--       - Draft/Packed rows still hold a reservation
--         (Products.ReservedQuantity); deleting them via SQL strands that
--         reservation, which must be released manually.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Report: exact duplicate waybill numbers (any tenant)
-- -----------------------------------------------------------------------------
SELECT w."WaybillNumber",
       w."Id",
       w."TenantId",
       w."Status",
       w."PackagingDate",
       w."CreatedAt",
       w."CreatedBy"
FROM "Waybills" w
WHERE w."WaybillNumber" IN (
    SELECT "WaybillNumber"
    FROM "Waybills"
    GROUP BY "WaybillNumber"
    HAVING COUNT(*) > 1)
ORDER BY w."WaybillNumber", w."CreatedAt";

-- -----------------------------------------------------------------------------
-- 2. Report: whitespace-padded numbers and trim-variant collisions
--    (rows that would collide once numbers are trimmed)
-- -----------------------------------------------------------------------------
SELECT '[' || w."WaybillNumber" || ']' AS padded_number,
       w."Id",
       w."TenantId",
       w."Status",
       w."CreatedAt"
FROM "Waybills" w
WHERE w."WaybillNumber" <> btrim(w."WaybillNumber")
ORDER BY w."CreatedAt";

SELECT btrim(w."WaybillNumber") AS trimmed_number,
       w."Id",
       w."TenantId",
       w."Status",
       w."CreatedAt"
FROM "Waybills" w
WHERE btrim(w."WaybillNumber") IN (
    SELECT btrim("WaybillNumber")
    FROM "Waybills"
    GROUP BY btrim("WaybillNumber")
    HAVING COUNT(*) > 1)
ORDER BY trimmed_number, w."CreatedAt";

-- -----------------------------------------------------------------------------
-- 3. Template: normalize padded numbers.
--    Only run after section 2 confirms trimming creates no new collision
--    (resolve any trim-variant collision via section 4 first).
-- -----------------------------------------------------------------------------
-- BEGIN;
-- UPDATE "Waybills"
-- SET "WaybillNumber" = btrim("WaybillNumber")
-- WHERE "WaybillNumber" <> btrim("WaybillNumber");
-- -- expect: UPDATE <count from section 2's first query>
-- COMMIT;

-- -----------------------------------------------------------------------------
-- 4. Template: delete the mistaken duplicate rows.
--    Review section 1: for each number keep the legitimate row and list the
--    mistaken row Ids below. Known duplicate numbers as of 2026-07-07:
--    78000039, SPXMY063652561615, SB (verify against the live report output).
-- -----------------------------------------------------------------------------
-- BEGIN;
-- DELETE FROM "Waybills"
-- WHERE "Id" IN (
--     -- '00000000-0000-0000-0000-000000000000'::uuid,
--     -- '00000000-0000-0000-0000-000000000000'::uuid
-- );
-- -- expect: DELETE <number of Ids listed>
-- COMMIT;

-- -----------------------------------------------------------------------------
-- 5. Post-check: must return zero rows before applying the migration.
-- -----------------------------------------------------------------------------
SELECT "WaybillNumber", COUNT(*)
FROM "Waybills"
GROUP BY "WaybillNumber"
HAVING COUNT(*) > 1;
