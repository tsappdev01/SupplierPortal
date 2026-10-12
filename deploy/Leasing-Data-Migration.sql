/* ===========================================================================
   Landlord (Leasing) portal — one-time data migration  [source: BP]
   ---------------------------------------------------------------------------
   Copies the existing Landlord data and the shared reference/identity data from
   the main database (BP) into the NEW standalone Landlord database.

   RUN THIS:
     * AFTER the Landlord app has started once (so its schema exists: the shared
       framework tables + the leasing.* tables are created by EF migrations), and
       AFTER running Leasing-DB-Setup.sql (Property / MaintainTenantUAEPassEmails
       synonyms).
     * IN THE CONTEXT OF THE NEW LANDLORD DATABASE (the target). e.g.
         USE [BP_Landlord];  -- your Landlord DB name
         :r Leasing-Data-Migration.sql        (or just run the body below)

   SAFE TO REVIEW FIRST: it runs in a transaction, aborts if the target already
   holds data (set @Force = 1 to reload into an empty-or-not target), preserves
   identity values, and copies only columns that exist in BOTH databases.

   The source login must be able to read [BP] on the same instance (or set up a
   linked server and change @MainDb to the 4-part prefix).
   =========================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @MainDb  sysname = N'BP';   -- source database
DECLARE @Force   bit     = 0;       -- set to 1 to load even if the target is not empty

/* Ordered list of tables to copy (FK-parents first). schema + table.
   Shared/identity tables, then the leasing.* registration tables.
   AuditLogs is intentionally omitted (historical audit stays in BP; the new app
   starts a fresh audit trail). Add it here if you want the history copied. */
DECLARE @Tables TABLE (Seq int IDENTITY(1,1) PRIMARY KEY, Sch sysname, Tbl sysname);
INSERT INTO @Tables (Sch, Tbl) VALUES
    (N'dbo',     N'Departments'),
    (N'dbo',     N'BusinessUnits'),
    (N'dbo',     N'LookupValues'),
    (N'dbo',     N'Users'),
    (N'dbo',     N'UserBusinessUnits'),
    (N'leasing', N'Landlords'),
    (N'leasing', N'LandlordPremises'),
    (N'leasing', N'LandlordContacts'),
    (N'leasing', N'LandlordSignatories'),
    (N'leasing', N'LandlordDocuments'),
    (N'leasing', N'LandlordComments'),
    (N'leasing', N'LandlordFieldChanges'),
    (N'leasing', N'LandlordEvents'),
    (N'leasing', N'LandlordAccessTokens'),
    (N'leasing', N'LandlordFieldConfigs'),
    (N'leasing', N'LandlordTabSettings');

/* ---- Pre-flight: make sure the target schema exists and (unless forced) is empty ---- */
DECLARE @Sch sysname, @Tbl sysname, @Seq int, @cnt bigint, @nonEmpty int = 0;
DECLARE cur CURSOR LOCAL FAST_FORWARD FOR SELECT Seq, Sch, Tbl FROM @Tables ORDER BY Seq;
OPEN cur; FETCH NEXT FROM cur INTO @Seq, @Sch, @Tbl;
WHILE @@FETCH_STATUS = 0
BEGIN
    IF OBJECT_ID(QUOTENAME(@Sch) + N'.' + QUOTENAME(@Tbl), N'U') IS NULL
    BEGIN
        RAISERROR('Target table %s.%s does not exist. Start the Landlord app once so EF creates the schema, then re-run.', 16, 1, @Sch, @Tbl);
        CLOSE cur; DEALLOCATE cur; RETURN;
    END
    DECLARE @sqlCnt nvarchar(max) = N'SELECT @c = COUNT_BIG(*) FROM ' + QUOTENAME(@Sch) + N'.' + QUOTENAME(@Tbl) + N';';
    EXEC sp_executesql @sqlCnt, N'@c bigint OUTPUT', @c = @cnt OUTPUT;
    IF @cnt > 0 SET @nonEmpty = @nonEmpty + 1;
    FETCH NEXT FROM cur INTO @Seq, @Sch, @Tbl;
END
CLOSE cur; DEALLOCATE cur;

IF @nonEmpty > 0 AND @Force = 0
BEGIN
    RAISERROR('Target already contains data in %d of the listed tables. Review, then set @Force = 1 to proceed.', 16, 1, @nonEmpty);
    RETURN;
