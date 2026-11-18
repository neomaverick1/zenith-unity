// How the game's sounds and pictures are imported: sounds decoded once at load so the music loops stay locked
// together and effects start instantly; icons and art kept crisp without mipmaps.
using UnityEditor;
using UnityEngine;

namespace Zenith.EditorTools
{
    public class Importers : AssetPostprocessor
    {
        void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith("Assets/Zenith/Resources/Audio")) return;
            var a = (AudioImporter)assetImporter;
            a.forceToMono = false;
            a.loadInBackground = false;
            var s = a.defaultSampleSettings;
            s.loadType = AudioClipLoadType.DecompressOnLoad;
            s.compressionFormat = AudioCompressionFormat.Vorbis;
            s.quality = 0.7f;
            s.preloadAudioData = true;
            s.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            a.defaultSampleSettings = s;
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Zenith/")) return;
            var t = (TextureImporter)assetImporter;
            t.mipmapEnabled = false;
            t.alphaIsTransparency = true;
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.npotScale = TextureImporterNPOTScale.None;
            if (assetPath.Contains("/Icons/"))
            {
                t.textureType = TextureImporterType.Default;
                t.textureCompression = TextureImporterCompression.Uncompressed;
            }
            if (assetPath.Contains("/Art/"))
            {
                t.textureCompression = TextureImporterCompression.Uncompressed;
                t.isReadable = true;
            }
        }
    }
}
