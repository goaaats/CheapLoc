using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

namespace CheapLoc
{
    public static class Loc
    {
        private static Dictionary<string, Dictionary<string, LocEntry>> _locData = new Dictionary<string, Dictionary<string, LocEntry>>();

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining)]
        public static void Setup(string locData)
        {
            var assemblyName = GetCallingAssemblyName(Assembly.GetCallingAssembly());

            if (_locData.ContainsKey(assemblyName))
                throw new ArgumentException("Already loaded localization data for " + assemblyName);

            _locData.Add(assemblyName, JsonConvert.DeserializeObject<Dictionary<string, LocEntry>>(locData));
        }

        [MethodImpl(MethodImplOptions.PreserveSig | MethodImplOptions.NoInlining)]
        public static string Localize(string key, string fallBack)
        {
            var assemblyName = GetCallingAssemblyName(Assembly.GetCallingAssembly());

            if (!_locData.ContainsKey(assemblyName))
                return $"#{key}";

            if (!_locData[assemblyName].TryGetValue(key, out var localizedString))
                return string.IsNullOrEmpty(fallBack) ? $"#{key}" : fallBack;

            return string.IsNullOrEmpty(localizedString.Message) ? $"#{key}" : localizedString.Message;
        }

        private static string GetCallingAssemblyName(Assembly assembly) => assembly.GetName().Name;
    }
}
