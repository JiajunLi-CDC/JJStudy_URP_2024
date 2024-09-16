using UnityEngine;

public static class GUIUtils
{
    public static GUIStyle GetActiveButtonStyle()
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.normal.textColor = Color.green;
        style.normal.background = MakeTex(2, 2, Color.green);
        return style;
    }

    public static GUIStyle GetInactiveButtonStyle()
    {
        return new GUIStyle(GUI.skin.button);
    }

    private static Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; pix.Length > i; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }
}