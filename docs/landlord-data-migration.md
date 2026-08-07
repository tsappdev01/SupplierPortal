# Landlord data migration — legacy `LandlordData` → new `leasing.*` schema

## 1. Context

The legacy landlord registrations live in **`ProdPropertyManagementSystem.dbo.LandlordData`**, a single
flat table that keeps both the landlord information and the uploaded documents (as `varbinary(max)`).

The new module stores the same information across a normalised, audited set of tables in the **`BP`**
database:

| New table | Holds |
|---|---|
| `leasing.Landlords` | one registration per property (snapshot of the Property-View fields, status, audit) |
| `leasing.LandlordContacts` | primary + secondary contacts |
| `leasing.LandlordDocuments` | documents as **Base64** (`ContentBase64 varchar(max)`) — no file system |
| `leasing.LandlordEvents` | audit trail |

## 2. Field mapping

| Legacy `LandlordData` | New location | Notes |
|---|---|---|
| `Id` | `Landlords.LegacySourceId` | traceability + idempotency key |
| `AgreementType` | `Landlords.Type` | `Individual`→0, anything else (`Corporate`)→**Company (1)** |
| `PlotNo` | `Landlords.PropertyCode` | also used to resolve `PropertyId` via `dbo.Property` (view) |
| `ContactName` | `Landlords.CompanyName` | the entity/company name |
| `Email` | `Landlords.Email` | the invitation address (best available from legacy) |
| `PrimaryName` / `FirstName`+`LastName` | primary `LandlordContacts.FullName` | first non-empty wins |
| `MobileNumber`, `Email`, `PrimaryEmiratesIdNo` | primary contact `Mobile`/`Email`/`EmiratesId` | |
| `PrimaryPOA` (varbinary) | `LandlordDocuments` POA, primary | → Base64 |
| `PrimaryEmiratesId` (varbinary) | `LandlordDocuments` Emirates ID, primary | → Base64 |
| `SecondaryContactName`/`SecondayName`, `SecondaryMobileNumber`, `SecondaryEmail`, `SecondayEmiratesIdNo` | secondary `LandlordContacts` | created only when a secondary name exists |
| `SecondaryPOA`, `SecondaryEmiratesId` (varbinary) | `LandlordDocuments` linked to the secondary contact | → Base64 |
| `CreatedOn` | `Landlords.CreatedAt` (+ `SubmittedAt` when verified) | |
| `isverified` / `Emailsent` | `Landlords.Status` | `isverified=1`→**Submitted**, else `Emailsent=1`→**InvitationSent**, else **Draft** |
| `DocumentNumber`, `FirstNameAr`, `LastNameAr`, `DateOfBirth`, `DateOfExpiration`, `DisclaimerAccepted`, `isverifiedsecondary` | *(not migrated)* | legacy UAE-Pass/OCR extras with no target field; kept in the source system for reference |

### Status decision
`isverified = 1` is mapped to **Submitted** (not Registration Complete) so the Business Centre reviews and
formally completes each migrated record in the new workflow — giving a clean audit trail and a completion
notification. Change the `CASE` in step 1 of the script to `8` (RegistrationComplete) if you would rather
treat already-verified legacy records as closed.

### Document content type
Legacy blobs carry no MIME type, so the script infers it from the file's magic bytes (`%PDF`→`application/pdf`,
`0xFFD8FF`→`image/jpeg`, `0x89504E47`→`image/png`, otherwise `application/octet-stream`) and names the stored
file accordingly, so preview/download in the review screen behaves correctly.

## 3. Strategy

1. **Schema first.** The target already carries the migration-created column `Landlords.LegacySourceId`
   (EF migration `LandlordLegacySourceId`) plus the Base64 document column
   (`LandlordDocuments.ContentBase64`, migration `LandlordDocumentBase64`). Apply migrations before running
   the data script (the app applies them automatically at startup).
2. **Same instance vs. cross-instance.** If both databases are on the same SQL Server instance the script's
   three-part name `ProdPropertyManagementSystem.dbo.LandlordData` works as-is. If they are on different
   instances, create a **linked server** and switch to a four-part name.
3. **Set-based, transactional, idempotent.** The script stages the source into a temp table, then inserts
   registrations → contacts → documents → audit events inside one transaction. Every step is guarded by
   `NOT EXISTS` on `LegacySourceId` (and natural keys for children), so **re-running only migrates new/missed
   rows** and never duplicates.
4. **Base64 conversion in-database.** Blobs are converted with
   `CAST(N'' AS XML).value('xs:base64Binary(sql:column("...")) ', 'VARCHAR(MAX)')` — no application round-trip.
5. **Verification.** The script finishes with a counts summary (source rows vs. migrated registrations /
   contacts / documents). Spot-check a few records in the Property List → Review screen and confirm each
   document previews.
6. **Cutover.** Run in a maintenance window (or online — it only inserts). Because it is idempotent you can
   do a **dry run on a copy**, then the real run, then a final delta run after freezing the legacy system.
7. **Rollback.** Everything migrated is tagged by `LegacySourceId`, so a clean rollback is:
   ```sql
   DELETE d FROM leasing.LandlordDocuments d JOIN leasing.Landlords l ON l.Id=d.LandlordId WHERE l.LegacySourceId IS NOT NULL;
   DELETE c FROM leasing.LandlordContacts  c JOIN leasing.Landlords l ON l.Id=c.LandlordId WHERE l.LegacySourceId IS NOT NULL;
   DELETE e FROM leasing.LandlordEvents    e JOIN leasing.Landlords l ON l.Id=e.LandlordId WHERE l.LegacySourceId IS NOT NULL;
   DELETE FROM leasing.Landlords WHERE LegacySourceId IS NOT NULL;
   ```

## 4. Running it

```sql
-- in the BP database
:setvar SourceDb "ProdPropertyManagementSystem"   -- or edit @SourceDb / @BusinessUnitId at the top
-- then execute scripts/migration/migrate-landlorddata.sql
```

Set `@BusinessUnitId` to the business unit these legacy landlords belong to (defaults to 0 = unassigned).

## 5. Post-migration

- Migrated **Submitted** records appear in **Registrations → Under Review** for the Business Centre to
  complete (or send back for correction).
- The `PropertyId` is resolved from `dbo.Property` by `PlotNo`; rows whose plot is not found in the view get
  `PropertyId = 0` — list them with
  `SELECT PropertyCode FROM leasing.Landlords WHERE LegacySourceId IS NOT NULL AND PropertyId = 0;`
  and reconcile the plot codes if any appear.
