using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Every texture in Assets/Art follows asset-spec.md: PPU 32, Point filter, no compression, full-rect meshes.
// Pivots: tiles, projectiles and UI are centred; everything else stands on its feet (bottom-centre).
// Rig parts (Assets/Art/Animations/<Name>/...) get their joint pivots from RigBuilder, so they are left alone.
// UI frames named *_b<N>.png are 9-sliced with an N-pixel border. Tiles stay readable (the map paints roads from them).
public class PixelArtImporter : AssetPostprocessor
{
    public override uint GetVersion() => 4;

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Art/") || assetPath.Contains("/Source/")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 32;
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.isReadable = assetPath.Contains("/Tiles/") || assetPath.EndsWith("_flame.png");   // flames: the map finds their foot
        ti.maxTextureSize = 4096;

        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        if (!assetPath.Contains("/Animations/"))
        {
            bool centered = assetPath.Contains("/Tiles/") || assetPath.Contains("/Projectiles/") || assetPath.Contains("/UI/");
            s.spriteAlignment = (int)(centered ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
        }
        var m = Regex.Match(assetPath, @"_b(\d+)\.png$");
        if (m.Success)
        {
            int b = int.Parse(m.Groups[1].Value);
            s.spriteBorder = new Vector4(b, b, b, b);
        }
        ti.SetTextureSettings(s);
    }

    // Source art (the user's originals) is kept as plain textures, never as game sprites.
    void OnPreprocessAudio()
    {
        if (!assetPath.StartsWith("Assets/Audio/")) return;
        var ai = (AudioImporter)assetImporter;
        var s = ai.defaultSampleSettings;
        bool longClip = assetPath.Contains("/Music/") || assetPath.Contains("/Ambient/");
        s.loadType = longClip ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = longClip ? 0.6f : 0.8f;
        ai.defaultSampleSettings = s;
        ai.forceToMono = !longClip;
    }
}
