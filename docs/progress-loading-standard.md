# Progress / Loading Indicator Standard

The application uses a standard set of progress and loading indicators. Pick the
indicator that matches the **nature, duration and measurability** of the
operation — never a single indicator for everything. Each one should tell the
user *what is happening*, *whether the system is still working*, *how much is
done* (where measurable), *what stage they are at*, and *whether they need to
wait or act*.

All components live in `src/SupplierPortal.Web/Components/Shared` and are styled
globally by `src/SupplierPortal.Web/wwwroot/di-progress.css`. They are available
in any component (registered in `Components/_Imports.razor`).

## Selection rules

| Scenario | Component | Notes |
| --- | --- | --- |
| Short operation, unknown duration | `DiSpinner` | Immediate "processing" feedback. |
| Known percentage completion | `DiLinearProgress` | Determinate bar with `Value` 0–100. |
| File upload / download | `DiLinearProgress` or `DiCircularProgress` | Show completed / remaining. |
| Long op, unknown completion % | `DiIndeterminateProgress` | Animated bar, no number. |
| Loading page / content / data | `DiSkeleton` | `Variant` = list / table / card. |
| Save / Submit / Approve / Pay | `DiLoadingButton` | Spinner + busy label; **disabled while busy → no duplicate submits**. |
| Critical / blocking operation | `DiBlockingOverlay` via `LoadingService` | Disables the whole screen. |
| Multi-step business process | `DiStepper` | `Steps` + `Current` index. |
| Processing / Pending / Completed / Failed … | `DiStatus` | `Kind` = `DiStatusKind.*`. |
| Complex / long-running workflow | `DiMultiPhaseProgress` | List of `DiPhase` rows with status + note. |

## Usage

```razor
@* Spinner — short unknown-duration op *@
<DiSpinner Block="true" Title="Processing…" Sub="Please wait…" />
<DiSpinner Size="sm" />   @* inline, e.g. beside text *@

@* Determinate progress *@
<DiLinearProgress Value="70" Title="Uploading file…" Detail="5.6 MB of 8 MB" />
<DiCircularProgress Value="65" Caption="Uploading Document…" Sub="65% complete" />

@* Unknown-% long op *@
<DiIndeterminateProgress Title="Importing supplier data…" />

@* Skeleton while data loads *@
<DiSkeleton Variant="table" Rows="6" Columns="5" />

@* Transactional button — prevents duplicate clicks *@
<DiLoadingButton Variant="primary" Text="Submit for Approval"
                 BusyText="Submitting…" Busy="@isSaving" OnClick="SubmitAsync" />

@* Stepper / status / phases *@
<DiStepper Steps="steps" Current="2" />
<DiStatus Kind="DiStatusKind.Completed" Sub="Action completed successfully." />
<DiMultiPhaseProgress Phases="phases" />
```

### Blocking overlay (critical operations)

Inject `LoadingService` (scoped, registered in `Program.cs`) and wrap the work.
The overlay is rendered once in `MainLayout` — you only drive the service:

```razor
@inject LoadingService LoadingService

await LoadingService.RunAsync(
    () => DoCriticalWorkAsync(),
    title: "Processing payment…",
    sub: "Please do not close this page.");
```

`RunAsync` always hides the overlay afterwards, even if the work throws. Use
`Show()` / `Hide()` directly if you need finer control.

## Where it's wired today

- **Supplier registration/edit** (`Components/Supplier/SupplierForm.razor`) —
  Next / Save as Draft / Submit for Approval / Save Changes use
  `DiLoadingButton`; only the clicked button spins and all are disabled while a
  save is in flight.
- **Supplier upload** (`Components/Supplier/Upload.razor`) —
  `DiIndeterminateProgress` during the Excel import; the file input is locked
  while importing.
- **Reference gallery** — `Administrator → UI Components`
  (`/admin/ui-components`) shows every indicator live plus the selection rules.

Adopt the components incrementally elsewhere (dashboard/listing skeletons,
export buttons, admin forms) following the table above.
