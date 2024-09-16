using UnityEngine;

public static class TextureUtils
{
    public static Texture2D GenerateTextureWithChannel(Texture2D image, EffectClipperToolNew.Channel channel)
    {
        Texture2D tempTexture = new Texture2D(image.width, image.height);
        Color[] pixels = image.GetPixels();
        bool hasAlpha = image.format == TextureFormat.ARGB32 || image.format == TextureFormat.RGBA32;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            switch (channel)
            {
                case EffectClipperToolNew.Channel.RGBA:
                    tempTexture.SetPixel(i % image.width, i / image.width, pixel);
                    break;
                case EffectClipperToolNew.Channel.R:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(pixel.r, 0, 0, pixel.a));
                    break;
                case EffectClipperToolNew.Channel.G:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, pixel.g, 0, pixel.a));
                    break;
                case EffectClipperToolNew.Channel.B:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, 0, pixel.b, pixel.a));
                    break;
                case EffectClipperToolNew.Channel.A:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(pixel.a, pixel.a, pixel.a, 1));
                    break;
            }
        }

        tempTexture.Apply();
        return tempTexture;
    }
}