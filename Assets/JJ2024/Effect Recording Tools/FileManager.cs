using System.IO;
using UnityEngine;
using UnityEditor;

namespace SpecialEffectsRecorder
{
    public static class FileManager
    {
        public static void SaveFinalTextureAsPNG(Texture2D texture, string savePath, string imageName)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = Application.dataPath;
            }

            string filePath = Path.Combine(savePath, imageName + ".png");
            byte[] bytes = texture.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
            Debug.Log("保存序列帧图像到: " + filePath);
            AssetDatabase.Refresh();
        }
    }
}