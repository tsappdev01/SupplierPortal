/*==============================================================================================
  Leasing-LandlordData-Migration.sql

  PURPOSE
  -------
  Bring the Landlord portal to PARITY with already-completed registrations that live in the
  legacy source table dbo.LandlordData. After this script runs, each completed legacy
  registration appears in the Landlord portal as a COMPLETED landlord (status
  RegistrationComplete = 8) together with its premises link, primary/secondary contacts,
  signatories and documents, so those landlords/tenants never have to re-register.

  WHERE IT RUNS
  -------------
  This script runs IN the Landlord application database -- the SAME database that holds both
  the application-owned leasing.* tables AND the legacy dbo.LandlordData / dbo.Property objects
  (the operator runs everything in ProdPropertyManagementSystem). There are therefore NO
  cross-database links; every object below is referenced in the current database.

  PREREQUISITES
  -------------
  * The leasing.* tables already exist (created by the LeasingDbContext EF Core migrations):
        leasing.Landlords, leasing.LandlordPremises, leasing.LandlordContacts,
        leasing.LandlordSignatories, leasing.LandlordDocuments.
  * dbo.LandlordData, dbo.Property and dbo.BusinessUnits exist in this database.
  * dbo.MaintainTransactionKeys and dbo.MaintainTenant exist in this database (source tenant chain,
    joined on PlotNo): they supply the signatories and the signing rule. dbo.MaintainTenantUAEPassEmails
    (already referenced for signatories) must also exist.

  IDEMPOTENCY & TRANSACTIONS
  --------------------------
  * Idempotency key is PER ROW: leasing.Landlords.LegacySourceId = dbo.LandlordData.Id.
    A legacy row whose Id already appears as a LegacySourceId is skipped, so the script is
    safe to re-run -- it only migrates rows that are not yet present.
  * The whole migration runs inside a SINGLE outer transaction with XACT_ABORT ON and a
    TRY/CATCH. On any hard failure the transaction is rolled back and the error re-thrown
    (all-or-nothing). Rows whose PlotNo has no matching dbo.Property are NOT hard failures:
    they are skipped and reported via PRINT, and the migration continues.

  ----------------------------------------------------------------------------------------------
  ASSUMPTIONS THE OPERATOR SHOULD VERIFY BEFORE RUNNING
  ----------------------------------------------------------------------------------------------
  1. dbo.LandlordData COLUMN SPELLINGS. This script references the source columns exactly as
     they were reported from the live table, INCLUDING the source's own misspellings
     "Seconday" (SecondayEmiratesIdNo, SecondayName) and "DocumentNumber". If any column name
     differs in your instance (e.g. "Secondary..." spelled correctly, or a renamed column),
     adjust the reference here to match -- a wrong name will raise "Invalid column name".
  2. DOCUMENT CONTENT TYPE. All migrated documents are assumed to be PDFs and are stored with
     ContentType = 'application/pdf'. Change this if a source document is a different format.
  3. SIGNATORIES AND SIGNING RULE COME FROM THE SOURCE TENANT CHAIN, not from LandlordData's
     primary/secondary contacts. Each LandlordData row is matched to at most one source tenant via
     PlotNo through the chain
        dbo.MaintainTransactionKeys (PropertyCode = PlotNo, TranCode NOT LIKE '%-%')
          -> dbo.MaintainTenant (TenantCode)
          -> dbo.MaintainTenantUAEPassEmails (MaintainTenantId).
     SigningRule is taken from dbo.MaintainTenant.SignatureType: 'OR' => 1 (Any), otherwise 0 (All).
     The signatories are the active dbo.MaintainTenantUAEPassEmails rows (OwnershipLevel = signing
     order, Email the address; the source has no name, so the name is derived from the email
     local-part). When NO tenant resolves for a row, the script falls back to the old behaviour and
     builds signatories from the LandlordData primary/secondary contacts (and SigningRule stays 0 = All).
  5. DATE FORMATS. CreatedOn and DateOfExpiration may be stored as TEXT. They are parsed with
     TRY_CONVERT (DateOfExpiration as dd/mm/yyyy = style 103, e.g. '29/05/2027'); an unparseable
     value becomes NULL (CreatedAt then falls back to now, EmiratesIdExpiry stays NULL) rather than
     failing the run. Adjust the style if your dates use a different format.

  4. TYPE (Individual vs Company). AgreementType is mapped to Type with:
        Type = 1 (Company)    when AgreementType LIKE 'Corp%' OR LIKE 'Comp%'
        Type = 0 (Individual) otherwise.
     Verify this covers every AgreementType value your data uses.

  ENUM VALUES USED (all enum columns stored as int):
     LandlordType:         Individual=0, Company=1
     SigningRule:          All=0, Any=1
     LandlordStatus:       RegistrationComplete=8
     LandlordDocumentType: EmiratesId=0, MoaOrPoa=7   (PowerOfAttorney=3, Other=6 not used here)
     ContactType:          Owner=0, AuthorisedRepresentative=1
==============================================================================================*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

-------------------------------------------------------------------------------------------------
-- Run-wide counters (reported in the final summary).
-------------------------------------------------------------------------------------------------
DECLARE @cntLandlords    int = 0;
DECLARE @cntPremises     int = 0;
DECLARE @cntContacts     int = 0;
DECLARE @cntSignatories  int = 0;
DECLARE @cntDocuments    int = 0;
DECLARE @cntSkippedNoProp int = 0;

-------------------------------------------------------------------------------------------------
-- Resolve the default Business Unit once (DIP preferred, else the lowest Id).
-------------------------------------------------------------------------------------------------
DECLARE @BusinessUnitId int;
SELECT TOP (1) @BusinessUnitId = Id
FROM dbo.BusinessUnits
ORDER BY CASE WHEN Code = 'DIP' THEN 0 ELSE 1 END, Id;

IF @BusinessUnitId IS NULL
BEGIN
    RAISERROR('No rows found in dbo.BusinessUnits -- cannot assign a default BusinessUnitId. Aborting.', 16, 1);
    RETURN;
END

-------------------------------------------------------------------------------------------------
-- Per-row working variables.
-------------------------------------------------------------------------------------------------
DECLARE @SourceId              int;
DECLARE @AgreementType         nvarchar(200);
DECLARE @PlotNo                nvarchar(200);
DECLARE @ContactName           nvarchar(200);
DECLARE @MobileNumber          nvarchar(200);
DECLARE @Email                 nvarchar(200);
DECLARE @SecondaryContactName  nvarchar(200);
DECLARE @SecondaryMobileNumber nvarchar(200);
DECLARE @SecondaryEmail        nvarchar(200);
DECLARE @RawCreatedOn          datetime2(7);
DECLARE @PrimaryEmiratesIdNo   nvarchar(100);
DECLARE @SecondayEmiratesIdNo  nvarchar(100);   -- source spelling "Seconday"
DECLARE @PrimaryName           nvarchar(200);
DECLARE @SecondayName          nvarchar(200);    -- source spelling "Seconday"
DECLARE @DateOfExpiration      datetime2(7);
DECLARE @TenantId              bigint;           -- source tenant id (dbo.MaintainTenant.ID), resolved via PlotNo
DECLARE @SignatureType         nvarchar(10);     -- source signing rule 'OR'/'AND' (dbo.MaintainTenant.SignatureType)

DECLARE @CreatedOn             datetime2(7);
DECLARE @SigningRule           int;              -- 1=Any (OR), 0=All; derived per row from @SignatureType
DECLARE @PropertyId            int;
DECLARE @PropCompanyName       nvarchar(200);
DECLARE @PropBuildingName      nvarchar(250);
DECLARE @PropLandLordCode      nvarchar(50);
DECLARE @Zone                  nvarchar(10);
DECLARE @Type                  int;
DECLARE @HasSecondary          bit;

DECLARE @LandlordId            int;
DECLARE @PrimaryContactId      int;
DECLARE @SecondaryContactId    int;

BEGIN TRY
    BEGIN TRANSACTION;

    -------------------------------------------------------------------------------------------
    -- Cursor over legacy rows that have NOT yet been migrated (idempotency by LegacySourceId).
    -- NOTE: if any column name below differs in your instance, correct it here.
    -------------------------------------------------------------------------------------------
    DECLARE cur CURSOR LOCAL FORWARD_ONLY READ_ONLY FOR
        SELECT  d.Id,
                d.AgreementType,
                d.PlotNo,
                d.ContactName,
                d.MobileNumber,
                d.Email,
                d.SecondaryContactName,
                d.SecondaryMobileNumber,
                d.SecondaryEmail,
                -- Dates may be stored as text. Parse defensively so a bad/locale value becomes NULL
                -- instead of aborting the whole run. CreatedOn is tried as ISO/native then dd/mm/yyyy;
                -- DateOfExpiration is dd/mm/yyyy (style 103, e.g. '29/05/2027') then a native fallback.
                COALESCE(TRY_CONVERT(datetime2(7), d.CreatedOn),
                         TRY_CONVERT(datetime2(7), d.CreatedOn, 103)),
                d.PrimaryEmiratesIdNo,
                d.SecondayEmiratesIdNo,   -- source spelling
                d.PrimaryName,
                d.SecondayName,           -- source spelling
                COALESCE(TRY_CONVERT(datetime2(7), d.DateOfExpiration, 103),
                         TRY_CONVERT(datetime2(7), d.DateOfExpiration)),
                -- Signatories and the signing rule come from the source tenant chain, keyed on PlotNo:
                --   MaintainTransactionKeys -> MaintainTenant (-> MaintainTenantUAEPassEmails later).
                -- Resolve AT MOST ONE tenant per row deterministically (lowest MaintainTenant.ID).
                ten.TenantId,
                ten.SignatureType
        FROM    dbo.LandlordData d
        OUTER APPLY (
                    SELECT TOP (1) mt.ID AS TenantId, mt.SignatureType AS SignatureType
                    FROM dbo.MaintainTransactionKeys k
                    JOIN dbo.MaintainTenant mt ON mt.TenantCode = k.TenantCode
                    WHERE k.PropertyCode = d.PlotNo AND k.TranCode NOT LIKE '%-%'
                    ORDER BY mt.ID
                ) ten
        WHERE   NOT EXISTS (
                    SELECT 1 FROM leasing.Landlords l WHERE l.LegacySourceId = d.Id
                );

    OPEN cur;
    FETCH NEXT FROM cur INTO
        @SourceId, @AgreementType, @PlotNo, @ContactName, @MobileNumber, @Email,
        @SecondaryContactName, @SecondaryMobileNumber, @SecondaryEmail, @RawCreatedOn,
        @PrimaryEmiratesIdNo, @SecondayEmiratesIdNo, @PrimaryName, @SecondayName,
        @DateOfExpiration, @TenantId, @SignatureType;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        -- Normalise CreatedOn (fallback to now) and derive Type.
        SET @CreatedOn = COALESCE(CAST(@RawCreatedOn AS datetime2(7)), SYSUTCDATETIME());
        SET @Type = CASE
                        WHEN @AgreementType LIKE 'Corp%' OR @AgreementType LIKE 'Comp%' THEN 1  -- Company
                        ELSE 0                                                                  -- Individual
                    END;

        -- Does this legacy row have a usable secondary contact?
        SET @HasSecondary = CASE
                                WHEN NULLIF(LTRIM(RTRIM(@SecondaryContactName)), N'') IS NOT NULL
                                  OR NULLIF(LTRIM(RTRIM(@SecondaryEmail)),       N'') IS NOT NULL
                                THEN 1 ELSE 0
                            END;

        ---------------------------------------------------------------------------------------
        -- Resolve the property by PropertyCode = PlotNo. Skip (and report) if none matches.
        ---------------------------------------------------------------------------------------
        SET @PropertyId       = NULL;
        SET @PropCompanyName  = NULL;
        SET @PropBuildingName = NULL;
        SET @PropLandLordCode = NULL;

        SELECT TOP (1)
               @PropertyId       = p.Id,
               @PropCompanyName  = p.CompanyName,
               @PropBuildingName = p.BuildingName,
               @PropLandLordCode = p.LandLordCode
        FROM   dbo.Property p
        WHERE  p.PropertyCode = @PlotNo
        ORDER BY p.Id;

        IF @PropertyId IS NULL
        BEGIN
            SET @cntSkippedNoProp = @cntSkippedNoProp + 1;
            PRINT CONCAT('SKIP LandlordData.Id=', @SourceId,
                         ' -- no dbo.Property row with PropertyCode=''', @PlotNo, '''.');

            FETCH NEXT FROM cur INTO
                @SourceId, @AgreementType, @PlotNo, @ContactName, @MobileNumber, @Email,
                @SecondaryContactName, @SecondaryMobileNumber, @SecondaryEmail, @RawCreatedOn,
                @PrimaryEmiratesIdNo, @SecondayEmiratesIdNo, @PrimaryName, @SecondayName,
                @DateOfExpiration, @TenantId, @SignatureType;
            CONTINUE;
        END

        -- Zone = the segment of the property code before the first dash (e.g. '597-1003' -> '597').
        -- dbo.Property has no stored Zone column (it is a derived value in the app model), so it is
        -- computed here from PropertyCode and truncated to the column width (nvarchar(10)).
        SET @Zone = LEFT(
                        CASE WHEN CHARINDEX('-', @PlotNo) > 0
                             THEN LEFT(@PlotNo, CHARINDEX('-', @PlotNo) - 1)
                             ELSE @PlotNo END, 10);

        -- Signing rule from the source tenant's SignatureType: 'OR' => 1 (Any), anything else => 0 (All).
        SET @SigningRule = CASE WHEN UPPER(LTRIM(RTRIM(@SignatureType))) = 'OR' THEN 1 ELSE 0 END;

        ---------------------------------------------------------------------------------------
        -- 1) Parent: leasing.Landlords
        --    Id is IDENTITY -- never set it; capture it with SCOPE_IDENTITY().
        ---------------------------------------------------------------------------------------
        INSERT INTO leasing.Landlords
            (BusinessUnitId, [Type], PropertyId, PropertyCode, CompanyName, Building, Email,
             LandLordCode, SigningRule, [Status], LinkActive,
             CreatedBy, CreatedAt, SubmittedAt, CompletedAt, CompletedBy, LegacySourceId)
        VALUES
            (@BusinessUnitId,
             @Type,
             @PropertyId,
             LEFT(@PlotNo, 80),                                    -- PropertyCode (NOT NULL)
             LEFT(COALESCE(@PropCompanyName, @ContactName), 200),  -- CompanyName (fallback ContactName)
             LEFT(@PropBuildingName, 250),                         -- Building
             LEFT(COALESCE(@Email, N''), 200),                     -- Email (NOT NULL)
             LEFT(@PropLandLordCode, 50),                          -- LandLordCode
             @SigningRule,                                         -- SigningRule from MaintainTenant.SignatureType (1=Any when 'OR', else 0=All)
             8,                                                    -- Status = RegistrationComplete
             0,                                                    -- LinkActive = 0 (link deactivated for completed reg.)
             N'LandlordData migration',                            -- CreatedBy
             @CreatedOn,                                           -- CreatedAt
             @CreatedOn,                                           -- SubmittedAt
             @CreatedOn,                                           -- CompletedAt
             N'LandlordData migration',                            -- CompletedBy
             @SourceId);                                           -- LegacySourceId (idempotency key)

        SET @LandlordId = CAST(SCOPE_IDENTITY() AS int);

        -- SigningRule is now set directly in the INSERT above from dbo.MaintainTenant.SignatureType
        -- (resolved per row via the PlotNo tenant chain), so the old optional override block is removed.

        -- Human-readable reference, e.g. DIP-LR-2026-104882.
        UPDATE leasing.Landlords
           SET Reference = 'DIP-LR-'
                         + CONVERT(varchar(4),  YEAR(@CreatedOn))
                         + '-'
                         + CONVERT(varchar(20), 100000 + @LandlordId)
         WHERE Id = @LandlordId;

        SET @cntLandlords = @cntLandlords + 1;

        ---------------------------------------------------------------------------------------
        -- 2) leasing.LandlordPremises (one ownership link to the resolved property).
        ---------------------------------------------------------------------------------------
        INSERT INTO leasing.LandlordPremises
            (LandlordId, PremisesId, PropertyCode, BuildingName, Zone, LinkedAt, LinkedBy)
        VALUES
            (@LandlordId, @PropertyId, LEFT(@PlotNo, 80), LEFT(@PropBuildingName, 250),
             @Zone, @CreatedOn, N'LandlordData migration');

        SET @cntPremises = @cntPremises + 1;

        ---------------------------------------------------------------------------------------
        -- 3) leasing.LandlordContacts -- PRIMARY (always) then SECONDARY (when present).
        ---------------------------------------------------------------------------------------
        INSERT INTO leasing.LandlordContacts
            (LandlordId, IsPrimary, ContactType, FullName, Email, Mobile,
             EmiratesId, EmiratesIdName, EmiratesIdExpiry, CreatedAt)
        VALUES
            (@LandlordId,
             1,                                                        -- IsPrimary
             0,                                                        -- ContactType = Owner
             LEFT(COALESCE(@PrimaryName, @ContactName, N''), 150),     -- FullName (NOT NULL)
             LEFT(COALESCE(@Email, N''), 200),                         -- Email (NOT NULL)
             LEFT(@MobileNumber, 40),                                  -- Mobile
             LEFT(@PrimaryEmiratesIdNo, 50),                           -- EmiratesId
             LEFT(@PrimaryName, 200),                                  -- EmiratesIdName
             CAST(@DateOfExpiration AS datetime2(7)),                  -- EmiratesIdExpiry
             @CreatedOn);

        SET @PrimaryContactId = CAST(SCOPE_IDENTITY() AS int);
        SET @cntContacts = @cntContacts + 1;

        SET @SecondaryContactId = NULL;
        IF @HasSecondary = 1
        BEGIN
            INSERT INTO leasing.LandlordContacts
                (LandlordId, IsPrimary, ContactType, FullName, Email, Mobile,
                 EmiratesId, CreatedAt)
            VALUES
                (@LandlordId,
                 0,                                                                -- IsPrimary
                 1,                                                                -- ContactType = AuthorisedRepresentative
                 LEFT(COALESCE(@SecondayName, @SecondaryContactName, N''), 150),   -- FullName (NOT NULL)
                 LEFT(COALESCE(@SecondaryEmail, N''), 200),                        -- Email (NOT NULL)
                 LEFT(@SecondaryMobileNumber, 40),                                 -- Mobile
                 LEFT(@SecondayEmiratesIdNo, 50),                                  -- EmiratesId
                 @CreatedOn);

            SET @SecondaryContactId = CAST(SCOPE_IDENTITY() AS int);
            SET @cntContacts = @cntContacts + 1;
        END

        ---------------------------------------------------------------------------------------
        -- 4) leasing.LandlordSignatories -- from the SOURCE TENANT CHAIN when a tenant resolves,
        --    else fall back to the LandlordData primary/secondary contacts.
        --    [Order] is a reserved word -> always bracketed.
        --
        --    When @TenantId IS NOT NULL the authorised signatories come from
        --    dbo.MaintainTenantUAEPassEmails (one active row per signatory, OwnershipLevel = order).
        --    The source has no signatory name, so [Name] is derived from the email local-part
        --    (the text before '@'); when the email is null/empty it becomes 'Signatory <level>'.
        ---------------------------------------------------------------------------------------
        IF @TenantId IS NOT NULL
        BEGIN
            INSERT INTO leasing.LandlordSignatories
                (LandlordId, [Name], Email, Mobile, [Order], IsPrimaryContact)
            SELECT @LandlordId,
                   LEFT(
                       CASE
                           WHEN NULLIF(LTRIM(RTRIM(u.Email)), N'') IS NULL
                               THEN N'Signatory ' + CONVERT(varchar, u.OwnershipLevel)
                           WHEN CHARINDEX('@', LTRIM(RTRIM(u.Email))) > 0
                               THEN LEFT(LTRIM(RTRIM(u.Email)),
                                         CHARINDEX('@', LTRIM(RTRIM(u.Email))) - 1)
                           ELSE LTRIM(RTRIM(u.Email))
                       END, 150),                                  -- [Name] derived from email local-part
                   LEFT(COALESCE(u.Email, N''), 200),              -- Email
                   NULL,                                           -- Mobile (none in source)
                   u.OwnershipLevel,                               -- [Order] = signing order
                   CASE WHEN u.OwnershipLevel = 1 THEN 1 ELSE 0 END -- IsPrimaryContact
            FROM dbo.MaintainTenantUAEPassEmails u
            WHERE u.MaintainTenantId = @TenantId
              AND u.IsActive = 1
              AND u.IsDeleted = 0
            ORDER BY u.OwnershipLevel;
            SET @cntSignatories = @cntSignatories + @@ROWCOUNT;
        END
        ELSE
        BEGIN
            -- Fallback: no tenant resolved -> keep the old LandlordData-based signatories.
            INSERT INTO leasing.LandlordSignatories
                (LandlordId, [Name], Email, Mobile, [Order], IsPrimaryContact)
            VALUES
                (@LandlordId,
                 LEFT(COALESCE(@PrimaryName, @ContactName, N''), 150),
                 LEFT(COALESCE(@Email, N''), 200),
                 LEFT(@MobileNumber, 40),
                 1,
                 1);
            SET @cntSignatories = @cntSignatories + 1;

            IF @HasSecondary = 1
            BEGIN
                INSERT INTO leasing.LandlordSignatories
                    (LandlordId, [Name], Email, Mobile, [Order], IsPrimaryContact)
                VALUES
                    (@LandlordId,
                     LEFT(COALESCE(@SecondayName, @SecondaryContactName, N''), 150),
                     LEFT(COALESCE(@SecondaryEmail, N''), 200),
                     LEFT(@SecondaryMobileNumber, 40),
                     2,
                     0);
                SET @cntSignatories = @cntSignatories + 1;
            END
        END

        ---------------------------------------------------------------------------------------
        -- 5) leasing.LandlordDocuments -- one row per non-empty source VARBINARY document.
        --    VARBINARY -> Base64 via the XML xs:base64Binary trick. Each insert re-reads the
        --    single source row by Id so sql:column() can reference the varbinary column.
        --    Only inserted when the column IS NOT NULL and DATALENGTH > 0.
        ---------------------------------------------------------------------------------------

        -- 5a) Primary Emirates ID -> DocumentType 0 (EmiratesId), ContactId = primary.
        INSERT INTO leasing.LandlordDocuments
            (LandlordId, DocumentType, FileName, StoredFileName, ContentType, FileSizeBytes,
             ContentBase64, UploadedAt, UploadedBy, ContactId, [Version], IsCurrent)
        SELECT @LandlordId, 0, N'EmiratesID-Primary.pdf', N'EmiratesID-Primary.pdf',
               N'application/pdf', DATALENGTH(d.PrimaryEmiratesId),
               CAST(N'' AS XML).value('xs:base64Binary(sql:column("d.PrimaryEmiratesId"))', 'VARCHAR(MAX)'),
               @CreatedOn, N'LandlordData migration', @PrimaryContactId, 1, 1
        FROM   dbo.LandlordData d
        WHERE  d.Id = @SourceId
          AND  d.PrimaryEmiratesId IS NOT NULL
          AND  DATALENGTH(d.PrimaryEmiratesId) > 0;
        SET @cntDocuments = @cntDocuments + @@ROWCOUNT;

        -- 5b) Primary POA -> DocumentType 7 (MoaOrPoa), ContactId = primary.
        INSERT INTO leasing.LandlordDocuments
            (LandlordId, DocumentType, FileName, StoredFileName, ContentType, FileSizeBytes,
             ContentBase64, UploadedAt, UploadedBy, ContactId, [Version], IsCurrent)
        SELECT @LandlordId, 7, N'POA-Primary.pdf', N'POA-Primary.pdf',
               N'application/pdf', DATALENGTH(d.PrimaryPOA),
               CAST(N'' AS XML).value('xs:base64Binary(sql:column("d.PrimaryPOA"))', 'VARCHAR(MAX)'),
               @CreatedOn, N'LandlordData migration', @PrimaryContactId, 1, 1
        FROM   dbo.LandlordData d
        WHERE  d.Id = @SourceId
          AND  d.PrimaryPOA IS NOT NULL
          AND  DATALENGTH(d.PrimaryPOA) > 0;
        SET @cntDocuments = @cntDocuments + @@ROWCOUNT;

        IF @HasSecondary = 1
        BEGIN
            -- 5c) Secondary Emirates ID -> DocumentType 0 (EmiratesId), ContactId = secondary.
            INSERT INTO leasing.LandlordDocuments
                (LandlordId, DocumentType, FileName, StoredFileName, ContentType, FileSizeBytes,
                 ContentBase64, UploadedAt, UploadedBy, ContactId, [Version], IsCurrent)
            SELECT @LandlordId, 0, N'EmiratesID-Secondary.pdf', N'EmiratesID-Secondary.pdf',
                   N'application/pdf', DATALENGTH(d.SecondaryEmiratesId),
                   CAST(N'' AS XML).value('xs:base64Binary(sql:column("d.SecondaryEmiratesId"))', 'VARCHAR(MAX)'),
                   @CreatedOn, N'LandlordData migration', @SecondaryContactId, 1, 1
            FROM   dbo.LandlordData d
            WHERE  d.Id = @SourceId
              AND  d.SecondaryEmiratesId IS NOT NULL
              AND  DATALENGTH(d.SecondaryEmiratesId) > 0;
            SET @cntDocuments = @cntDocuments + @@ROWCOUNT;

            -- 5d) Secondary POA -> DocumentType 7 (MoaOrPoa), ContactId = secondary.
            INSERT INTO leasing.LandlordDocuments
                (LandlordId, DocumentType, FileName, StoredFileName, ContentType, FileSizeBytes,
                 ContentBase64, UploadedAt, UploadedBy, ContactId, [Version], IsCurrent)
            SELECT @LandlordId, 7, N'POA-Secondary.pdf', N'POA-Secondary.pdf',
                   N'application/pdf', DATALENGTH(d.SecondaryPOA),
                   CAST(N'' AS XML).value('xs:base64Binary(sql:column("d.SecondaryPOA"))', 'VARCHAR(MAX)'),
                   @CreatedOn, N'LandlordData migration', @SecondaryContactId, 1, 1
            FROM   dbo.LandlordData d
            WHERE  d.Id = @SourceId
              AND  d.SecondaryPOA IS NOT NULL
              AND  DATALENGTH(d.SecondaryPOA) > 0;
            SET @cntDocuments = @cntDocuments + @@ROWCOUNT;
        END

        PRINT CONCAT('Migrated LandlordData.Id=', @SourceId,
                     ' -> leasing.Landlords.Id=', @LandlordId,
                     ' (PropertyCode=', @PlotNo, ').');

        -- Next row.
        FETCH NEXT FROM cur INTO
            @SourceId, @AgreementType, @PlotNo, @ContactName, @MobileNumber, @Email,
            @SecondaryContactName, @SecondaryMobileNumber, @SecondaryEmail, @RawCreatedOn,
            @PrimaryEmiratesIdNo, @SecondayEmiratesIdNo, @PrimaryName, @SecondayName,
            @DateOfExpiration, @TenantId, @SignatureType;
    END

    CLOSE cur;
    DEALLOCATE cur;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    -- Close/deallocate the cursor if the failure happened mid-loop.
    IF CURSOR_STATUS('local', 'cur') >= 0
    BEGIN
        CLOSE cur;
        DEALLOCATE cur;
    END

    IF XACT_STATE() <> 0
        ROLLBACK TRANSACTION;

    PRINT 'Migration FAILED and was rolled back. No rows were migrated by this run.';
    THROW;  -- re-raise the original error to the operator
END CATCH

-------------------------------------------------------------------------------------------------
-- Summary.
-------------------------------------------------------------------------------------------------
PRINT '==================================================================';
PRINT 'LandlordData -> leasing migration complete.';
PRINT CONCAT('  Landlords migrated   : ', @cntLandlords);
PRINT CONCAT('  Premises links       : ', @cntPremises);
PRINT CONCAT('  Contacts created     : ', @cntContacts);
PRINT CONCAT('  Signatories created  : ', @cntSignatories);
PRINT CONCAT('  Documents created    : ', @cntDocuments);
PRINT CONCAT('  Rows skipped (no Property match): ', @cntSkippedNoProp);
PRINT '==================================================================';
GO
