using System.IO;
using UnityEditor;
using UnityEngine;

namespace Rocket.EditorTools
{
    /// <summary>Reports how the part texture array was imported (layers, size), for checking the layer order.</summary>
    public static class TexDebug
    {
        public static void Dump()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var name in new[] { "PartDetailA", "PartDetailN" })
            {
                string path = $"Assets/Game/Resources/Textures/{name}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var imp = (TextureImporter)AssetImporter.GetAtPath(path);
                var tex = Resources.Load<Texture2DArray>("Textures/" + name);
                sb.AppendLine($"{name}: shape {imp.textureShape}, settings rows/cols via SerializedObject:");
                var so = new SerializedObject(imp);
                sb.AppendLine($"  flipbookRows {so.FindProperty("m_FlipbookRows")?.intValue} flipbookColumns {so.FindProperty("m_FlipbookColumns")?.intValue} maxSize {imp.maxTextureSize}");
                sb.AppendLine(tex == null ? "  (no array loaded)" : $"  array {tex.width}x{tex.height} depth {tex.depth} format {tex.format} readable {tex.isReadable}");
                if (tex != null && name == "PartDetailA")
                {
                    // average colour of each layer from its smallest mip (copied to a readable texture)
                    int mip = Mathf.Max(0, tex.mipmapCount - 4);
                    for (int l = 0; l < tex.depth; l++)
                    {
                        var t2 = new Texture2D(Mathf.Max(1, tex.width >> mip), Mathf.Max(1, tex.height >> mip), tex.format, false);
                        Graphics.CopyTexture(tex, l, mip, t2, 0, 0);
                        var raw = t2.GetPixels();
                        Color avg = Color.black;
                        foreach (var c in raw) avg += c;
                        avg /= Mathf.Max(1, raw.Length);
                        sb.AppendLine($"  layer {l}: avg {avg.r:0.00} {avg.g:0.00} {avg.b:0.00}");
                    }
                }
            }
            File.WriteAllText("TestResults/texdebug.txt", sb.ToString());
            EditorApplication.Exit(0);
        }
    }
}
