/*───────────────────────────────────────────────────────────────────────────
  FULL RESET — Supplier Portal  ("clear all data and start from scratch")
───────────────────────────────────────────────────────────────────────────
  Deletes ALL transactional data AND accounts/history:
      • SupplierFieldChanges     (change log / review history)
      • SupplierAccessTokens      (self-service links + OTP / email history)
      • SupplierDocuments         (uploaded-document metadata)
      • SupplierPartners          (partners read from trade licenses)
      • Suppliers
      • AuditLogs                 (all audit history)
      • UserBusinessUnits         (user↔business-unit links)
      • Users                     (all accounts, incl. the seeded default admin)

  KEEPS the seeded master/config data so the app still works out of the box:
      • LookupValues (LOVs incl. workflow statuses)
      • Banks / BankBranches
      • BusinessUnits
      • SupplierFieldSettings

  AFTER RUNNING
    - Restart the Supplier Portal app. On start-up DbSeeder re-creates the
      default administrator (admin@supplierportal.local / Admin@12345, or your
      DefaultAdmin override) because no other admin exists. SSO bootstrap admins
      (Auth:BootstrapAdminEmails) are re-provisioned on their first SSO login.
    - Physical uploaded files under  <app content root>\doc\supplier\  are NOT
      removed by SQL. Delete them separately, e.g. in PowerShell on the server:

          Remove-Item -Path 'C:\inetpub\SupplierPortal\doc\supplier\*' -Recurse -Force
          # adjust the path to the site's content root

  NOTES
    - Runs inside a transaction: if anything fails, nothing is deleted.
    - Children are deleted before parents so foreign keys never block the purge.
      (SupplierFieldChanges must go before SupplierAccessTokens because its FK to
       SupplierAccessTokens is NO ACTION, not cascade.)

  ⚠  IRREVERSIBLE. Back up the database first if in any doubt.

  HOW TO RUN
    1. Point the query window at the Supplier Portal database (e.g. BP) —
       uncomment the USE line below, or just select the DB in SSMS.
    2. Execute. Review the before/after counts it prints.
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
        (SELECT COUNT(*) FROM dbo.SupplierAccessTokens) AS SupplierAccessTokens,
        (SELECT COUNT(*) FROM dbo.AuditLogs)            AS AuditLogs,
        (SELECT COUNT(*) FROM dbo.Users)                AS Users;

    -- Delete in FK-safe order (children first).
    DELETE FROM dbo.SupplierFieldChanges;
    DELETE FROM dbo.SupplierAccessTokens;
    DELETE FROM dbo.SupplierDocuments;
    DELETE FROM dbo.SupplierPartners;
    DELETE FROM dbo.Suppliers;
    DELETE FROM dbo.AuditLogs;
    DELETE FROM dbo.UserBusinessUnits;   -- user↔BU join (also cascades from Users, cleared explicitly)
    DELETE FROM dbo.Users;

    -- Reset identity seeds so new records number from 1 again.
    DBCC CHECKIDENT ('dbo.SupplierFieldChanges', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.SupplierAccessTokens', RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.SupplierDocuments',    RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.SupplierPartners',     RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Suppliers',            RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.AuditLogs',            RESEED, 0) WITH NO_INFOMSGS;
    DBCC CHECKIDENT ('dbo.Users',                RESEED, 0) WITH NO_INFOMSGS;

    PRINT '--- Row counts AFTER (all should be 0) ---';
    SELECT
        (SELECT COUNT(*) FROM dbo.Suppliers)            AS Suppliers,
        (SELECT COUNT(*) FROM dbo.SupplierPartners)     AS SupplierPartners,
        (SELECT COUNT(*) FROM dbo.SupplierDocuments)    AS SupplierDocuments,
        (SELECT COUNT(*) FROM dbo.SupplierFieldChanges) AS SupplierFieldChanges,
        (SELECT COUNT(*) FROM dbo.SupplierAccessTokens) AS SupplierAccessTokens,
        (SELECT COUNT(*) FROM dbo.AuditLogs)            AS AuditLogs,
        (SELECT COUNT(*) FROM dbo.Users)                AS Users;

    PRINT '--- Master/config data KEPT (non-zero expected) ---';
    SELECT
        (SELECT COUNT(*) FROM dbo.LookupValues)         AS LookupValues,
        (SELECT COUNT(*) FROM dbo.Banks)                AS Banks,
        (SELECT COUNT(*) FROM dbo.BankBranches)         AS BankBranches,
        (SELECT COUNT(*) FROM dbo.BusinessUnits)        AS BusinessUnits,
        (SELECT COUNT(*) FROM dbo.SupplierFieldSettings) AS SupplierFieldSettings;

    COMMIT TRANSACTION;
    PRINT 'Full reset complete. Restart the app so the default administrator is re-seeded.';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;
    PRINT 'ERROR — nothing was deleted. Details:';
    THROW;
END CATCH;
GO
