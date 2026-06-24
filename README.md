# energy-plan-compare
Tool for comparing Australian energy plans

## Build

```bash
dotnet build EnergyPlanCompare.slnx
```

## Fetch plans

```bash
dotnet run --project src/EnergyPlanCompare -- fetch \
  --url "https://api.energymadeeasy.gov.au/consumerplan/plans?usageDataSource=noUsageFrontier&customerType=R&distE=&distG=&fuelType=E&journey=E&postcode=YOUR_POSTCODE" \
  --postcode YOUR_POSTCODE \
  --output plans.json
```

By default, fetch stores only **currently available** plans (published + active tariff dates), which requires per-plan detail lookups.
Use `--include-historical` to keep historical/expired plans. `--fetch-all` is also available to force detail fetches in any mode.

The fetch command now shows a progress bar while loading plans.

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
