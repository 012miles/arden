using UnityEditor;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>
    /// Import settings for the downloaded art (milestone 20): the part detail strips become Texture2DArrays (one layer per
    /// material), and NASA's star map becomes an HDR cubemap.
    /// </summary>
    public class GraphicsImport : AssetPostprocessor
    {
        /// <summary>Milestone 21: music streams from disk (Vorbis); short effects load decompressed, long loops stay compressed.</summary>
        void OnPreprocessAudio()
        {
            string p = assetPath.Replace('\\', '/');
            if (!p.Contains("Resources/Audio/")) return;
            var ai = (AudioImporter)assetImporter;
            var s = ai.defaultSampleSettings;
            bool music = p.Contains("/Music/");
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = music ? 0.55f : 0.7f;
            s.loadType = music ? AudioClipLoadType.Streaming : (p.EndsWith("_amb.wav") || p.Contains("crickets") || p.Contains("engine_vac") || p.Contains("/wind")) ? AudioClipLoadType.CompressedInMemory : AudioClipLoadType.DecompressOnLoad;
            s.preloadAudioData = !music;
            ai.defaultSampleSettings = s;
            ai.forceToMono = false;
            ai.loadInBackground = music;
        }

        void OnPreprocessTexture()
        {
            var ti = (TextureImporter)assetImporter;
            string p = assetPath.Replace('\\', '/');
            bool part = p.EndsWith("Resources/Textures/PartDetailA.png") || p.EndsWith("Resources/Textures/PartDetailN.png");
            bool ground = p.EndsWith("Resources/Textures/TerrainDetailA.png") || p.EndsWith("Resources/Textures/TerrainDetailN.png");
            if (part || ground)
            {
                bool normal = p.EndsWith("N.png");
                ti.textureType = TextureImporterType.Default;
                ti.textureShape = TextureImporterShape.Texture2DArray;
                var s = new TextureImporterSettings();
                ti.ReadTextureSettings(s);
                s.flipbookRows = ground ? 8 : 6;        // parts (milestone 24): 12 layers in a 2-wide grid (Tools/art/build_part_textures.py)
                s.flipbookColumns = ground ? 1 : 2;
                ti.SetTextureSettings(s);
                ti.sRGBTexture = !normal && !ground;   // normals, roughness and the ground's relative colour stay linear
                ti.alphaSource = normal ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
                ti.alphaIsTransparency = false;
                ti.mipmapEnabled = true;
                ti.wrapMode = TextureWrapMode.Repeat;
                ti.filterMode = FilterMode.Trilinear;
                ti.anisoLevel = 8;
                ti.maxTextureSize = 8192;          // the strips are 1024 wide, one 1024 square per layer
                ti.textureCompression = TextureImporterCompression.CompressedHQ;
            }
            else if (p.EndsWith("Resources/Sky/StarMap.exr"))
            {
                ti.textureType = TextureImporterType.Default;
                ti.textureShape = TextureImporterShape.TextureCube;
                ti.generateCubemap = TextureImporterGenerateCubemap.Cylindrical;   // latitude-longitude layout
                ti.sRGBTexture = false;
                ti.mipmapEnabled = true;
                ti.filterMode = FilterMode.Trilinear;
                ti.maxTextureSize = 2048;          // per face: about one texel per screen pixel
                ti.textureCompression = TextureImporterCompression.CompressedHQ;      // BC6H
            }
        }
    }
}
