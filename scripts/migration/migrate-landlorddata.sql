/* ============================================================================================
   Landlord data migration — legacy [dbo].[LandlordData]  ->  new [leasing].* schema
   --------------------------------------------------------------------------------------------
   Run this in the TARGET database (BP), which holds the leasing.* tables. The legacy table is
   referenced by a three-part name; set @SourceDb below. If the legacy database lives on a
   DIFFERENT SQL Server instance, create a linked server and replace the three-part names with
   four-part names ([LINKEDSRV].[ProdPropertyManagementSystem].[dbo].[LandlordData]).

   Idempotent: rows already migrated (matched by leasing.Landlords.LegacySourceId) are skipped,
   so the script is safe to re-run. Wrapped in a single transaction.

   Enum values (must match the app):
     LandlordType         : Individual=0, Company=1
     LandlordStatus       : Draft=0, InvitationSent=1, OtpVerified=2, InProgress=3, Submitted=4,
                            UnderReview=5, CorrectionRequired=6, Rejected=7, RegistrationComplete=8
     ContactType          : Owner=0, AuthorisedRepresentative=1
     LandlordDocumentType : EmiratesId=0, TradeLicense=1, Moa=2, PowerOfAttorney=3,
                            AuthorisationLetter=4, TitleDeed=5, Other=6
   ============================================================================================ */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;   -- required by the xs:base64Binary XML method used for varbinary -> Base64
SET ANSI_NULLS ON;

DECLARE @SourceDb  SYSNAME       = N'ProdPropertyManagementSystem';
DECLARE @MigActor  NVARCHAR(100) = N'Legacy Migration';
DECLARE @BusinessUnitId INT      = 0;          -- set the BU these legacy records belong to (0 = unassigned)

/* The legacy table is referenced dynamically so @SourceDb can vary. We stage the source into a
   temp table once, then do set-based inserts against the staging copy. */
IF OBJECT_ID('tempdb..#src') IS NOT NULL DROP TABLE #src;
CREATE TABLE #src (
    Id INT PRIMARY KEY, AgreementType NVARCHAR(50), PlotNo NVARCHAR(500),
    ContactName NVARCHAR(500), MobileNumber NVARCHAR(20), Email NVARCHAR(500),
    PrimaryName VARCHAR(1000), PrimaryEmiratesIdNo VARCHAR(50), FirstName VARCHAR(255), LastName VARCHAR(255),
    PrimaryPOA VARBINARY(MAX), PrimaryEmiratesId VARBINARY(MAX),
    SecondaryContactName NVARCHAR(500), SecondaryMobileNumber NVARCHAR(20), SecondaryEmail NVARCHAR(500),
    SecondayName VARCHAR(1000), SecondayEmiratesIdNo VARCHAR(50),
    SecondaryPOA VARBINARY(MAX), SecondaryEmiratesId VARBINARY(MAX),
    CreatedOn DATETIME, isverified INT, Emailsent INT
);

DECLARE @sql NVARCHAR(MAX) = N'
    INSERT INTO #src SELECT Id, AgreementType, PlotNo, ContactName, MobileNumber, Email,
        PrimaryName, PrimaryEmiratesIdNo, FirstName, LastName, PrimaryPOA, PrimaryEmiratesId,
        SecondaryContactName, SecondaryMobileNumber, SecondaryEmail, SecondayName, SecondayEmiratesIdNo,
        SecondaryPOA, SecondaryEmiratesId, CreatedOn, isverified, Emailsent
    FROM ' + QUOTENAME(@SourceDb) + N'.[dbo].[LandlordData];';
EXEC sys.sp_executesql @sql;

BEGIN TRAN;

/* ---- 1) Registrations (leasing.Landlords) ---- */
INSERT INTO leasing.Landlords
    (BusinessUnitId, Type, PropertyId, PropertyCode, CompanyName, Building, Email, LandLordCode,
     SigningRule, Status, LinkActive, Reference, CreatedBy, CreatedAt, SubmittedAt, LegacySourceId)