END

/* ---- Copy ---- */
BEGIN TRY
    BEGIN TRAN;

    -- Disable all FK constraints during the load so table order never blocks us.
    EXEC sp_MSforeachtable @command1 = 'ALTER TABLE ? NOCHECK CONSTRAINT ALL';

    DECLARE cur2 CURSOR LOCAL FAST_FORWARD FOR SELECT Seq, Sch, Tbl FROM @Tables ORDER BY Seq;
    OPEN cur2; FETCH NEXT FROM cur2 INTO @Seq, @Sch, @Tbl;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        DECLARE @full nvarchar(512) = QUOTENAME(@Sch) + N'.' + QUOTENAME(@Tbl);
        DECLARE @src  nvarchar(600) = QUOTENAME(@MainDb) + N'.' + QUOTENAME(@Sch) + N'.' + QUOTENAME(@Tbl);

        -- Column list = columns present in BOTH databases (target AND source), excluding computed columns.
        DECLARE @cols nvarchar(max);
        DECLARE @colSql nvarchar(max) = N'
            SELECT @out = STRING_AGG(QUOTENAME(tc.name), '', '') WITHIN GROUP (ORDER BY tc.column_id)
            FROM sys.columns tc
            JOIN sys.objects tt ON tt.object_id = tc.object_id
            JOIN sys.schemas ts ON ts.schema_id = tt.schema_id
            WHERE ts.name = @s AND tt.name = @t AND tc.is_computed = 0
              AND EXISTS (
                  SELECT 1 FROM ' + QUOTENAME(@MainDb) + N'.sys.columns sc
                  JOIN ' + QUOTENAME(@MainDb) + N'.sys.objects so ON so.object_id = sc.object_id
                  JOIN ' + QUOTENAME(@MainDb) + N'.sys.schemas ss ON ss.schema_id = so.schema_id
                  WHERE ss.name = @s AND so.name = @t AND sc.name = tc.name);';
        EXEC sp_executesql @colSql, N'@s sysname, @t sysname, @out nvarchar(max) OUTPUT',
             @s = @Sch, @t = @Tbl, @out = @cols OUTPUT;

        IF @cols IS NULL
        BEGIN
            RAISERROR('No common columns found for %s.%s between the target and [%s] — skipping.', 10, 1, @Sch, @Tbl, @MainDb);
            FETCH NEXT FROM cur2 INTO @Seq, @Sch, @Tbl;
            CONTINUE;
        END

        -- Does the target have an identity column? (then wrap with IDENTITY_INSERT)
        DECLARE @hasIdentity bit = CASE WHEN EXISTS (
            SELECT 1 FROM sys.identity_columns ic
            JOIN sys.tables t2 ON t2.object_id = ic.object_id
            JOIN sys.schemas s2 ON s2.schema_id = t2.schema_id
            WHERE s2.name = @Sch AND t2.name = @Tbl) THEN 1 ELSE 0 END;

        DECLARE @sql nvarchar(max) =
            CASE WHEN @hasIdentity = 1 THEN N'SET IDENTITY_INSERT ' + @full + N' ON;' + CHAR(10) ELSE N'' END +
            N'INSERT INTO ' + @full + N' (' + @cols + N') SELECT ' + @cols + N' FROM ' + @src + N';' + CHAR(10) +
            CASE WHEN @hasIdentity = 1 THEN N'SET IDENTITY_INSERT ' + @full + N' OFF;' ELSE N'' END;

        PRINT N'Copying ' + @src + N' -> ' + @full;
        EXEC sp_executesql @sql;
        PRINT N'  rows: ' + CAST(@@ROWCOUNT AS nvarchar(20));

        FETCH NEXT FROM cur2 INTO @Seq, @Sch, @Tbl;
    END
    CLOSE cur2; DEALLOCATE cur2;

    -- Re-enable + validate the FK constraints (WITH CHECK proves the copied data is consistent).
    EXEC sp_MSforeachtable @command1 = 'ALTER TABLE ? WITH CHECK CHECK CONSTRAINT ALL';

    COMMIT;
    PRINT N'Landlord data migration from [' + @MainDb + N'] completed.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK;
    PRINT N'Migration FAILED and was rolled back:';
    THROW;
END CATCH
GO
