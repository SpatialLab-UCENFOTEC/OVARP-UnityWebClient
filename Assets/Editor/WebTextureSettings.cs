using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Applies Web platform texture overrides to the avatar art. Source textures are authored at
/// 2048 with no per-platform override, which dominates the download size of the build.
/// </summary>
public static class WebTextureSettings
{
    private const string PlatformName = "WebGL";
    private const string AvatarFolder = "Assets/Avatars";
    private const int MaxTextureSize = 1024;
    private const int CompressionQuality = 50;

    [MenuItem("Tools/OVARP/Apply Web Texture Settings")]
    public static void Apply()
    {
        string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { AvatarFolder });
        int changed = 0;

        foreach (string path in guids.Select(AssetDatabase.GUIDToAssetPath))
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;

            var settings = new TextureImporterPlatformSettings
            {
                name = PlatformName,
                overridden = true,
                maxTextureSize = MaxTextureSize,
                format = TextureImporterFormat.Automatic,
                textureCompression = TextureImporterCompression.Compressed,
                compressionQuality = CompressionQuality,
                crunchedCompression = true
            };

            importer.SetPlatformTextureSettings(settings);
            importer.SaveAndReimport();
            changed++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[WebTextureSettings] Applied {PlatformName} overrides to {changed} texture(s) in {AvatarFolder}.");
    }
}