SELECT
    @BusinessUnitId,
    CASE WHEN LOWER(LTRIM(RTRIM(s.AgreementType))) = 'individual' THEN 0 ELSE 1 END,     -- Type
    ISNULL((SELECT MIN(p.Id) FROM dbo.Property p WHERE p.PropertyCode = s.PlotNo), 0),   -- PropertyId (view lookup)
    s.PlotNo,
    s.ContactName,                                                                        -- CompanyName
    NULL,                                                                                 -- Building (not in legacy)
    ISNULL(s.Email, N''),                                                                 -- invitation email
    NULL,
    0,                                                                                    -- SigningRule = All
    CASE WHEN ISNULL(s.isverified,0) = 1 THEN 4                                           -- verified  -> Submitted (BC reviews)
         WHEN ISNULL(s.Emailsent,0)  = 1 THEN 1                                           -- invited   -> InvitationSent
         ELSE 0 END,                                                                      -- otherwise -> Draft
    1,                                                                                    -- LinkActive
    CASE WHEN ISNULL(s.isverified,0)=1
         THEN CONCAT('DIP-LR-', CONVERT(varchar(4), YEAR(ISNULL(s.CreatedOn, GETDATE()))), '-', RIGHT('000000'+CAST(s.Id AS varchar(10)),6))
         END,                                                                             -- Reference for verified
    @MigActor,
    ISNULL(s.CreatedOn, GETUTCDATE()),
    CASE WHEN ISNULL(s.isverified,0)=1 THEN ISNULL(s.CreatedOn, GETUTCDATE()) END,
    s.Id
FROM #src s
WHERE NOT EXISTS (SELECT 1 FROM leasing.Landlords x WHERE x.LegacySourceId = s.Id);

/* ---- 2) Primary contacts ---- */
INSERT INTO leasing.LandlordContacts
    (LandlordId, IsPrimary, ContactType, FullName, Designation, Mobile, Email, EmiratesId, CreatedAt)
SELECT l.Id, 1, 0,
       COALESCE(NULLIF(LTRIM(RTRIM(s.PrimaryName)),''),
                NULLIF(LTRIM(RTRIM(CONCAT(s.FirstName,' ',s.LastName))),''),
                s.ContactName),
       NULL, s.MobileNumber, s.Email, s.PrimaryEmiratesIdNo, ISNULL(s.CreatedOn, GETUTCDATE())
FROM leasing.Landlords l
JOIN #src s ON s.Id = l.LegacySourceId
WHERE l.LegacySourceId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM leasing.LandlordContacts c WHERE c.LandlordId = l.Id AND c.IsPrimary = 1);

/* ---- 3) Secondary contacts (only where present) ---- */
INSERT INTO leasing.LandlordContacts
    (LandlordId, IsPrimary, ContactType, FullName, Designation, Mobile, Email, EmiratesId, CreatedAt)
SELECT l.Id, 0, 1,
       COALESCE(NULLIF(LTRIM(RTRIM(s.SecondayName)),''), s.SecondaryContactName),
       NULL, s.SecondaryMobileNumber, s.SecondaryEmail, s.SecondayEmiratesIdNo, ISNULL(s.CreatedOn, GETUTCDATE())
FROM leasing.Landlords l
JOIN #src s ON s.Id = l.LegacySourceId
WHERE l.LegacySourceId IS NOT NULL
  AND (NULLIF(LTRIM(RTRIM(s.SecondaryContactName)),'') IS NOT NULL OR NULLIF(LTRIM(RTRIM(s.SecondayName)),'') IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM leasing.LandlordContacts c WHERE c.LandlordId = l.Id AND c.IsPrimary = 0);

