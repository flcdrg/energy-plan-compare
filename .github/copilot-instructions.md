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
- By default, keeps only currently available plans (`planStatus == PUBLISHED` + at least one active `tariffPeriod` for today's date); use `--include-historical` to keep expired ones

### `calculate` command  
- Parses the interval CSV into two streams: `B1` (solar generation) and `E1` (consumption from grid), each as `Dictionary<DateOnly, decimal[]>` with 288 five-minute slots per day
- For each eligible plan: calculates consumption cost, subtracts FiT solar credits, adds daily supply charge, then annualises by `(total / dayCount) * 365`
- Controlled-load plans (tariff type or pricing model containing `CL`) are **excluded by default**; pass `--controlled-load` to include them

## Key Conventions

### API monetary values are in cents
All `unitPrice`, `dailySupplyCharge` values from the API are in **cents/kWh** and **cents/day** respectively. Division by 100 happens only at the `PlanCostResult` output layer in `CostCalculator`.

### TOU time window matching
`TouTimeOfUse.StartTime`/`EndTime` are 4-digit strings in `HHMM` format (e.g. `"0600"`, `"0959"`). Slot index maps to `HHMM` via `(slot * 5 / 60) * 100 + (slot * 5 % 60)`. End times are **inclusive** (`0959` covers slot 119, i.e. 09:55–09:59).

### Tariff period selection
When a plan has multiple `tariffPeriod` entries (seasonal or successive), `CostCalculator.SelectTariffPeriod` uses the one with the latest `startDate`, falling back to `tariffPeriod[0]` if none have dates.

### FiT rate selection
Use `solarFit` entries with `type == "R"` (retailer FiT). Entries with `type == "G"` are legacy government Solar Bonus Scheme rates and are ignored.

### Demand charge plans
Plans where any `tariffPeriod` contains a `demandCharge` array are **always excluded** — our calculator cannot handle demand tariffs (charged on peak kW, not kWh). This matches the Energy Made Easy website behaviour exactly: 4 Alinta "Demand Single Rate" SR plans are excluded; no TOU plans have demand charges. `PlanFilter.FilterDemandPlans()` handles this, called unconditionally in `CalculateCommand`.

### Plan availability filtering (`IsCurrentlyAvailable`)
Only two checks apply when `--include-historical` is not set:
1. `planStatus == "PUBLISHED"` (from detail API only; not present in list response)
2. `effectiveDate <= today` if set

**Tariff period dates are NOT used.** Many valid, currently-offered plans have stale `startDate`/`endDate` values years in the past (e.g. `2019-07-01 to 2019-06-30`). The website shows these plans regardless. Using tariff period dates for filtering incorrectly excluded dozens of valid plans.

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
