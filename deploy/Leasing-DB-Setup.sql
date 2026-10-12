/* ===========================================================================
   Landlord (Leasing) portal — separate-database setup
   ---------------------------------------------------------------------------
   The standalone Landlord app (SupplierPortal.Leasing.Web) runs against its OWN
   database. EF migrations create the shared framework tables (Users, BusinessUnits,
   Departments, LookupValues, AuditLogs, UserBusinessUnits) and the leasing.* tables
   automatically on first start.

   TWO objects are NOT created by migrations because they live in / mirror the main
   database and are mapped read/externally:

     1. dbo.Property            — read-only land/property view (mapped via ToView).
     2. dbo.MaintainTenantUAEPassEmails — shared table written on approval.

   Run this script ONCE against the Landlord database after it is created, editing
   @MainDb to the database that owns the live Property view and the UAE-Pass table.
   Requires the SQL login to have access to both databases (same instance), or set
   up a linked server and adjust the 4-part names.
   =========================================================================== */

DECLARE @MainDb sysname = N'BP';   -- <-- the main Supplier/Customer database on this instance

/* 1) Property: expose the main DB's read-only Property view inside the Landlord DB.
      A SYNONYM keeps it always-current (the app only ever reads it). */
IF OBJECT_ID(N'dbo.Property', N'SN') IS NOT NULL DROP SYNONYM dbo.Property;
IF OBJECT_ID(N'dbo.Property', N'V')  IS NOT NULL DROP VIEW    dbo.Property;
DECLARE @sql nvarchar(max) = N'CREATE SYNONYM dbo.Property FOR [' + @MainDb + N'].dbo.Property;';
EXEC sp_executesql @sql;

/* 2) MaintainTenantUAEPassEmails: the Landlord app writes signatory emails here on
      approval. Point at the main DB copy via a synonym so there is a single source
      of truth. (If you prefer a local table instead, create it here and remove the
      synonym — the app only inserts/updates rows keyed by the source landlord id.) */
IF OBJECT_ID(N'dbo.MaintainTenantUAEPassEmails', N'SN') IS NOT NULL DROP SYNONYM dbo.MaintainTenantUAEPassEmails;
IF OBJECT_ID(N'dbo.MaintainTenantUAEPassEmails', N'U')  IS NULL
BEGIN
    SET @sql = N'CREATE SYNONYM dbo.MaintainTenantUAEPassEmails FOR [' + @MainDb + N'].dbo.MaintainTenantUAEPassEmails;';
    EXEC sp_executesql @sql;
END

/* Reference data (Business Units, Lookups) and the break-glass admin are seeded by
   the app on first start (LeasingDbSeeder). Maintain them in this database going
   forward, independently of the main portal. */
GO
