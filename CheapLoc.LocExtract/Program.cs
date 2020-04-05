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

            File.WriteAllText("out.json", JsonConvert.SerializeObject(outList));

            Console.ReadLine();
        }
    }
}
