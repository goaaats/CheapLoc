using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Mono.Cecil;
using Newtonsoft.Json;
using OpCodes = Mono.Cecil.Cil.OpCodes;

namespace CheapLoc
{
    /// <summary>
    /// Static class providing run-time localization services.
    /// </summary>
    public static class Loc
    {
        private static readonly Dictionary<string, Dictionary<string, LocEntry>> LocData = new();

        /// <summary>
        /// Set-up localization data for the calling assembly with the provided JSON structure.
        /// </summary>
        /// <param name="locData">JSON structure containing a key/LocEntry mapping.</param>
        public static void Setup(string locData)
        {
            Setup(locData, Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Set-up localization data for the provided assembly with the provided JSON structure.
        /// </summary>
        /// <param name="locData">JSON structure containing a key/LocEntry mapping.</param>
        /// <param name="assembly">Assembly to load the localization data for.</param>
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining)]
        public static void Setup(string locData, Assembly assembly)
        {
            var assemblyName = GetAssemblyName(assembly);

            if (LocData.ContainsKey(assemblyName))
                LocData.Remove(assemblyName);

            LocData.Add(assemblyName, JsonConvert.DeserializeObject<Dictionary<string, LocEntry>>(locData));
        }

        /// <summary>
        /// Set-up empty localization data to force all fallbacks to show for the calling assembly.
        /// </summary>
        public static void SetupWithFallbacks()
        {
            Setup("{}", Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Set-up empty localization data to force all fallbacks to show for the provided assembly.
        /// </summary>
        /// <param name="assembly">Assembly to load the localization data for.</param>
        public static void SetupWithFallbacks(Assembly assembly)
        {
            Setup("{}", assembly);
        }

        /// <summary>
        /// Search the set-up localization data for the provided assembly for the given string key and return it.
        /// If the key is not present, the fallback is shown.
        /// The fallback is also required to create the string files to be localized.
        ///
        /// Calling this method should always be the first step in your localization chain.
        /// </summary>
        /// <param name="key">The string key to be returned.</param>
        /// <param name="fallBack">The fallback string, usually your source language.</param>#
        /// <returns>The localized string, fallback or string key if not found.</returns>
        public static string Localize(string key, string fallBack)
        {
            return Localize(key, fallBack, Assembly.GetCallingAssembly());
        }

        /// <summary>
        /// Search the set-up localization data for the calling assembly for the given string key and return it.
        /// If the key is not present, the fallback is shown.
        /// The fallback is also required to create the string files to be localized.
        /// </summary>
        /// <param name="key">The string key to be returned.</param>
        /// <param name="fallBack">The fallback string, usually your source language.</param>#
        /// <param name="assembly">Assembly to load the localization data for.</param>
        /// <returns>The localized string, fallback or string key if not found.</returns>
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining)]
        public static string Localize(string key, string fallBack, Assembly assembly)
        {
            var assemblyName = GetAssemblyName(assembly);

            if (!LocData.ContainsKey(assemblyName))
                return $"#{key}";

            if (!LocData[assemblyName].TryGetValue(key, out var localizedString))
                return string.IsNullOrEmpty(fallBack) ? $"#{key}" : fallBack;

            return string.IsNullOrEmpty(localizedString.Message) ? $"#{key}" : localizedString.Message;
        }

