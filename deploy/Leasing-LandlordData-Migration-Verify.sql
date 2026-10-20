/*==============================================================================================
  Leasing-LandlordData-Migration-Verify.sql   (READ-ONLY)

  Run AFTER Leasing-LandlordData-Migration.sql, in the same Landlord database, to confirm the
  portal (leasing.*) is at parity with the source (dbo.LandlordData + the tenant chain). Every
  statement is a SELECT -- it changes nothing. Each result grid is labelled by a "Check" column.

  What "good" looks like:
    * Check 1  : Migrated_Landlords + Not_Migrated_NoProperty == Source_LandlordData_Rows,
                 and Not_Migrated_Other == 0.
    * Checks 3/4/5 (exceptions): return ZERO rows (no signatory / document / signing-rule mismatch).
    * Check 6  : totals line up with your expectations.

  Tenant chain used (same as the migration): LandlordData.PlotNo -> dbo.MaintainTransactionKeys
  (TranCode NOT LIKE '%-%') -> dbo.MaintainTenant -> dbo.MaintainTenantUAEPassEmails.
==============================================================================================*/

SET NOCOUNT ON;

------------------------------------------------------------------------------------------------
-- Reusable resolution: each migrated landlord -> its source row -> its source tenant id.
------------------------------------------------------------------------------------------------
;WITH MigratedTenant AS
(
    SELECT  l.Id              AS LandlordId,
            l.Reference       AS Reference,
            l.PropertyCode    AS PropertyCode,
            l.SigningRule     AS MigratedSigningRule,
            d.Id              AS SourceId,
            d.PlotNo          AS PlotNo,
            ten.TenantId,
            ten.SignatureType
    FROM        leasing.Landlords l
    INNER JOIN  dbo.LandlordData  d  ON d.Id = l.LegacySourceId
    OUTER APPLY (
                   SELECT TOP (1) mt.ID AS TenantId, mt.SignatureType AS SignatureType
                   FROM   dbo.MaintainTransactionKeys k
                   JOIN   dbo.MaintainTenant mt ON mt.TenantCode = k.TenantCode
                   WHERE  k.PropertyCode = d.PlotNo AND k.TranCode NOT LIKE '%-%'
                   ORDER BY mt.ID
               ) ten
)
SELECT * INTO #mt FROM MigratedTenant;

------------------------------------------------------------------------------------------------
-- CHECK 1 — Headline counts.
------------------------------------------------------------------------------------------------
SELECT
    'Check 1: Landlord counts' AS [Check],
    (SELECT COUNT(*) FROM dbo.LandlordData)                                        AS Source_LandlordData_Rows,
    (SELECT COUNT(*) FROM leasing.Landlords WHERE LegacySourceId IS NOT NULL)      AS Migrated_Landlords,
    (SELECT COUNT(*) FROM dbo.LandlordData d
        WHERE NOT EXISTS (SELECT 1 FROM leasing.Landlords l WHERE l.LegacySourceId = d.Id)
          AND NOT EXISTS (SELECT 1 FROM dbo.Property p WHERE p.PropertyCode = d.PlotNo)) AS Not_Migrated_NoProperty,
    (SELECT COUNT(*) FROM dbo.LandlordData d
        WHERE NOT EXISTS (SELECT 1 FROM leasing.Landlords l WHERE l.LegacySourceId = d.Id)
          AND     EXISTS (SELECT 1 FROM dbo.Property p WHERE p.PropertyCode = d.PlotNo)) AS Not_Migrated_Other;

