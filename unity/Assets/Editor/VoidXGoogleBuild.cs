using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace VoidX
{
    public sealed class VoidXGoogleBuild : IPostGenerateGradleAndroidProject
    {
        const string ConfigPath = "Assets/Resources/VoidXGoogleConfig.json";
        [Serializable] sealed class Config { public string webClientId; }
        public int callbackOrder => 20;

        public static void RequireConfigured()
        {
            string clientId = Environment.GetEnvironmentVariable("VOIDX_GOOGLE_WEB_CLIENT_ID");
            if (string.IsNullOrWhiteSpace(clientId) && File.Exists(ConfigPath)) clientId = JsonUtility.FromJson<Config>(File.ReadAllText(ConfigPath))?.webClientId;
            if (string.IsNullOrWhiteSpace(clientId) || !Regex.IsMatch(clientId, @"^\d+-[A-Za-z0-9_-]+\.apps\.googleusercontent\.com$"))
                throw new InvalidOperationException("Configure the VoidX Web OAuth client ID in VOIDX_GOOGLE_WEB_CLIENT_ID before building Google sign-in. Never put an OAuth client secret in the game.");
            File.WriteAllText(ConfigPath, JsonUtility.ToJson(new Config { webClientId = clientId }, true));
            AssetDatabase.ImportAsset(ConfigPath, ImportAssetOptions.ForceSynchronousImport);
        }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // Credential Manager 1.6 uses the API 36 compile SDK; keep the game's target SDK unchanged.
            foreach (var module in new[] { path, Path.Combine(path, "..", "launcher") }) {
                string file = Path.Combine(module, "build.gradle");
                if (!File.Exists(file)) continue;
                string gradle = File.ReadAllText(file);
                gradle = Regex.Replace(gradle, @"(?m)^(\s*compileSdk(?:Version)?\s+)\d+", "${1}36");
                // JVM jar indexes are unused on Android and duplicated by Google's auth jars.
                if (!gradle.Contains("META-INF/INDEX.LIST"))
                    gradle += "\nandroid { packaging { resources { excludes += ['META-INF/INDEX.LIST'] } } }\n";
                File.WriteAllText(file, gradle);
            }
        }
    }
}