        /// <summary>
        /// Saves localizable JSON data in the current working directory for the provided assembly.
        /// </summary>
        /// <param name="assembly">Assembly to save localization data from.</param>
        /// <param name="ignoreInvalidFunctions">If set to true, this ignores malformed Localize functions instead of failing.</param>
        public static void ExportLocalizableForAssembly(Assembly assembly, bool ignoreInvalidFunctions = false)
        {
            var types = assembly.GetTypes();

            var debugOutput = string.Empty;
            var outList = new Dictionary<string, LocEntry>();

            var assemblyDef = AssemblyDefinition.ReadAssembly(assembly.Location);

            var toInspect = assemblyDef.MainModule.GetTypes()
                .SelectMany(t => t.Methods
                    .Where(m => m.HasBody)
                    .Select(m => new {t, m}));

            foreach (var tm in toInspect)
            {
                var instructions = tm.m.Body.Instructions;

                foreach (var instruction in instructions)
                {
                    if (instruction.OpCode == OpCodes.Call)
                    {
                        var methodInfo = instruction.Operand as MethodReference;

                        if (methodInfo != null)
                        {
                            var methodType = methodInfo.DeclaringType;
                            var parameters = methodInfo.Parameters;

                            if (!methodInfo.Name.Contains("Localize"))
                                continue;

                            debugOutput += string.Format("->{0}.{1}.{2}({3});\n",
                                    tm.t.FullName,
                                    methodType.Name,
                                    methodInfo.Name,
                                    string.Join(", ",
                                        parameters.Select(p =>
                                            p.ParameterType.FullName + " " + p.Name).ToArray())
                                );

                            var entry = new LocEntry
                            {
                                Message = instruction.Previous.Operand as string,
                                Description = $"{tm.t.Name}.{tm.m.Name}"
                            };

                            var key = instruction.Previous.Previous.Operand as string;

                            if (string.IsNullOrEmpty(key))
                            {
                                var errMsg = $"Key was empty for message: {entry.Message} (from {entry.Description}) in {tm.t.FullName}::{tm.m.FullName}";
                                if (ignoreInvalidFunctions)
                                {
                                    debugOutput += $"{errMsg}\n";
                                    continue;
                                }
                                else
                                    throw new Exception(errMsg);
                            }

                            if (outList.Any(x => x.Key == key))
                            {
                                if (outList.Any(x => x.Key == key && x.Value.Message != entry.Message))
                                {
                                    throw new Exception(
                                        $"Message with key {key} has previous appearance but other fallback text in {entry.Description} in {tm.t.FullName}::{tm.m.FullName}");
                                }
                            }
                            else
                            {
                                debugOutput += $"    ->{key} - {entry.Message} (from {entry.Description})\n";
                                outList.Add(key, entry);
                            }
                        }
                    }
                }
            }

            File.WriteAllText("loc.log", debugOutput);
            File.WriteAllText($"{GetAssemblyName(assembly)}_Localizable.json", JsonConvert.SerializeObject(outList,
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                }));

            return;

            /*
            Old version, depended on Mono.Reflection/MethodBaseRocks

            foreach (var type in types.Where(x => x.IsClass || x.IsAbstract))
            {
                var toParse = new List<MethodBase>();
                toParse.AddRange(type.GetTypeInfo().DeclaredConstructors);
                toParse.AddRange(type.GetTypeInfo().DeclaredMethods);

                foreach (var method in toParse)
                    try
                    {
                        var instructions = method.GetInstructions();

                        foreach (var instruction in instructions)
                            if (instruction.OpCode == OpCodes.Call)
                            {
                                var methodInfo = instruction.Operand as MethodInfo;

                                if (methodInfo != null && methodInfo.IsStatic)
                                {
                                    var methodType = methodInfo.DeclaringType;
                                    var parameters = methodInfo.GetParameters();

                                    if (!methodInfo.Name.Contains("Localize"))
                                        continue;

                                    Console.WriteLine("->({0}) {1}.{2}.{3}({4});",
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
                                        Description = $"{type.Name}.{method.Name}"
                                    };

                                    var key = instruction.Previous.Previous.Operand as string;

                                    if (string.IsNullOrEmpty(key))
                                        throw new Exception(
                                            $"Key was empty for message: {entry.Message} (from {entry.Description})");

                                    if (outList.Any(x => x.Key == key))
                                    {
                                        if (outList.Any(x => x.Key == key && x.Value.Message != entry.Message))
                                            throw new Exception(
                                                $"Message with key {key} has previous appearance but other fallback text in {entry.Description}");
                                    }
                                    else
                                    {
                                        Console.WriteLine($"    ->{key} - {entry.Message} (from {entry.Description})");
                                        outList.Add(key, entry);
                                    }
                                }
                            }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Couldn't parse {method.Name}:\n{ex}");
                    }
            }
            */
        }

        /// <summary>
        /// Saves localizable JSON data in the current working directory for the calling assembly.
        /// </summary>
        /// <param name="ignoreInvalidFunctions">If set to true, this ignores malformed Localize functions instead of failing.</param>
        public static void ExportLocalizable(bool ignoreInvalidFunctions = false)
        {
            ExportLocalizableForAssembly(Assembly.GetCallingAssembly(), ignoreInvalidFunctions);
        }

        private static string GetAssemblyName(Assembly assembly) => assembly.GetName().Name;
    }
}