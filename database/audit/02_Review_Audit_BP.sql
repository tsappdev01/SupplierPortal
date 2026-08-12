/**********************************************************************************
  02_Review_Audit_BP.sql   (SupplierPortal / database [BP])
  ------------------------------------------------------------------------------
  Purpose : Read the audit trail produced by 01_Deploy_Audit_BP.sql and produce
            the evidence an ITGC reviewer / KPMG asks for. Closes Gap 2 (periodic,
            evidenced log review) when run on a schedule by someone independent of
            the DBA team, with the output retained.

  Reader  : sys.fn_get_audit_file requires CONTROL SERVER (or a role granted read
            access). Grant the reviewer read access WITHOUT DBA rights.

  action_id cheat-sheet: IN=insert UP=update DL=delete SL=select EX=execute
                         CR=create AL=alter DR=drop  LGIS=login ok  LGIF=login fail
**********************************************************************************/

DECLARE @AuditPath NVARCHAR(260) = N'D:\SQLAudit\*.sqlaudit';

/* The connection identity(ies) that legitimate SupplierPortal traffic uses.
   Set  Application Name=SupplierPortal  in the app's connection string, then keep
   this list in sync. Everything NOT in this list is treated as "outside the app". */
-- known app application_name(s): 'SupplierPortal'
-- known app host/IP           : the web server only


/*==============================================================================
  1. SOMEONE EDITED DATA DIRECTLY (e.g. via SSMS) - the headline query
  ------------------------------------------------------------------------------
  Every INSERT/UPDATE/DELETE against [BP] that did NOT come through the
  application identity. application_name is client-supplied and spoofable, so
  this is a "flag for review" signal, not proof - corroborate with client_ip
  (should be the web server for real app traffic) and the statement text.
==============================================================================*/
SELECT
      f.event_time
    , f.server_principal_name          AS login_name
    , f.database_name + '.' + f.schema_name + '.' + f.object_name AS target
    , CASE f.action_id WHEN 'IN' THEN 'INSERT' WHEN 'UP' THEN 'UPDATE'
                       WHEN 'DL' THEN 'DELETE' ELSE CAST(f.action_id AS VARCHAR(10)) END AS operation
    , f.affected_rows
    , f.statement
    , f.client_ip
    , f.application_name
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.database_name = 'BP'
  AND f.action_id IN ('IN','UP','DL')
  AND f.application_name NOT IN ('SupplierPortal')     -- <<< your app identity(ies)
  AND f.application_name NOT LIKE '%SqlClient Data Provider%'   -- default EF/.NET name if not overridden
ORDER BY f.event_time DESC;


/*==============================================================================
  2. ACTIVITY THROUGH SSMS / ADO tools  (descriptive - see spoofing caution)
==============================================================================*/
SELECT  f.event_time, f.server_principal_name, f.database_name, f.object_name,
        f.action_id, f.affected_rows, f.statement, f.client_ip, f.application_name
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.application_name LIKE '%Management Studio%'
   OR f.application_name LIKE '%Azure Data Studio%'
   OR f.application_name LIKE '%sqlcmd%'
ORDER BY f.event_time DESC;


/*==============================================================================
  3. ALL DATA CHANGES ON [BP] - the full change trail (last 24h; widen as needed)
==============================================================================*/
SELECT  f.event_time, f.server_principal_name AS login_name, f.database_principal_name AS db_user,
        f.schema_name, f.object_name,
        CASE f.action_id WHEN 'IN' THEN 'INSERT' WHEN 'UP' THEN 'UPDATE'
                         WHEN 'DL' THEN 'DELETE' ELSE CAST(f.action_id AS VARCHAR(10)) END AS operation,
        f.affected_rows, f.succeeded, f.statement, f.client_ip, f.application_name
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.database_name = 'BP'
  AND f.action_id IN ('IN','UP','DL')
  AND f.event_time >= DATEADD(DAY, -1, GETUTCDATE())
ORDER BY f.event_time DESC;


/*==============================================================================
  4. SQL LOGIN / ROLE / PERMISSION CHANGES  (KPMG Gap 1 - reconcile to authorisation)
  ------------------------------------------------------------------------------
  NOTE: these are SQL Server logins/users, NOT SupplierPortal app users. App user
  and role changes are INSERT/UPDATE on dbo.[Users] and appear in queries 1/3
  above (as bpuser / the app identity).
==============================================================================*/
SELECT  f.event_time, f.server_principal_name AS granted_by,
        f.target_server_principal_name   AS target_login,
        f.target_database_principal_name AS target_user,
        f.database_name, f.statement, f.succeeded, f.client_ip
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.statement LIKE 'GRANT %'  OR f.statement LIKE 'REVOKE %' OR f.statement LIKE 'DENY %'
   OR f.statement LIKE '%CREATE LOGIN%' OR f.statement LIKE '%ALTER LOGIN%' OR f.statement LIKE '%DROP LOGIN%'
   OR f.statement LIKE '%CREATE USER%'  OR f.statement LIKE '%ALTER USER%'  OR f.statement LIKE '%DROP USER%'
   OR f.statement LIKE '%ALTER ROLE%ADD MEMBER%' OR f.statement LIKE '%ALTER ROLE%DROP MEMBER%'
   OR f.statement LIKE '%sp_addsrvrolemember%'   OR f.statement LIKE '%sp_droprolemember%'
ORDER BY f.event_time DESC;


/*==============================================================================
  5. TAMPER DETECTION - changes to the audit itself  (alert to Security in real time)
==============================================================================*/
SELECT  f.event_time, f.server_principal_name, f.statement, f.succeeded, f.client_ip, f.application_name
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.statement LIKE '%SERVER AUDIT%'
   OR f.statement LIKE '%AUDIT SPECIFICATION%'
   OR f.statement LIKE '%sp_configure%'
   OR f.class_type IN ('SA','SG')
ORDER BY f.event_time DESC;


/*==============================================================================
  6. LOGIN ACTIVITY - "last login" + repeated failures (feeds access review)
==============================================================================*/
SELECT  f.server_principal_name, MAX(f.event_time) AS last_successful_login,
        COUNT(*) AS login_count, COUNT(DISTINCT f.client_ip) AS distinct_source_ips
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.action_id = 'LGIS'
GROUP BY f.server_principal_name
ORDER BY last_successful_login DESC;

SELECT  f.server_principal_name, f.client_ip, COUNT(*) AS failed_attempts,
        MIN(f.event_time) AS first_attempt, MAX(f.event_time) AS last_attempt
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.action_id = 'LGIF'
GROUP BY f.server_principal_name, f.client_ip
HAVING COUNT(*) > 5
ORDER BY failed_attempts DESC;


/*==============================================================================
  7. HIGH-IMPACT CHANGES - large or unbounded UPDATE/DELETE on [BP]
  ------------------------------------------------------------------------------
  An UPDATE/DELETE with no WHERE against Suppliers / Banks / Users is the event
  most worth catching quickly.
==============================================================================*/
SELECT  f.event_time, f.server_principal_name,
        f.schema_name + '.' + f.object_name AS target,
        f.action_id, f.affected_rows, f.statement, f.application_name, f.client_ip
FROM sys.fn_get_audit_file(@AuditPath, DEFAULT, DEFAULT) AS f
WHERE f.database_name = 'BP'
  AND f.action_id IN ('UP','DL')
  AND (f.affected_rows > 100 OR (f.statement NOT LIKE '%WHERE%' AND f.statement IS NOT NULL))
ORDER BY f.affected_rows DESC;
