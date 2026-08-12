/**********************************************************************************
  01_Deploy_Audit_BP.sql   (SupplierPortal / database [BP])
  ------------------------------------------------------------------------------
  Purpose : Enable auditing on the SQL Server instance and the [BP] database to
            close the KPMG ITGC observation for SupplierPortal:

              Gap 1  Incomplete audit logging - user/role, access modifications,
                     account changes, disablement/removal and deletion not captured.
              Gap 2  No systematic, evidenced, periodic log review.

  Scope of THIS script (deliberately excludes SELECT auditing to protect
  application performance - see the note in STEP 3):

     LAYER 1  "SQL logins level"  - who connects to SQL Server and every change
              to SQL logins, server roles and server-level permissions.
     LAYER 2  "data edited via SSMS" - every INSERT / UPDATE / DELETE against
              [BP], plus database user / role / permission changes.

  READ FIRST - two different notions of "user" in this system
  ------------------------------------------------------------------------------
  SupplierPortal authenticates its OWN users (cookie auth) as rows in the [Users]
  table and connects to SQL as a SINGLE shared login (bpuser). Therefore:

    - Changes to APPLICATION users/roles (adding a user, changing User Type in the
      admin UI) are INSERT/UPDATE on dbo.[Users] performed AS bpuser. They are
      caught by LAYER 2 (DML), and will look like normal application traffic.

    - Changes to SQL LOGINS (a DBA's login, sa, bpuser itself, server roles) are
      caught by LAYER 1.

    - A person editing data directly in SSMS also usually connects AS bpuser, so
      you cannot tell them apart from the app by login name. You separate them by
      application_name / client_ip. SET  Application Name=SupplierPortal  in the
      app connection string so app traffic is identifiable; the review script then
      flags any change that does NOT carry that identity as "outside the app".

  IMPORTANT
  ---------
  1. Run in a NON-PRODUCTION environment first; measure file growth and CPU.
  2. Requires ALTER ANY SERVER AUDIT / CONTROL SERVER.
  3. Enable SQLCMD Mode in SSMS before running:  Query menu -> SQLCMD Mode.
  4. SQL Server Audit does NOT capture old/new column values. If you must be able
     to show the prior value of an edited row (e.g. a changed bank account), add
     temporal tables on the sensitive tables - see 03 in the original pack.
  5. Do NOT skip STEP 5 (tamper protection); without it the audited DBA can switch
     the audit off.
**********************************************************************************/

SET NOCOUNT ON;
GO

/*==============================================================================
  CONFIGURATION - edit before running
==============================================================================*/
:setvar AuditPath        "D:\SQLAudit\"        -- dedicated volume, NOT the data or log drive
:setvar AuditName        "Audit_SupplierPortal"
:setvar TargetDatabase   "BP"
:setvar MaxFileSizeMB    "256"
:setvar MaxRolloverFiles "500"                 -- 256MB x 500 = ~128GB ceiling
GO


/*==============================================================================
  STEP 1 - SERVER AUDIT (the destination)
  ------------------------------------------------------------------------------
  ON_FAILURE : FAIL_OPERATION rolls back the failing transaction if the audit
               cannot be written (instance stays up). CONTINUE is not acceptable
               for compliance. SHUTDOWN is the strongest control / biggest
               availability risk - choose deliberately and get it approved.
  QUEUE_DELAY: 1000ms is the standard compliance setting; 0 = synchronous.
==============================================================================*/
USE [master];
GO

IF EXISTS (SELECT 1 FROM sys.server_audits WHERE name = '$(AuditName)')
BEGIN
    ALTER SERVER AUDIT [$(AuditName)] WITH (STATE = OFF);
    DROP  SERVER AUDIT [$(AuditName)];
END
GO

CREATE SERVER AUDIT [$(AuditName)]
TO FILE
(
      FILEPATH           = N'$(AuditPath)'
    , MAXSIZE            = $(MaxFileSizeMB) MB
    , MAX_ROLLOVER_FILES = $(MaxRolloverFiles)
    , RESERVE_DISK_SPACE = OFF
)
WITH
(
      QUEUE_DELAY = 1000
    , ON_FAILURE  = FAIL_OPERATION
    , AUDIT_GUID  = NEWID()
);
GO

ALTER SERVER AUDIT [$(AuditName)] WITH (STATE = ON);
GO


/*==============================================================================
  STEP 2 - SERVER AUDIT SPECIFICATION   [ LAYER 1 : "SQL logins level" ]
  ------------------------------------------------------------------------------
  Low volume, high value (~tens-to-hundreds of MB/day). Captures who connects and
  every change to SQL logins, server roles and server-level permissions - plus
  AUDIT_CHANGE_GROUP, which records any attempt to alter/disable the audit itself.
==============================================================================*/
USE [master];
GO

