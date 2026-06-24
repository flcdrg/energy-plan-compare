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

By default, only TOU plans are enriched with per-plan detail calls (SR plans are read from the list response). Use `--fetch-all` to fetch details for every plan.

The fetch command now shows a progress bar while loading plans.

## Calculate and rank

```bash
dotnet run --project src/EnergyPlanCompare -- calculate \
  --interval /path/to/MeterDataReport.csv \
  --plans plans.json \
  --smart-meter \
  --top 20
```

Optional eligibility flags: `--ev`, `--battery`, `--pensioner`.
Use `--top` to limit how many ranked plans are displayed.

## Test

```bash
dotnet test EnergyPlanCompare.slnx
```
