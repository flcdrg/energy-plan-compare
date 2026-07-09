# Copilot Instructions

## Build & Test

```bash
dotnet build EnergyPlanCompare.slnx
dotnet test EnergyPlanCompare.slnx

# Run a single test class
dotnet test tests/EnergyPlanCompare.Tests --filter "ClassName=EnergyPlanCompare.Tests.CostCalculatorTests"

# Run a single test method
dotnet test tests/EnergyPlanCompare.Tests --filter "FullyQualifiedName=EnergyPlanCompare.Tests.CostCalculatorTests.RankPlans_SingleRateIncludesFitAndSupplyCharge"
```

The solution file is `EnergyPlanCompare.slnx` (not `.sln` — .NET 10 uses the new XML solution format).

## Architecture

Two-phase workflow: **fetch** (write `plans.json`) → **calculate** (read `plans.json` + interval CSV → ranked output).

### `fetch` command
- Calls the Energy Made Easy list API to get all plans for a postcode
- SR plans are used as-is from the list response (complete pricing data)
- TOU plans (tariff type starts with `TOU`) must be individually fetched because the list endpoint omits `timeOfUse` schedule windows
- By default, keeps only currently available plans (`planStatus == PUBLISHED` and `effectiveDate <= today` when set); use `--include-historical` to keep expired plans

### `calculate` command  
- Parses the interval CSV into two streams: `B1` (solar generation) and `E1` (consumption from grid), each as `Dictionary<DateOnly, decimal[]>` with 288 five-minute slots per day
- **Auto-detects file format**: NEM12 (first line starts with a numeric record indicator like `200,`) or Globird meter report (starts with `Nmi,`)
- Optional `--typical-day yyyy-mm-dd` limits costing to one day from the interval file
- Optional EV forecast mode (`--ev-forecast`) reallocates each day's usage by a configured EV charging window (`--ev-window`) and target window share (`--ev-window-percentage`)
- For each eligible plan: calculates consumption cost, subtracts FiT solar credits, adds daily supply charge, then annualises by `(total / dayCount) * 365`
- Controlled-load plans (tariff type or pricing model containing `CL`) are **excluded by default**; pass `--controlled-load` to include them

### NEM12 format (AEMO MDFF Specification NEM12/NEM13 v2.7)
Supported record types:
- `100` — file header (optional in SAPN Detailed export); skipped
- `200` — NMI Data Details: identifies stream type from **col[4] (NMISuffix)** (`E1`=consumption, `B1`=generation) and interval length from **col[8] (IntervalLength)**: `5`, `15`, or `30` minutes
- `300` — Interval Data: col[1]=date (`yyyyMMdd`), col[2..N+1]=N interval values (N = 288/96/48 depending on interval length), trailing quality fields
- `400` — Interval Events (quality overrides); skipped
- `900` — end of file

**Interval normalisation**: 15-min (96 slots) and 30-min (48 slots) data is expanded to 288 five-minute slots by dividing each value by the expansion factor (3 or 6) and repeating. Total energy is preserved.

## Key Conventions

### API monetary values are in cents
All `unitPrice`, `dailySupplyCharge` values from the API are in **cents/kWh** and **cents/day** respectively. Division by 100 happens only at the `PlanCostResult` output layer in `CostCalculator`.

### TOU time window matching
`TouTimeOfUse.StartTime`/`EndTime` are 4-digit strings in `HHMM` format (e.g. `"0600"`, `"0959"`). Slot index maps to `HHMM` via `(slot * 5 / 60) * 100 + (slot * 5 % 60)`. End times are **inclusive** (`0959` covers slot 119, i.e. 09:55–09:59).

### Tariff period selection
When a plan has multiple `tariffPeriod` entries:
- `CostCalculator.SelectTariffPeriodForDate` first tries to match each usage date to a seasonal period by month/day range using `startDate` and `endDate`
- If no seasonal match is found, `CostCalculator.SelectTariffPeriod` falls back to the period with the latest `startDate`, then `tariffPeriod[0]`