IF EXISTS (SELECT 1 FROM sys.server_audit_specifications WHERE name = 'SrvAuditSpec_SupplierPortal')
BEGIN
    ALTER SERVER AUDIT SPECIFICATION [SrvAuditSpec_SupplierPortal] WITH (STATE = OFF);
    DROP  SERVER AUDIT SPECIFICATION [SrvAuditSpec_SupplierPortal];
END
GO

CREATE SERVER AUDIT SPECIFICATION [SrvAuditSpec_SupplierPortal]
FOR SERVER AUDIT [$(AuditName)]

    -- Tamper detection: any change to audits/traces
    ADD (AUDIT_CHANGE_GROUP),
    ADD (TRACE_CHANGE_GROUP),

    -- Authentication (who connected, when, from where)
    ADD (SUCCESSFUL_LOGIN_GROUP),
    ADD (FAILED_LOGIN_GROUP),
    ADD (LOGOUT_GROUP),
    ADD (LOGIN_CHANGE_PASSWORD_GROUP),

    -- SQL login / server role / server permission changes  (KPMG Gap 1 at server level)
    ADD (SERVER_PRINCIPAL_CHANGE_GROUP),          -- CREATE/ALTER/DROP LOGIN, enable/disable
    ADD (SERVER_ROLE_MEMBER_CHANGE_GROUP),        -- add/drop login to a server role (e.g. sysadmin)
    ADD (SERVER_PERMISSION_CHANGE_GROUP),
    ADD (SERVER_OBJECT_PERMISSION_CHANGE_GROUP),
    ADD (SERVER_OBJECT_OWNERSHIP_CHANGE_GROUP),

    -- Database-scoped principal/permission changes captured at server level
    ADD (DATABASE_PRINCIPAL_CHANGE_GROUP),        -- CREATE/ALTER/DROP USER
    ADD (DATABASE_ROLE_MEMBER_CHANGE_GROUP),      -- add/drop db user to a db role
    ADD (DATABASE_PERMISSION_CHANGE_GROUP),
    ADD (DATABASE_OBJECT_PERMISSION_CHANGE_GROUP),
    ADD (DATABASE_OWNERSHIP_CHANGE_GROUP),

    -- Privileged / operational activity worth evidencing
    ADD (SERVER_STATE_CHANGE_GROUP),
    ADD (BACKUP_RESTORE_GROUP),
    ADD (DBCC_GROUP);
GO

ALTER SERVER AUDIT SPECIFICATION [SrvAuditSpec_SupplierPortal] WITH (STATE = ON);
GO


/*==============================================================================
  STEP 3 - DATABASE AUDIT SPECIFICATION on [BP]   [ LAYER 2 : "data edited via SSMS" ]
  ------------------------------------------------------------------------------
  Captures every INSERT / UPDATE / DELETE (and EXECUTE) on [BP] BY [public] -
  i.e. every principal, no exceptions, DBAs included. This is what actually
  records a direct data edit made through SSMS.

  SELECT is deliberately NOT audited here. Read-auditing is what generates the
  large volume / CPU cost the business objected to; change-auditing does not.
  If read-auditing of specific sensitive tables is later required, add targeted
  ADD (SELECT ON OBJECT::... ) lines rather than SELECT on the whole database.

  BY [public] is intentional - never scope this to a role that excludes DBAs;
  the privileged user is exactly who the control exists to cover.
==============================================================================*/
USE [$(TargetDatabase)];
GO

IF EXISTS (SELECT 1 FROM sys.database_audit_specifications WHERE name = 'DbAuditSpec_BP')
BEGIN
    ALTER DATABASE AUDIT SPECIFICATION [DbAuditSpec_BP] WITH (STATE = OFF);
    DROP  DATABASE AUDIT SPECIFICATION [DbAuditSpec_BP];
END
GO

CREATE DATABASE AUDIT SPECIFICATION [DbAuditSpec_BP]
FOR SERVER AUDIT [$(AuditName)]

    -- Every data change by every principal (NO select - see note above)
    ADD (INSERT, UPDATE, DELETE, EXECUTE ON DATABASE::[$(TargetDatabase)] BY [public]),

    -- Access rights changes inside this database (KPMG Gap 1 at database level)
    ADD (DATABASE_PRINCIPAL_CHANGE_GROUP),
    ADD (DATABASE_ROLE_MEMBER_CHANGE_GROUP),
    ADD (DATABASE_PERMISSION_CHANGE_GROUP),
    ADD (DATABASE_OBJECT_PERMISSION_CHANGE_GROUP),

    -- Schema / object DDL inside [BP]
    ADD (SCHEMA_OBJECT_CHANGE_GROUP),
    ADD (SCHEMA_OBJECT_OWNERSHIP_CHANGE_GROUP),
    ADD (SCHEMA_OBJECT_PERMISSION_CHANGE_GROUP),

    -- Ad hoc / privileged database operations
    ADD (DATABASE_OPERATION_GROUP);
