using System.CommandLine;
using EnergyPlanCompare.Commands;

var rootCommand = new RootCommand("Compare electricity plans using interval meter data");
rootCommand.Subcommands.Add(FetchCommand.Build());
rootCommand.Subcommands.Add(CalculateCommand.Build());

return await rootCommand.Parse(args).InvokeAsync();
