using CommandLine;
using CommandLine.Text;
using nier_rein_code_diff;
using System.Collections.Generic;
using System;
using System.IO;

var parser = new Parser(parserSettings => parserSettings.AutoHelp = true);

var parsedResult = parser.ParseArguments<Options>(args);

parsedResult
    .WithNotParsed(errors => DisplayHelp(parsedResult, errors))
    .WithParsed(Execute);

void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errors)
{
    var helpText = HelpText.AutoBuild(result, h =>
    {
        h.AdditionalNewLineAfterOption = false;
        return HelpText.DefaultParsingErrorsHandler(result, h);
    }, e => e);

    Console.WriteLine(helpText);
}

void Execute(Options o)
{
    if (!File.Exists(o.Input))
    {
        Console.WriteLine($"The input {o.Input} does not exist.");
        return;
    }

    if (!Directory.Exists(o.Output))
    {
        Console.WriteLine($"The output {o.Output} does not exist.");
        return;
    }

    var differ = new DatabaseDiffer(o.Input, o.Output);
    var diff = differ.CreateDiff();

    if (o.Verbose)
        diff.Print();

    var applier = new PatchApplier(o.Input, o.Output);
    applier.Apply(diff);
}
