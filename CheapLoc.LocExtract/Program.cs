using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Mono.Reflection;
using Newtonsoft.Json;

namespace CheapLoc.LocExtract
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("USAGE: CheapLoc.LocExtract [PATH TO ASSEMBLY]");
                return;
            }

            // Try to load missing assemblies from the local directory of the requesting assembly
            // This would usually be implicit when using Assembly.Load(), but Assembly.LoadFile() doesn't do it...
            // This handler should only be invoked on things that fail regular lookups, but it *is* global to this appdomain
            AppDomain.CurrentDomain.AssemblyResolve += (source, e) =>
            {
                Debug.WriteLine($"Resolving missing assembly {e.Name}");
                // This looks weird but I'm pretty sure it's actually correct.  Pretty sure.  Probably.
                var assemblyPath = Path.Combine(Path.GetDirectoryName(e.RequestingAssembly.Location),
                    new AssemblyName(e.Name).Name + ".dll");
                if (!File.Exists(assemblyPath))
                {
                    Debug.WriteLine($"Assembly not found at {assemblyPath}");
                    return null;
                }

                return Assembly.LoadFrom(assemblyPath);
            };

            var assemblyFile = new FileInfo(args[0]);

            var loadedAssembly = Assembly.LoadFile(assemblyFile.FullName);

            var types = loadedAssembly.GetTypes();

            var outList = new List<LocEntry>();

            foreach (var type in types.Where(x => x.IsClass || x.IsAbstract))
            {
                var toParse = new List<MethodBase>();
                toParse.AddRange(type.GetTypeInfo().DeclaredConstructors);
                toParse.AddRange(type.GetTypeInfo().DeclaredMethods);

                foreach (var method in toParse)
                    try
                    {
                        var instructions = MethodBodyReader.GetInstructions(method);

                        foreach (var instruction in instructions)
                        {
                            if (instruction.OpCode == OpCodes.Call)
                            {
                                var methodInfo = instruction.Operand as MethodInfo;

                                if (methodInfo != null && methodInfo.IsStatic)
                                {
                                    var methodType = methodInfo.DeclaringType;
                                    var parameters = methodInfo.GetParameters();

                                    if (!methodInfo.Name.Contains("Localize"))
                                        continue;

                                    Console.WriteLine("          ->({0}) {1}.{2}.{3}({4});",
                                        method.DeclaringType.Assembly.GetName().Name,
                                        type.FullName,
                                        methodType.Name,
                                        methodInfo.Name,
                                        string.Join(", ",
                                            parameters.Select(p =>
                                                p.ParameterType.FullName + " " + p.Name).ToArray())
                                    );

                                    var entry = new LocEntry
                                    {
                                        Message = instruction.Previous.Operand as string,
                                        Key = instruction.Previous.Previous.Operand as string
                                    };

                                    Console.WriteLine($"{entry.Key} - {entry.Message}");

                                    outList.Add(entry);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Couldn't parse {method.Name}:\n{ex}");
                    }
            }

            File.WriteAllText("out.json", JsonConvert.SerializeObject(outList, new JsonSerializerSettings
            {
                Formatting = Formatting.Indented
            }));

            Console.ReadLine();
        }
    }
}
