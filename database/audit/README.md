# SupplierPortal — SQL Server Audit (KPMG ITGC remediation)

Remediation for the KPMG observation on **incomplete audit logging** and
**lack of a periodic, evidenced log review** for the SupplierPortal `BP`
database.

| File | Purpose |
|---|---|
| `01_Deploy_Audit_BP.sql` | Enables the server audit + specifications on the instance and `BP`. |
| `02_Review_Audit_BP.sql` | Reads the trail — the evidence pack for a periodic independent review. |

## Two notions of "user" in this system — read before deploying

SupplierPortal authenticates its **own** users (cookie auth) as rows in the
`Users` table, and connects to SQL Server as a **single shared login
(`bpuser`)**. So:

- **Application** user / role changes (Add User, changing User Type) are
  `INSERT`/`UPDATE` on `dbo.Users`, performed as `bpuser`. They are captured
  by the **DML** audit (Layer 2) and look like normal app traffic.
- **SQL Server login** changes (a DBA's login, `sa`, server roles) are
  captured by the **login/principal** audit (Layer 1).
- A person editing data in **SSMS** typically also connects as `bpuser`, so
  they are **not** distinguishable from the app by login name — only by
  `application_name` / `client_ip`.

**Action required for direct-edit detection to work:** set
`Application Name=SupplierPortal` in the app's connection string, then keep the
allow-list in query 1 of `02_Review_Audit_BP.sql` in sync. Anything not
carrying that identity is flagged as "outside the application".

## What is (and isn't) audited — and why

| Layer | Captured | Volume |
|---|---|---|
| 1 — SQL logins level | logins, login/role/permission changes, audit tampering | low (~10s–100s MB/day) |
| 2 — data changes on `BP` | every INSERT/UPDATE/DELETE, DB user/role/permission changes, DDL | moderate |
| **Not enabled** | `SELECT` (read) auditing | — |

`SELECT` auditing is deliberately **off**. Read-auditing is what generates the
large volume and CPU cost the business objected to; change-auditing does not.
This directly answers the "makes the application slow" concern while still
capturing every change. If read-auditing of specific sensitive tables is later
required, add targeted `SELECT ON OBJECT::...` lines — never `SELECT` on the
whole database.

## Known limitation

SQL Server Audit records **that** a row changed, by whom, when, and the T-SQL
submitted — but **not the old/new column values**. If you must be able to show
the prior value of an edited row (e.g. a changed supplier bank account),
add **system-versioned temporal tables** on the sensitive tables (`Suppliers`,
`Banks`, `Users`) — Option 1 in the original `03_Before_After_Values.sql`.

## Closing Gap 2 (periodic review)

Run `02_Review_Audit_BP.sql` on a fixed cycle (monthly/quarterly) as someone
**independent of the DBA team**, and retain the output (who reviewed, what
period, findings, escalations) as the audit artefact — the logs alone are not
the evidence the review happened.

## Deploy order

1. Provision a dedicated audit volume (not the data/log drive).
2. Run `01` in **non-production** first; measure growth + CPU over a business day.
3. Apply the STEP 5 tamper protections (file ACLs, off-box forwarding, alerts).
4. Run the STEP 6 smoke test; confirm it appears via `02`.
5. Schedule the health check (every 15 min) and the periodic review.

> These scripts have not been run against a live instance. Validate in
> non-production and have the DBA review scoping before promoting.