GO

ALTER DATABASE AUDIT SPECIFICATION [DbAuditSpec_BP] WITH (STATE = ON);
GO


/*==============================================================================
  STEP 4 - VERIFY THE DEPLOYMENT
==============================================================================*/
SELECT  a.name AS audit_name, a.type_desc AS destination, a.on_failure_desc,
        a.queue_delay, s.status_desc, s.audit_file_path
FROM    sys.server_audits a
JOIN    sys.dm_server_audit_status s ON s.audit_id = a.audit_id
WHERE   a.name = '$(AuditName)';
GO

SELECT name, is_state_enabled FROM sys.server_audit_specifications
WHERE  name = 'SrvAuditSpec_SupplierPortal';
GO

SELECT DB_NAME(database_id) AS [database], name, is_state_enabled
FROM   sys.database_audit_specifications;
GO


/*==============================================================================
  STEP 5 - TAMPER PROTECTION  (do not skip)
  ------------------------------------------------------------------------------
  The audit is only evidence if the people it audits cannot quietly switch it
  off. Apply all of:
    5.1  File-system ACL on $(AuditPath): SQL service account = Modify,
         internal audit/security group = Read. Remove DBAs / Everyone. The DBA
         must not be able to delete .sqlaudit files.
    5.2  Hold ALTER ANY SERVER AUDIT in a break-glass account (Security / PAM),
         not in the standing DBA role.
    5.3  Forward .sqlaudit files off-box to a SIEM / immutable storage same day.
    5.4  Alert on AUDIT_CHANGE_GROUP in real time, routed to Security (not DBAs).
    5.5  Alert on audit stoppage (health check below).

  Health check - schedule every 15 minutes via SQL Agent; route the alert to
  Security, NOT to the DBA distribution list.
==============================================================================*/
USE [master];
GO
IF NOT EXISTS
(
    SELECT 1 FROM sys.dm_server_audit_status
    WHERE name = '$(AuditName)' AND status_desc = 'STARTED'
)
BEGIN
    DECLARE @msg NVARCHAR(400) =
        N'COMPLIANCE ALERT: SQL Server Audit "$(AuditName)" is not running on '
        + @@SERVERNAME + N'. Investigate immediately - this may indicate tampering.';
    RAISERROR (@msg, 16, 1) WITH LOG;
    -- EXEC msdb.dbo.sp_send_dbmail @profile_name='AlertProfile',
    --      @recipients='security@yourcompany.com',
    --      @subject='SQL Audit STOPPED', @body=@msg;
END
GO


/*==============================================================================
  STEP 6 - SMOKE TEST  (prove the control exists before sign-off)
  ------------------------------------------------------------------------------
  Connect through SSMS as a privileged account, make a change, then confirm it
  appears via 02_Review_Audit_BP.sql. An untested audit is not a control.
==============================================================================*/
/*
    USE [BP];
    CREATE TABLE dbo.AuditSmokeTest (id INT IDENTITY, note NVARCHAR(100));
    INSERT dbo.AuditSmokeTest (note) VALUES (N'audit smoke test');
    UPDATE dbo.AuditSmokeTest SET note = N'changed via SSMS' WHERE id = 1;
    DELETE dbo.AuditSmokeTest WHERE id = 1;
    DROP  TABLE dbo.AuditSmokeTest;
*/


/*==============================================================================
  ROLLBACK - if the deployment must be reversed
==============================================================================*/
/*
    USE [BP];
    ALTER DATABASE AUDIT SPECIFICATION [DbAuditSpec_BP] WITH (STATE = OFF);
    DROP  DATABASE AUDIT SPECIFICATION [DbAuditSpec_BP];

    USE [master];
    ALTER SERVER AUDIT SPECIFICATION [SrvAuditSpec_SupplierPortal] WITH (STATE = OFF);
    DROP  SERVER AUDIT SPECIFICATION [SrvAuditSpec_SupplierPortal];
    ALTER SERVER AUDIT [Audit_SupplierPortal] WITH (STATE = OFF);
    DROP  SERVER AUDIT [Audit_SupplierPortal];
    -- Retain existing .sqlaudit files. Deleting them destroys audit evidence.
*/