/* ---- 4) Documents: varbinary(max) -> Base64 varchar(max), content-type from magic bytes ---- */
;WITH docs AS (
    SELECT l.Id AS LandlordId, s.Id AS SrcId, 3 AS DocType, 'Primary POA' AS Nm, 1 AS ForPrimary, s.PrimaryPOA AS Blob
      FROM leasing.Landlords l JOIN #src s ON s.Id = l.LegacySourceId WHERE s.PrimaryPOA IS NOT NULL
    UNION ALL
    SELECT l.Id, s.Id, 0, 'Primary Emirates ID', 1, s.PrimaryEmiratesId
      FROM leasing.Landlords l JOIN #src s ON s.Id = l.LegacySourceId WHERE s.PrimaryEmiratesId IS NOT NULL
    UNION ALL
    SELECT l.Id, s.Id, 3, 'Secondary POA', 0, s.SecondaryPOA
      FROM leasing.Landlords l JOIN #src s ON s.Id = l.LegacySourceId WHERE s.SecondaryPOA IS NOT NULL
    UNION ALL
    SELECT l.Id, s.Id, 0, 'Secondary Emirates ID', 0, s.SecondaryEmiratesId
      FROM leasing.Landlords l JOIN #src s ON s.Id = l.LegacySourceId WHERE s.SecondaryEmiratesId IS NOT NULL
)
INSERT INTO leasing.LandlordDocuments
    (LandlordId, DocumentType, FileName, StoredFileName, ContentType, FileSizeBytes, ContentBase64,
     UploadedBy, ContactId, Version, IsCurrent, UploadedAt)
SELECT d.LandlordId, d.DocType,
       d.Nm + ext.x,
       LOWER(CONVERT(varchar(36), NEWID())) + ext.x,
       ext.ct,
       DATALENGTH(d.Blob),
       CAST(N'' AS XML).value('xs:base64Binary(sql:column("d.Blob"))', 'VARCHAR(MAX)'),
       @MigActor,
       (SELECT TOP 1 c.Id FROM leasing.LandlordContacts c WHERE c.LandlordId = d.LandlordId AND c.IsPrimary = d.ForPrimary),
       1, 1, GETUTCDATE()
FROM docs d
CROSS APPLY (SELECT
    CASE WHEN SUBSTRING(d.Blob,1,4) = 0x25504446 THEN '.pdf'
         WHEN SUBSTRING(d.Blob,1,3) = 0xFFD8FF   THEN '.jpg'
         WHEN SUBSTRING(d.Blob,1,4) = 0x89504E47 THEN '.png'
         ELSE '.bin' END AS x,
    CASE WHEN SUBSTRING(d.Blob,1,4) = 0x25504446 THEN 'application/pdf'
         WHEN SUBSTRING(d.Blob,1,3) = 0xFFD8FF   THEN 'image/jpeg'
         WHEN SUBSTRING(d.Blob,1,4) = 0x89504E47 THEN 'image/png'
         ELSE 'application/octet-stream' END AS ct) ext
WHERE NOT EXISTS (
    SELECT 1 FROM leasing.LandlordDocuments x
    WHERE x.LandlordId = d.LandlordId AND x.DocumentType = d.DocType
      AND x.ContactId = (SELECT TOP 1 c.Id FROM leasing.LandlordContacts c WHERE c.LandlordId = d.LandlordId AND c.IsPrimary = d.ForPrimary));

/* ---- 5) Audit event per migrated registration ---- */
INSERT INTO leasing.LandlordEvents (LandlordId, EventType, Detail, [By], ByType, NewStatus, [At])
SELECT l.Id, 'RegistrationMigrated',
       CONCAT('Migrated from legacy LandlordData #', l.LegacySourceId),
       @MigActor, 'System',
       CASE l.Status WHEN 4 THEN 'Submitted' WHEN 1 THEN 'InvitationSent' ELSE 'Draft' END,
       GETUTCDATE()
FROM leasing.Landlords l
JOIN #src s ON s.Id = l.LegacySourceId
WHERE NOT EXISTS (SELECT 1 FROM leasing.LandlordEvents e WHERE e.LandlordId = l.Id AND e.EventType = 'RegistrationMigrated');

COMMIT TRAN;

/* ---- Verification ---- */
SELECT
    (SELECT COUNT(*) FROM #src)                                                   AS SourceRows,
    (SELECT COUNT(*) FROM leasing.Landlords WHERE LegacySourceId IS NOT NULL)     AS MigratedRegistrations,
    (SELECT COUNT(*) FROM leasing.LandlordContacts c JOIN leasing.Landlords l ON l.Id=c.LandlordId WHERE l.LegacySourceId IS NOT NULL) AS MigratedContacts,
    (SELECT COUNT(*) FROM leasing.LandlordDocuments d JOIN leasing.Landlords l ON l.Id=d.LandlordId WHERE l.LegacySourceId IS NOT NULL) AS MigratedDocuments;

DROP TABLE #src;
