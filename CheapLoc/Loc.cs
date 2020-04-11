using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Mono.Reflection;
using Newtonsoft.Json;

namespace CheapLoc
{
    /// <summary>
    /// Static class providing run-time localization services.
    /// </summary>
    public static class Loc
    {
        private static Dictionary<string, Dictionary<string, LocEntry>> _locData = new Dictionary<string, Dictionary<string, LocEntry>>();

        /// <summary>
        /// Set-up localization data for the calling assembly with the provided JSON structure.
        /// </summary>
        /// <param name="locData">JSON structure containing a key/LocEntry mapping.</param>
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining)]
        public static void Setup(string locData)
        {
            var assemblyName = GetAssemblyName(Assembly.GetCallingAssembly());

            if (_locData.ContainsKey(assemblyName))
                throw new ArgumentException("Already loaded localization data for " + assemblyName);

            _locData.Add(assemblyName, JsonConvert.DeserializeObject<Dictionary<string, LocEntry>>(locData));
        }

        /// <summary>
        /// Set-up empty localization data to force all fallbacks to show.
        /// </summary>
        public static void SetupWithFallbacks()
        {
            Setup("{}");
        }

        /// <summary>
        /// Search the set-up localization data for this assembly for the given string key and return it.
        /// If the key is not present, the fallback is shown.
        /// The fallback is also required to create the string files to be localized.
        /// 
        /// Calling this method should always be the last step in your localization chain.
        /// </summary>
        /// <param name="key">The string key to be returned.</param>
        /// <param name="fallBack">The fallback string, usually your source language.</param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining)]
        public static string Localize(string key, string fallBack)
        {
            var assemblyName = GetAssemblyName(Assembly.GetCallingAssembly());

            if (!_locData.ContainsKey(assemblyName))
                return $"#{key}";

            if (!_locData[assemblyName].TryGetValue(key, out var localizedString))
                return string.IsNullOrEmpty(fallBack) ? $"#{key}" : fallBack;

            return string.IsNullOrEmpty(localizedString.Message) ? $"#{key}" : localizedString.Message;
        }

        /// <summary>
        /// Saves localizable JSON data in the current working directory for the provided assembly.
        /// </summary>
        /// <param name="assembly">Assembly to save localization data from.</param>
        public static void ExportLocalizableForAssembly(Assembly assembly)
        {
            var types = assembly.GetTypes();

            var outList = new Dictionary<string, LocEntry>();

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

            File.WriteAllText($"{GetAssemblyName(assembly)}_Localizable.json", JsonConvert.SerializeObject(outList,
                new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented
                }));
        }

        /// <summary>
        /// Saves localizable JSON data in the current working directory for the calling assembly.
        /// </summary>
        public static void ExportLocalizable()
        {
            ExportLocalizableForAssembly(Assembly.GetCallingAssembly());
        }

        private static string GetAssemblyName(Assembly assembly) => assembly.GetName().Name;
    }
}
