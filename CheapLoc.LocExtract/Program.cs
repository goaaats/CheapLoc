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
            AppDomain.CurrentDomain.AssemblyResolve += (object source, ResolveEventArgs e) =>
            {
                Console.WriteLine($"Resolving missing assembly {e.Name}");
                // This looks weird but I'm pretty sure it's actually correct.  Pretty sure.  Probably.
                var assemblyPath = Path.Combine(Path.GetDirectoryName(e.RequestingAssembly.Location), new AssemblyName(e.Name).Name + ".dll");
                if (!File.Exists(assemblyPath))
                {
                    Console.WriteLine($"Assembly not found at {assemblyPath}");
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
                Console.WriteLine(type.FullName);

                var toParse = new List<MethodBase>();
                toParse.AddRange(type.GetTypeInfo().DeclaredConstructors);
                toParse.AddRange(type.GetTypeInfo().DeclaredMethods);

                foreach (var method in toParse)
                {
                    Console.WriteLine("     ->" + method.Name);

                    try
                    {
                        var instructions = MethodBodyReader.GetInstructions(method);

                        var lastCall = new Stack<string>();

                        foreach (Instruction instruction in instructions)
                        {
                            if (instruction.OpCode == OpCodes.Ldstr)
                            {
                                lastCall.Push(instruction.Operand.ToString());
                                continue;
                            }

                            if (instruction.OpCode == OpCodes.Call)
                            {
                                MethodInfo methodInfo = instruction.Operand as MethodInfo;

                                if (methodInfo != null && methodInfo.IsStatic)
                                {
                                    var methodType = methodInfo.DeclaringType;
                                    ParameterInfo[] parameters = methodInfo.GetParameters();

                                    if (!methodInfo.Name.Contains("Localize"))
                                        continue;

                                    Console.WriteLine("          ->({0}) {1}.{2}.{3}({4});",
                                        method.DeclaringType.Assembly.GetName().Name,
                                        type.FullName,
                                        methodType.Name,
                                        methodInfo.Name,
                                        String.Join(", ",
                                            parameters.Select(p =>
                                                p.ParameterType.FullName + " " + p.Name).ToArray())
                                    );

                                    outList.Add(new LocEntry
                                    {
                                        Message = lastCall.Pop(),
                                        Key = lastCall.Pop()
                                    });

                                    lastCall.Clear();
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Couldn't parse {method.Name}:\n{ex}");
                    }
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
