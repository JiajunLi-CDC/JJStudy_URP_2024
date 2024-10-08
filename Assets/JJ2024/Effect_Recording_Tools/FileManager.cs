using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace SpecialEffectsRecorder
{
    public static class FileManager
    {
        public static void SaveFinalTextureAsPNG(RenderTexture RTtexture, string savePath, string imageName)
        {
            // 转换 RenderTexture 为 Texture2D
            Texture2D texture = ConvertRenderTextureToTexture2D(RTtexture);

            // 将线性空间的 Texture2D 转换为 sRGB
            Texture2D srgbTexture = ConvertToSRGB(texture);
            // Texture2D srgbTexture = texture;

            if (string.IsNullOrEmpty(savePath))
            {
                savePath = Application.dataPath;
            }

            string filePath = Path.Combine(savePath, imageName + ".png");
            byte[] bytes = srgbTexture.EncodeToPNG();
            File.WriteAllBytes(filePath, bytes);
            // Debug.Log("保存序列帧图像到: " + filePath);
            // 弹出错误对话框
            EditorUtility.DisplayDialog(
                "Save Message",
                "保存序列帧图像到: " + filePath,
                "OK"
            );
            AssetDatabase.Refresh();

            // 清理内存
            Object.DestroyImmediate(texture);
            Object.DestroyImmediate(srgbTexture);
        }

        public static void SaveFinalTextureListAsPNG(List<RenderTexture> renderTextures, string savePath,
            string imageName)
        {
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = Application.dataPath;
            }

            // 检查路径是否存在，如果不存在则给出弹窗提示，并询问是否创建文件夹
            if (!Directory.Exists(savePath))
            {
                if (EditorUtility.DisplayDialog(
                        "路径不存在",
                        $"指定的路径不存在：{savePath}\n是否创建文件夹？",
                        "是",
                        "否"))
                {
                    Directory.CreateDirectory(savePath);
                }
                else
                {
                    // 如果用户选择不创建文件夹，则直接返回
                    return;
                }
            }

            // 遍历 List 中的 RenderTexture 并逐一保存
            for (int i = 0; i < renderTextures.Count; i++)
            {
                RenderTexture rt = renderTextures[i];

                // 转换 RenderTexture 为 Texture2D
                Texture2D texture = ConvertRenderTextureToTexture2D(rt);
                Texture2D srgbTexture = texture;

                // 文件名加入索引后缀
                string filePath = Path.Combine(savePath, $"{imageName}_{i:D04}.png");

                // 保存为 PNG 文件
                byte[] bytes = srgbTexture.EncodeToPNG();
                File.WriteAllBytes(filePath, bytes);

                // Debug.Log($"保存图片: {filePath}");

                // 清理内存
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(srgbTexture);
            }

            // 刷新资源管理器
            AssetDatabase.Refresh();

            // 弹出保存完成的对话框
            EditorUtility.DisplayDialog(
                "Save Message",
                $"已成功保存 {renderTextures.Count} 张序列帧图像到文件夹: {savePath}",
                "OK"
            );
        }


        // 将 RenderTexture 转换为 Texture2D 以便保存
        private static Texture2D ConvertRenderTextureToTexture2D(RenderTexture renderTexture)
        {
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBAFloat, false);
            RenderTexture.active = renderTexture;
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;
            return texture;
        }

        // 将线性空间的 Texture2D 转换为 sRGB 颜色空间
        private static Texture2D ConvertToSRGB(Texture2D linearTexture)
        {
            Texture2D srgbTexture = new Texture2D(linearTexture.width, linearTexture.height, linearTexture.format, false);
        
            for (int y = 0; y < linearTexture.height; y++)
            {
                for (int x = 0; x < linearTexture.width; x++)
                {
                    Color linearColor = linearTexture.GetPixel(x, y);
                    Color srgbColor = linearColor.gamma; // 使用 gamma 来将线性颜色转换为 sRGB
                    srgbTexture.SetPixel(x, y, srgbColor);
                }
            }
        
            srgbTexture.Apply();
            return srgbTexture;
        }

        public static (Rect boundingBox, Vector2 center) CalculateBoundingBoxForSequence(RenderTexture renderTexture,
            Vector2 frameCount)
        {
            // 将 RenderTexture 转换为 Texture2D
            Texture2D texture = ConvertRenderTextureToTexture2D(renderTexture);

            int frameWidth = Mathf.FloorToInt(texture.width / frameCount.x); // 单帧的宽度（例如400）
            int frameHeight = Mathf.FloorToInt(texture.height / frameCount.y); // 单帧的高度（例如400）

            // 初始化包围盒边界为最大值（保证逐渐缩小）
            int minX = frameWidth;
            int minY = frameHeight;
            int maxX = 0;
            int maxY = 0;

            Color[] accumulatedColors = new Color[frameWidth * frameHeight];

            // 遍历所有帧并叠加帧的像素
            for (int yFrame = 0; yFrame < frameCount.y; yFrame++)
            {
                for (int xFrame = 0; xFrame < frameCount.x; xFrame++)
                {
                    // 遍历每帧中的每个像素
                    for (int y = 0; y < frameHeight; y++)
                    {
                        for (int x = 0; x < frameWidth; x++)
                        {
                            int pixelX = x + xFrame * frameWidth;
                            int pixelY = y + yFrame * frameHeight;
                            Color framePixel = texture.GetPixel(pixelX, pixelY);

                            // 如果当前像素有非透明值（RGBA 都为 0 的像素忽略）
                            if (framePixel.a > 0f || framePixel.r > 0f || framePixel.g > 0f || framePixel.b > 0f)
                            {
                                // 叠加颜色到 accumulatedColors 数组中（这里我们只关心有像素的地方）
                                int index = y * frameWidth + x;
                                accumulatedColors[index] = new Color(
                                    Mathf.Max(accumulatedColors[index].r, framePixel.r),
                                    Mathf.Max(accumulatedColors[index].g, framePixel.g),
                                    Mathf.Max(accumulatedColors[index].b, framePixel.b),
                                    Mathf.Max(accumulatedColors[index].a, framePixel.a)
                                );

                                // 更新包围盒的边界
                                minX = Mathf.Min(minX, x);
                                minY = Mathf.Min(minY, y);
                                maxX = Mathf.Max(maxX, x);
                                maxY = Mathf.Max(maxY, y);
                            }
                        }
                    }
                }
            }

            // 如果 minX 和 minY 没有被更新，说明没有非透明像素，返回空包围盒
            if (minX == frameWidth && minY == frameHeight)
            {
                return (new Rect(0, 0, 0, 0), Vector2.zero);
            }

            // 计算包围盒的宽度和高度
            int boundingBoxWidth = maxX - minX + 1;
            int boundingBoxHeight = maxY - minY + 1;

            // 计算包围盒的中心
            Vector2 center = new Vector2(minX + boundingBoxWidth / 2f, minY + boundingBoxHeight / 2f);

            // 返回包围盒和中心点
            return (new Rect(minX, minY, boundingBoxWidth, boundingBoxHeight), center);
        }

        public static (Rect boundingBox, Vector2 center) CalculateBoundingBoxForMultipleFrames(
            List<RenderTexture> renderTextures, Vector2 frameSize)
        {
            if (renderTextures == null || renderTextures.Count == 0)
            {
                return (new Rect(0, 0, 0, 0), Vector2.zero);
            }

            // 获取单张图片的宽度和高度
            int frameWidth = Mathf.FloorToInt(frameSize.x); // 单帧的宽度
            int frameHeight = Mathf.FloorToInt(frameSize.y); // 单帧的高度

            // 初始化包围盒边界为最大值（保证逐渐缩小）
            int minX = frameWidth;
            int minY = frameHeight;
            int maxX = 0;
            int maxY = 0;

            Color[] accumulatedColors = new Color[frameWidth * frameHeight];

            // 遍历所有 RenderTexture 进行叠加
            foreach (var renderTexture in renderTextures)
            {
                // 将 RenderTexture 转换为 Texture2D
                Texture2D texture = ConvertRenderTextureToTexture2D(renderTexture);

                // 遍历每帧中的每个像素
                for (int y = 0; y < frameHeight; y++)
                {
                    for (int x = 0; x < frameWidth; x++)
                    {
                        Color framePixel = texture.GetPixel(x, y);

                        // 如果当前像素有非透明值（RGBA 都为 0 的像素忽略）
                        if (framePixel.a > 0f || framePixel.r > 0f || framePixel.g > 0f || framePixel.b > 0f)
                        {
                            // 叠加颜色到 accumulatedColors 数组中（这里我们只关心有像素的地方）
                            int index = y * frameWidth + x;
                            accumulatedColors[index] = new Color(
                                Mathf.Max(accumulatedColors[index].r, framePixel.r),
                                Mathf.Max(accumulatedColors[index].g, framePixel.g),
                                Mathf.Max(accumulatedColors[index].b, framePixel.b),
                                Mathf.Max(accumulatedColors[index].a, framePixel.a)
                            );

                            // 更新包围盒的边界
                            minX = Mathf.Min(minX, x);
                            minY = Mathf.Min(minY, y);
                            maxX = Mathf.Max(maxX, x);
                            maxY = Mathf.Max(maxY, y);
                        }
                    }
                }
            }

            // 如果 minX 和 minY 没有被更新，说明没有非透明像素，返回空包围盒
            if (minX == frameWidth && minY == frameHeight)
            {
                return (new Rect(0, 0, 0, 0), Vector2.zero);
            }

            // 计算包围盒的宽度和高度
            int boundingBoxWidth = maxX - minX + 1;
            int boundingBoxHeight = maxY - minY + 1;

            // 计算包围盒的中心
            Vector2 center = new Vector2(minX + boundingBoxWidth / 2f, minY + boundingBoxHeight / 2f);

            // 返回包围盒和中心点
            return (new Rect(minX, minY, boundingBoxWidth, boundingBoxHeight), center);
        }
    }
}