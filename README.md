# energy-plan-compare
Tool for comparing Australian energy plans

## Build

```bash
dotnet build EnergyPlanCompare.slnx
```

## Fetch plans

```bash
dotnet run --project src/EnergyPlanCompare -- fetch \
  --postcode YOUR_POSTCODE \
  --output plans.json
```

The `--postcode` option is required. The list URL is automatically built from the postcode; pass `--url` to override it.

By default, fetch stores only **currently available** plans (published status and effective date not in the future), which requires per-plan detail lookups.
Use `--include-historical` to keep historical/expired plans. `--fetch-all` is also available to force detail fetches in any mode.

## Calculate and rank

```bash
dotnet run --project src/EnergyPlanCompare -- calculate \
  --interval /path/to/MeterDataReport.csv \
  --plans plans.json \
  --smart-meter \
  --top 20 \
  --url \
  --controlled-load
```

Optional eligibility flags: `--ev`, `--battery`, `--pensioner`.
Use `--top` to limit how many ranked plans are displayed.
Use `--url` to include a direct Energy Made Easy URL for each displayed plan.
Controlled-load plans are excluded by default; use `--controlled-load` to include them.
The `Total` column is an estimated annual cost based on the provided interval sample.

## Test

```bash
dotnet test EnergyPlanCompare.slnx
```