### FiT rate selection
Use `solarFit` entries with `type == "R"` (retailer FiT). Entries with `type == "G"` are legacy government Solar Bonus Scheme rates and are ignored.

### Demand charge plans
Plans where any `tariffPeriod` contains a `demandCharge` array are **always excluded** — our calculator cannot handle demand tariffs (charged on peak kW, not kWh). This matches the Energy Made Easy website behaviour exactly: 4 Alinta "Demand Single Rate" SR plans are excluded; no TOU plans have demand charges. `PlanFilter.FilterDemandPlans()` handles this, called unconditionally in `CalculateCommand`.

### Plan availability filtering (`IsCurrentlyAvailable`)
Only two checks apply when `--include-historical` is not set:
1. `planStatus == "PUBLISHED"` (from detail API only; not present in list response)
2. `effectiveDate <= today` if set

**Tariff period dates are NOT used.** Many valid, currently-offered plans have stale `startDate`/`endDate` values years in the past (e.g. `2019-07-01 to 2019-06-30`). The website shows these plans regardless. Using tariff period dates for filtering incorrectly excluded dozens of valid plans.

### `--typical-day` behavior
- `--typical-day yyyy-mm-dd` filters interval data to a single consumption day before plan costing
- If solar data exists for that date, it is included; if not, costing still proceeds with consumption-only data
- If consumption data for the date is missing, `calculate` fails with a clear error

### EV forecast behavior
- EV forecast mode is enabled with `--ev-forecast`
- It requires both `--ev-window <start>-<end>` and `--ev-window-percentage <0..100>`
- `--ev-window` supports overnight windows (for example `22-6`) and uses start-inclusive/end-exclusive hour bounds
- Daily kWh is preserved, then redistributed so the specified percentage lands inside the EV window

### Eligibility filtering
`EligibilityFilter` maps structured restriction `type` codes to boolean flags on `EligibilityRequirements`. The `calculate` command accepts `--smart-meter`, `--ev`, `--battery`, `--solar`, and `--pensioner` flags.

**Key rules per restriction type:**
- `SM` (smart meter): skip if description contains "WILL INSTALL" (retailer installs, so no user requirement); otherwise require `--smart-meter`.
- `CB` (community/home battery): **always requires `--battery`**. All `CB`-tagged plans are designed for battery owners. The description may use "estimate based on..." language (pricing methodology) or "you must have..." language, but the requirement is the same.
- `SP` (solar plan): only require `--solar` if description contains explicit requirement language. Pricing notes ("estimate based on typical solar system") are ignored.
- `OC` (other conditions, free text): only filter for `NON-PENSIONER` + `--pensioner` check. **Do NOT keyword-scan for "BATTERY" or "EV"** — OC descriptions often mention these as part of plan names (e.g. "Battery Maximiser Terms") or pricing notes, not as hardware requirements.
- All other types (`FF`, `SC`, `SN`, `SO`, `PS`) are currently not filtered.

### Anonymised test fixture
`tests/EnergyPlanCompare.Tests/Fixtures/sample-interval-anonymised.csv` uses placeholder NMI `0000000000` and meter serial `ANON000001` with synthetically generated interval values. The real meter CSV must never be committed. Tests use `TestPaths.Fixture("filename")` to resolve fixture paths from the test output directory.

### Services are injectable for testing
`PlanFetcher` accepts an `HttpClient` constructor argument — inject a `StubHandler` (`HttpMessageHandler` subclass) for unit tests. No live network calls in tests.

### `PlanData` model is shared across contexts
The same `PlanData` record is used for both the list API response and the stored `plans.json`. When adding fields, update `ApiModels.cs` and update all `new PlanData(...)` call sites in tests (positional record constructor).

## Other rules

- Avoid including any personally identifiable information (PII) in test fixtures or documentation. Use anonymized or synthetic data where possible.