------------------------------------------------------------------------------------------------
-- CHECK 2 — Child-row totals (source expectation vs migrated).
------------------------------------------------------------------------------------------------
SELECT
    'Check 2: Child-row totals' AS [Check],
    -- signatories expected from the tenant chain (only for landlords that resolved a tenant)
    (SELECT COUNT(*)
       FROM #mt m
       JOIN dbo.MaintainTenantUAEPassEmails u
            ON u.MaintainTenantId = m.TenantId AND u.IsActive = 1 AND u.IsDeleted = 0
      WHERE m.TenantId IS NOT NULL)                                                AS Source_Signatories_FromTenant,
    (SELECT COUNT(*) FROM leasing.LandlordSignatories s
        JOIN leasing.Landlords l ON l.Id = s.LandlordId AND l.LegacySourceId IS NOT NULL) AS Migrated_Signatories,
    -- documents expected = non-empty source binaries on migrated rows
    (SELECT SUM(
         CASE WHEN d.PrimaryEmiratesId   IS NOT NULL AND DATALENGTH(d.PrimaryEmiratesId)   > 0 THEN 1 ELSE 0 END
       + CASE WHEN d.PrimaryPOA          IS NOT NULL AND DATALENGTH(d.PrimaryPOA)          > 0 THEN 1 ELSE 0 END
       + CASE WHEN d.SecondaryEmiratesId IS NOT NULL AND DATALENGTH(d.SecondaryEmiratesId) > 0 THEN 1 ELSE 0 END
       + CASE WHEN d.SecondaryPOA        IS NOT NULL AND DATALENGTH(d.SecondaryPOA)        > 0 THEN 1 ELSE 0 END)
       FROM #mt m JOIN dbo.LandlordData d ON d.Id = m.SourceId)                    AS Source_Documents_NonEmpty,
    (SELECT COUNT(*) FROM leasing.LandlordDocuments doc
        JOIN leasing.Landlords l ON l.Id = doc.LandlordId AND l.LegacySourceId IS NOT NULL) AS Migrated_Documents,
    (SELECT COUNT(*) FROM leasing.LandlordContacts c
        JOIN leasing.Landlords l ON l.Id = c.LandlordId AND l.LegacySourceId IS NOT NULL)   AS Migrated_Contacts;

------------------------------------------------------------------------------------------------
-- CHECK 3 — EXCEPTIONS: signatory-count mismatches (expect ZERO rows).
--   For landlords that resolved a tenant, migrated signatory count must equal the active
--   source UAE-Pass rows for that tenant. (Fallback landlords with no tenant are not checked
--   here because their signatories came from LandlordData, not the tenant chain.)
------------------------------------------------------------------------------------------------
SELECT  'Check 3: Signatory mismatch' AS [Check],
        m.LandlordId, m.Reference, m.SourceId, m.PlotNo, m.TenantId,
        (SELECT COUNT(*) FROM dbo.MaintainTenantUAEPassEmails u
            WHERE u.MaintainTenantId = m.TenantId AND u.IsActive = 1 AND u.IsDeleted = 0) AS Source_Signatories,
        (SELECT COUNT(*) FROM leasing.LandlordSignatories s WHERE s.LandlordId = m.LandlordId) AS Migrated_Signatories
FROM    #mt m
WHERE   m.TenantId IS NOT NULL
  AND   (SELECT COUNT(*) FROM dbo.MaintainTenantUAEPassEmails u
            WHERE u.MaintainTenantId = m.TenantId AND u.IsActive = 1 AND u.IsDeleted = 0)
      <> (SELECT COUNT(*) FROM leasing.LandlordSignatories s WHERE s.LandlordId = m.LandlordId)
ORDER BY m.LandlordId;

------------------------------------------------------------------------------------------------
-- CHECK 4 — EXCEPTIONS: document-count mismatches (expect ZERO rows).
------------------------------------------------------------------------------------------------
SELECT  'Check 4: Document mismatch' AS [Check],
        m.LandlordId, m.Reference, m.SourceId, m.PlotNo,
        ( CASE WHEN d.PrimaryEmiratesId   IS NOT NULL AND DATALENGTH(d.PrimaryEmiratesId)   > 0 THEN 1 ELSE 0 END
        + CASE WHEN d.PrimaryPOA          IS NOT NULL AND DATALENGTH(d.PrimaryPOA)          > 0 THEN 1 ELSE 0 END
        + CASE WHEN d.SecondaryEmiratesId IS NOT NULL AND DATALENGTH(d.SecondaryEmiratesId) > 0 THEN 1 ELSE 0 END
        + CASE WHEN d.SecondaryPOA        IS NOT NULL AND DATALENGTH(d.SecondaryPOA)        > 0 THEN 1 ELSE 0 END) AS Source_Documents,
        (SELECT COUNT(*) FROM leasing.LandlordDocuments doc WHERE doc.LandlordId = m.LandlordId) AS Migrated_Documents
FROM    #mt m
JOIN    dbo.LandlordData d ON d.Id = m.SourceId
WHERE   ( CASE WHEN d.PrimaryEmiratesId   IS NOT NULL AND DATALENGTH(d.PrimaryEmiratesId)   > 0 THEN 1 ELSE 0 END
        + CASE WHEN d.PrimaryPOA          IS NOT NULL AND DATALENGTH(d.PrimaryPOA)          > 0 THEN 1 ELSE 0 END
        + CASE WHEN d.SecondaryEmiratesId IS NOT NULL AND DATALENGTH(d.SecondaryEmiratesId) > 0 THEN 1 ELSE 0 END
        + CASE WHEN d.SecondaryPOA        IS NOT NULL AND DATALENGTH(d.SecondaryPOA)        > 0 THEN 1 ELSE 0 END)
      <> (SELECT COUNT(*) FROM leasing.LandlordDocuments doc WHERE doc.LandlordId = m.LandlordId)
ORDER BY m.LandlordId;

------------------------------------------------------------------------------------------------
-- CHECK 5 — EXCEPTIONS: signing-rule mismatches (expect ZERO rows).
--   Migrated SigningRule (0=All, 1=Any) must equal the tenant's SignatureType ('OR' -> 1 else 0).
--   Only landlords that resolved a tenant are checked.
------------------------------------------------------------------------------------------------
SELECT  'Check 5: Signing-rule mismatch' AS [Check],
        m.LandlordId, m.Reference, m.PlotNo, m.TenantId,
        m.SignatureType                                          AS Source_SignatureType,
        m.MigratedSigningRule                                    AS Migrated_SigningRule,
        CASE WHEN UPPER(LTRIM(RTRIM(m.SignatureType))) = 'OR' THEN 1 ELSE 0 END AS Expected_SigningRule
FROM    #mt m
WHERE   m.TenantId IS NOT NULL
  AND   m.MigratedSigningRule <> CASE WHEN UPPER(LTRIM(RTRIM(m.SignatureType))) = 'OR' THEN 1 ELSE 0 END
ORDER BY m.LandlordId;

------------------------------------------------------------------------------------------------
-- CHECK 6 — Per-landlord spot-check sample (first 50 migrated landlords, for eyeballing).
------------------------------------------------------------------------------------------------
SELECT TOP (50)
        'Check 6: Sample' AS [Check],
        m.LandlordId, m.Reference, m.PlotNo, m.TenantId,
        m.MigratedSigningRule,
        (SELECT COUNT(*) FROM leasing.LandlordContacts    c WHERE c.LandlordId = m.LandlordId) AS Contacts,
        (SELECT COUNT(*) FROM leasing.LandlordSignatories s WHERE s.LandlordId = m.LandlordId) AS Signatories,
        (SELECT COUNT(*) FROM leasing.LandlordDocuments   g WHERE g.LandlordId = m.LandlordId) AS Documents
FROM    #mt m
ORDER BY m.LandlordId;

DROP TABLE #mt;
