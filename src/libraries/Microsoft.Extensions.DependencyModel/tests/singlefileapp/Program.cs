using System;
using System.Linq;
using Microsoft.Extensions.DependencyModel;

try
{
    var context = DependencyContext.Default ?? throw new InvalidOperationException("DependencyContext.Default returned null");
    Console.WriteLine($@"Target.Framework: {context.Target.Framework}");
    Console.WriteLine($@"Target.Runtime: {context.Target.Runtime}");
    Console.WriteLine($@"Target.RuntimeSignature: {context.Target.RuntimeSignature}");
    Console.WriteLine($@"Target.IsPortable: {context.Target.IsPortable}");
    foreach (var runtimeLibrary in context.RuntimeLibraries.OrderBy(e => e.Name))
    {
        Console.WriteLine($@"{runtimeLibrary.Name}: {runtimeLibrary.Version}");
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception);
}
