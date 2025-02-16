using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace WalkSimulator.Tools
{
    public static class AssetUtils
    {
        private static string FormatPath(string path)
        {
            return path.Replace("/", ".").Replace("\\", ".");
        }

        public static AssetBundle LoadAssetBundle(string path)
        {
            path = FormatPath(path);
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream manifestResourceStream = assembly.GetManifestResourceStream(path);

            if (manifestResourceStream == null)
            {
                Debug.LogError($"Failed to load asset bundle from path: {path}");
                return null;
            }

            AssetBundle result = AssetBundle.LoadFromStream(manifestResourceStream);
            manifestResourceStream.Close();
            return result;
        }

        public static string[] GetResourceNames()
        {
            Assembly callingAssembly = Assembly.GetCallingAssembly();
            string[] manifestResourceNames = callingAssembly.GetManifestResourceNames();

            if (manifestResourceNames == null || manifestResourceNames.Length == 0)
            {
                Debug.LogWarning("No manifest resources found.");
                return Array.Empty<string>(); // Return an empty array instead of null
            }

            return manifestResourceNames;
        }
    }
}
