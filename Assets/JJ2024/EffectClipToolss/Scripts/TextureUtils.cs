using UnityEngine;

namespace JJEffectClipperTool
{
    public static class TextureUtils
    {
        // 生成单张图片的纹理，处理通道的有无
        public static Texture2D GenerateTextureWithChannel(Texture2D image, EffectClipperToolNew.Channel channel)
        {
            Texture2D tempTexture = new Texture2D(image.width, image.height);
            Color[] pixels = image.GetPixels();

            // 判断是否有 Alpha 通道
            bool hasAlpha = image.format == TextureFormat.ARGB32 || image.format == TextureFormat.RGBA32 ||
                            image.format == TextureFormat.RGBAHalf || image.format == TextureFormat.RGBAFloat;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];
                float alphaValue = pixel.a; // 如果没有 A 通道，默认为 1

                switch (channel)
                {
                    case EffectClipperToolNew.Channel.RGBA:
                        tempTexture.SetPixel(i % image.width, i / image.width,
                            new Color(pixel.r, pixel.g, pixel.b, alphaValue));
                        break;
                    case EffectClipperToolNew.Channel.R:
                        tempTexture.SetPixel(i % image.width, i / image.width, new Color(pixel.r, 0, 0, 1));
                        break;
                    case EffectClipperToolNew.Channel.G:
                        tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, pixel.g, 0, 1));
                        break;
                    case EffectClipperToolNew.Channel.B:
                        tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, 0, pixel.b, 1));
                        break;
                    case EffectClipperToolNew.Channel.A:
                        tempTexture.SetPixel(i % image.width, i / image.width,
                            new Color(alphaValue, alphaValue, alphaValue, 1));
                        break;
                    case EffectClipperToolNew.Channel.StepA:
                        // 如果 alphaValue > 0，则输出白色 (1, 1, 1, 1)，否则输出黑色 (0, 0, 0, 1)
                        tempTexture.SetPixel(i % image.width, i / image.width,
                            alphaValue > 0 ? new Color(1, 1, 1, 1) : new Color(0, 0, 0, 1));
                        break;

                }
            }

            tempTexture.Apply();
            return tempTexture;
        }

        public static Texture2D GenerateTextureForSequence(Texture2D image, Vector2 frameSize,
            EffectClipperToolNew.Channel channel)
        {
            int frameWidth = Mathf.FloorToInt(image.width / frameSize.x);
            int frameHeight = Mathf.FloorToInt(image.height / frameSize.y);

            // 生成纹理与单帧相同大小
            Texture2D resultTexture = new Texture2D(frameWidth, frameHeight);
            Color[] accumulatedColors = new Color[frameWidth * frameHeight];

            // 初始化所有像素为黑色
            for (int i = 0; i < accumulatedColors.Length; i++)
            {
                accumulatedColors[i] = Color.black;
            }

            // 判断是否有 Alpha 通道
            bool hasAlpha = image.format == TextureFormat.ARGB32 || image.format == TextureFormat.RGBA32 ||
                            image.format == TextureFormat.RGBAHalf || image.format == TextureFormat.RGBAFloat;

            // 遍历所有帧
            for (int yFrame = 0; yFrame < frameSize.y; yFrame++)
            {
                for (int xFrame = 0; xFrame < frameSize.x; xFrame++)
                {
                    // 遍历每帧中的每个像素
                    for (int y = 0; y < frameHeight; y++)
                    {
                        for (int x = 0; x < frameWidth; x++)
                        {
                            int pixelX = x + xFrame * frameWidth;
                            int pixelY = y + yFrame * frameHeight;
                            Color framePixel = image.GetPixel(pixelX, pixelY);

                            // 考虑通道的情况
                            float alphaValue = framePixel.a; // 如果没有 A 通道，默认为 1

                            Color newPixel;
                            switch (channel)
                            {
                                case EffectClipperToolNew.Channel.RGBA:
                                    newPixel = new Color(framePixel.r, framePixel.g, framePixel.b, alphaValue);
                                    break;
                                case EffectClipperToolNew.Channel.R:
                                    newPixel = new Color(framePixel.r, 0, 0, 1);
                                    break;
                                case EffectClipperToolNew.Channel.G:
                                    newPixel = new Color(0, framePixel.g, 0, 1);
                                    break;
                                case EffectClipperToolNew.Channel.B:
                                    newPixel = new Color(0, 0, framePixel.b, 1);
                                    break;
                                case EffectClipperToolNew.Channel.A:
                                    newPixel = new Color(alphaValue, alphaValue, alphaValue, 1);
                                    break;
                                case EffectClipperToolNew.Channel.StepA:
                                    // 如果 alphaValue > 0，则输出白色 (1, 1, 1)，否则输出黑色 (0, 0, 0)
                                    newPixel = alphaValue > 0 ? new Color(1, 1, 1, 1) : new Color(0, 0, 0, 1);
                                    break;
                                default:
                                    newPixel = new Color(1, 1, 1, alphaValue);
                                    break;
                            }

                            // 将当前帧的颜色与已累积的颜色叠加，取每个通道的最大值
                            int index = y * frameWidth + x;
                            accumulatedColors[index] = new Color(
                                Mathf.Max(newPixel.r, accumulatedColors[index].r), // 取 R 通道的最大值
                                Mathf.Max(newPixel.g, accumulatedColors[index].g), // 取 G 通道的最大值
                                Mathf.Max(newPixel.b, accumulatedColors[index].b), // 取 B 通道的最大值
                                Mathf.Max(newPixel.a, accumulatedColors[index].a) // 取 A 通道的最大值
                            );
                        }
                    }
                }
            }

            // 将累积的颜色进行 saturate 操作，确保颜色值不超出 [0, 1] 范围
            for (int i = 0; i < accumulatedColors.Length; i++)
            {
                accumulatedColors[i] = new Color(
                    Mathf.Clamp01(accumulatedColors[i].r),
                    Mathf.Clamp01(accumulatedColors[i].g),
                    Mathf.Clamp01(accumulatedColors[i].b),
                    Mathf.Clamp01(accumulatedColors[i].a)
                );
            }

            resultTexture.SetPixels(accumulatedColors);
            resultTexture.Apply();
            return resultTexture;
        }
    }
}