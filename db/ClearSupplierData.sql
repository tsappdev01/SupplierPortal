/*───────────────────────────────────────────────────────────────────────────
  Clear ALL supplier data and history — Supplier Portal
───────────────────────────────────────────────────────────────────────────
  Deletes every supplier record and its history:
      • Suppliers
      • SupplierPartners         (partners read from trade licenses)
      • SupplierDocuments        (uploaded-document metadata)
      • SupplierFieldChanges     (the change log / review history)
      • SupplierAccessTokens     (self-service links + OTP / email history)

  Does NOT touch (master/config data is preserved):
      • LookupValues (LOVs incl. workflow statuses), Banks / BankBranches,
        BusinessUnits, Users, SupplierFieldSettings, AuditLogs

  Notes:
    - Runs inside a transaction: if anything fails, nothing is deleted.
    - Deletes children before parents so foreign keys never block the purge.
      (SupplierFieldChanges must go before SupplierAccessTokens because its FK
       to SupplierAccessTokens is NO ACTION, not cascade.)
    - Physical uploaded files under  <app content root>\doc\supplier\  are NOT
      removed by this script (SQL can't reach the file system). They become
      harmless orphans; delete that folder's contents separately if desired.
    - Audit-log rows are intentionally KEPT. To also clear supplier audit
      entries, run:  DELETE FROM dbo.AuditLogs WHERE EntityType = 'Supplier';

  HOW TO RUN
    1. Point your query window at the Supplier Portal database (e.g. BP)
       — uncomment the USE line below, or just select the DB in SSMS.
    2. Execute. Review the before/after counts it prints.

  ⚠  This is irreversible. Back up the database first if in doubt.
───────────────────────────────────────────────────────────────────────────*/

-- USE [BP];   -- <-- uncomment and set to your database name, or select it in SSMS
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;   -- any error aborts the whole transaction

BEGIN TRY
    BEGIN TRANSACTION;

    PRINT '--- Row counts BEFORE ---';
    SELECT
        (SELECT COUNT(*) FROM dbo.Suppliers)            AS Suppliers,
        (SELECT COUNT(*) FROM dbo.SupplierPartners)     AS SupplierPartners,
        (SELECT COUNT(*) FROM dbo.SupplierDocuments)    AS SupplierDocuments,
        (SELECT COUNT(*) FROM dbo.SupplierFieldChanges) AS SupplierFieldChanges,
        (SELECT COUNT(*) FROM dbo.SupplierAccessTokens) AS SupplierAccessTokens;

    -- Delete in FK-safe order (children first).
    DELETE FROM dbo.SupplierFieldChanges;
    DELETE FROM dbo.SupplierAccessTokens;
    DELETE FROM dbo.SupplierDocuments;
    DELETE FROM dbo.SupplierPartners;
    DELETE FROM dbo.Suppliers;

    -- Reset identity seeds so new records number from 1 again (optional but tidy).
    -- Safe to keep: if a table is empty, the next inserted Id becomes 1.
    DBCC CHECKIDENT ('dbo.SupplierFieldChanges', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.SupplierAccessTokens', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.SupplierDocuments',    RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.SupplierPartners',     RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Suppliers',            RESEED, 0) WITH NO_INFOMSGS;

    PRINT '--- Row counts AFTER (all should be 0) ---';
    SELECT
        (SELECT COUNT(*) FROM dbo.Suppliers)            AS Suppliers,
        (SELECT COUNT(*) FROM dbo.SupplierPartners)     AS SupplierPartners,
        (SELECT COUNT(*) FROM dbo.SupplierDocuments)    AS SupplierDocuments,
        (SELECT COUNT(*) FROM dbo.SupplierFieldChanges) AS SupplierFieldChanges,
        (SELECT COUNT(*) FROM dbo.SupplierAccessTokens) AS SupplierAccessTokens;

    COMMIT TRANSACTION;
    PRINT 'Supplier data and history cleared successfully.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    PRINT 'ERROR — nothing was deleted. Details:';
    THROW;
END CATCH;
GO
