using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class EffectClipperTool : EditorWindow
{
    private Texture2D selectedImage; // 用户选择的图片
    private List<Contour> outerContours = new List<Contour>(); // 存储外轮廓和Hole
    private Contour currentDrawingContour = null; // 当前正在绘制的轮廓
    private bool isDrawing = false; // 是否在绘制状态
    private bool isDrawMode = true; // true 表示 "绘制/删除" 模式，false 表示 "移动/添加" 模式
    private int draggedPointIndex = -1; // 当前被拖动的点

    private enum Channel { RGBA, R, G, B, A } // 通道枚举
    private Channel selectedChannel = Channel.RGBA; // 默认选择RGBA通道

    private Texture2D cachedTexture = null; // 缓存的纹理
    private Texture2D originalImage = null; // 用于检查图片是否更换

    [MenuItem("Tools/特效工具/特效裁剪工具")]
    static void ShowWindow()
    {
        EffectClipperTool window = GetWindow<EffectClipperTool>("特效裁剪工具");
        window.Show();
    }

    private void OnEnable()
    {
        if (outerContours.Count == 0)
        {
            outerContours.Add(new Contour("外轮廓1"));
        }
    }

    private void OnGUI()
    {
        Event e = Event.current;
        // 处理回车键结束绘制的功能
        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
        {
            if (isDrawing && currentDrawingContour != null)
            {
                ToggleDrawing(currentDrawingContour); // 结束当前绘制
                Repaint();
            }
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
        {
            isDrawMode = !isDrawMode; // 切换模式
            Repaint();
            e.Use();
        }

        GUILayout.Label("模式选择", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("绘制/删除", GetButtonStyle(isDrawMode), GUILayout.Width(150), GUILayout.Height(50)))
        {
            isDrawMode = true;
        }
        if (GUILayout.Button("移动/添加", GetButtonStyle(!isDrawMode), GUILayout.Width(150), GUILayout.Height(50)))
        {
            isDrawMode = false;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 通道选择部分
        GUILayout.Label("通道选择", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        selectedChannel = (Channel)EditorGUILayout.EnumPopup("选择通道: ", selectedChannel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // 检测图片是否更换
        Texture2D newSelectedImage = ObjectFieldWithoutThumbnail("放置图片", selectedImage, typeof(Texture2D)) as Texture2D;
        if (newSelectedImage != selectedImage)
        {
            // 如果图片更换，清除轮廓和holes
            selectedImage = newSelectedImage;
            outerContours.Clear();
            outerContours.Add(new Contour("外轮廓1"));
            cachedTexture = null;
        }

        if (selectedImage != null)
        {
            // 检查图片是否更换或通道是否改变
            if (originalImage != selectedImage || cachedTexture == null)
            {
                originalImage = selectedImage; // 更新原始图片引用
                cachedTexture = GenerateTextureWithChannel(selectedImage, selectedChannel); // 重新生成纹理
            }

            // 计算窗口宽度的80%作为画布宽度，并保持图片长宽比
            float windowWidth = position.width;
            float canvasWidth = windowWidth * 0.8f;
            float aspectRatio = (float)selectedImage.width / selectedImage.height;
            float canvasHeight = canvasWidth / aspectRatio;

            // 居中显示画布
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect imageRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.Width(canvasWidth), GUILayout.Height(canvasHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 绘制缓存的图片纹理
            GUI.DrawTexture(imageRect, cachedTexture, ScaleMode.ScaleToFit);

            // 绘制画布框
            Handles.DrawSolidRectangleWithOutline(imageRect, Color.clear, Color.white);

            // 处理鼠标事件
            HandleMouseEvents(imageRect);

            // 绘制所有轮廓
            DrawAllContours(imageRect);
        }
        else
        {
            EditorGUILayout.HelpBox("请在上方放置一张图片。", MessageType.Info);
            GUILayout.FlexibleSpace();
        }

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("外轮廓", EditorStyles.boldLabel);
        if (GUILayout.Button("添加外轮廓", GUILayout.Width(120)))
        {
            outerContours.Add(new Contour("外轮廓" + (outerContours.Count + 1)));
        }
        EditorGUILayout.EndHorizontal();

        DrawOuterContourList();
    }

    // 生成纹理并根据通道进行显示（仅在图片或通道更换时调用）
    private Texture2D GenerateTextureWithChannel(Texture2D image, Channel channel)
    {
        // 创建一个新的临时纹理，用于显示特定的通道
        Texture2D tempTexture = new Texture2D(image.width, image.height);

        // 获取图片的所有像素信息
        Color[] pixels = image.GetPixels();

        bool hasAlpha = image.format == TextureFormat.ARGB32 || image.format == TextureFormat.RGBA32 ||
                        image.format == TextureFormat.DXT5 || image.format == TextureFormat.PVRTC_RGBA4 ||
                        image.format == TextureFormat.ETC2_RGBA8; // 检查图片格式是否包含Alpha通道

        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            switch (channel)
            {
                case Channel.RGBA:
                    tempTexture.SetPixel(i % image.width, i / image.width, pixel); // 原始颜色
                    break;
                case Channel.R:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(pixel.r, 0, 0, pixel.a)); // 只显示红色
                    break;
                case Channel.G:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, pixel.g, 0, pixel.a)); // 只显示绿色
                    break;
                case Channel.B:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, 0, pixel.b, pixel.a)); // 只显示蓝色
                    break;
                case Channel.A:
                    if (hasAlpha)
                    {
                        tempTexture.SetPixel(i % image.width, i / image.width, new Color(pixel.a, pixel.a, pixel.a, 1)); // 显示Alpha通道
                    }
                    else
                    {
                        tempTexture.SetPixel(i % image.width, i / image.width, Color.black); // 如果没有Alpha通道，显示黑色
                    }
                    break;
            }
        }

        // 应用更改
        tempTexture.Apply();
        return tempTexture;
    }

   private void HandleMouseEvents(Rect imageRect)
{
    Event e = Event.current;

    // 检查是否有正在绘制的轮廓
    if (currentDrawingContour == null)
    {
        return; // 如果没有正在绘制的轮廓，直接返回，防止空引用错误
    }

    if (!imageRect.Contains(e.mousePosition))
        return;

    Vector2 mousePos = e.mousePosition;
    Vector2 point = mousePos - imageRect.position;
    point.x /= imageRect.width;
    point.y /= imageRect.height;

    if (e.type == EventType.MouseDown)
    {
        if (isDrawMode)
        {
            // 绘制/删除模式
            if (e.button == 0)
            {
                // 左键添加点
                currentDrawingContour.points.Add(point);
                e.Use();
                Repaint();
            }
            else if (e.button == 1)
            {
                // 右键删除最近的点
                int index = FindClosestPointIndex(currentDrawingContour.points, point, imageRect);
                if (index >= 0)
                {
                    currentDrawingContour.points.RemoveAt(index);
                    Repaint();
                }
                e.Use();
            }
        }
        else
        {
            // 移动/添加模式
            if (e.button == 0)
            {
                // 左键拖动顶点
                draggedPointIndex = FindClosestPointIndex(currentDrawingContour.points, point, imageRect);
                e.Use();
            }
            else if (e.button == 1)
            {
                // 右键插入点到线段
                int index = FindClosestEdgeIndex(currentDrawingContour.points, point, imageRect);
                if (index >= 0)
                {
                    currentDrawingContour.points.Insert(index + 1, point);
                    Repaint();
                }
                e.Use();
            }
        }
    }
    else if (e.type == EventType.MouseDrag && draggedPointIndex != -1 && e.button == 0)
    {
        // 拖动顶点更新位置
        currentDrawingContour.points[draggedPointIndex] = point;

        if (draggedPointIndex == 0)
        {
            currentDrawingContour.points[currentDrawingContour.points.Count - 1] = point;
        }

        Repaint();
        e.Use();
    }
    else if (e.type == EventType.MouseUp && e.button == 0)
    {
        draggedPointIndex = -1; // 结束拖动
    }
}


    private void DrawOuterContourList()
    {
        EditorGUI.indentLevel++;
        for (int i = 0; i < outerContours.Count; i++)
        {
            Contour contour = outerContours[i];

            GUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(contour.name);

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = contour.isDrawing ? new Color(0.8f, 0, 0) : Color.white;

            if (GUILayout.Button(contour.isDrawing ? "结束绘制" : "开始绘制", GUILayout.Width(80)))
            {
                if (isDrawing && currentDrawingContour != null && currentDrawingContour != contour)
                {
                    EditorUtility.DisplayDialog("提示", "请先结束当前轮廓的绘制。", "确定");
                }
                else
                {
                    ToggleDrawing(contour);
                }
            }

            GUI.backgroundColor = originalColor;

            if (GUILayout.Button("清除绘制", GUILayout.Width(80)))
            {
                if (contour.isDrawing)
                {
                    ToggleDrawing(contour);
                }
                contour.points.Clear();
                Repaint();
            }

            GUI.enabled = !contour.isDrawing;
            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                outerContours.RemoveAt(i);
                Repaint();
                GUILayout.EndHorizontal();  // 确保删除后立即退出当前布局
                GUILayout.EndVertical();
                return; // 添加 return 来防止布局继续执行
            }
            GUI.enabled = true;

            if (GUILayout.Button("添加Hole", GUILayout.Width(80)))
            {
                int holeCount = contour.holes.Count + 1;
                contour.holes.Add(new Contour("Hole" + holeCount));
            }
            EditorGUILayout.EndHorizontal();

            DrawHoleList(contour.holes);

            GUILayout.EndVertical(); // 确保匹配的布局调用
        }

        EditorGUI.indentLevel--;
    }

    private void DrawHoleList(List<Contour> holes)
    {
        EditorGUI.indentLevel++;
        for (int i = 0; i < holes.Count; i++)
        {
            Contour hole = holes[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(hole.name, GUILayout.Width(EditorGUIUtility.currentViewWidth - 330));

            Color originalColor = GUI.backgroundColor;
            GUI.backgroundColor = hole.isDrawing ? new Color(0.8f, 0, 0) : Color.white;

            if (GUILayout.Button(hole.isDrawing ? "结束绘制" : "开始绘制", GUILayout.Width(80)))
            {
                if (isDrawing && currentDrawingContour != null && currentDrawingContour != hole)
                {
                    EditorUtility.DisplayDialog("提示", "请先结束当前轮廓的绘制。", "确定");
                }
                else
                {
                    ToggleDrawing(hole);
                }
            }

            GUI.backgroundColor = originalColor;

            if (GUILayout.Button("清除绘制", GUILayout.Width(80)))
            {
                if (hole.isDrawing)
                {
                    ToggleDrawing(hole);
                }
                hole.points.Clear();
                Repaint();
            }

            GUI.enabled = !hole.isDrawing;
            if (GUILayout.Button("删除", GUILayout.Width(60)))
            {
                holes.RemoveAt(i);
                Repaint();
                EditorGUILayout.EndHorizontal(); // 确保删除后布局结束
                EditorGUI.indentLevel--;
                return; // 添加 return 来防止布局继续执行
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel--;
    }

    private void ToggleDrawing(Contour contour)
    {
        contour.isDrawing = !contour.isDrawing;
        isDrawing = contour.isDrawing;

        if (contour.isDrawing)
        {
            currentDrawingContour = contour;
        }
        else
        {
            if (contour.points.Count > 2 && contour.points[0] != contour.points[contour.points.Count - 1])
            {
                contour.points.Add(contour.points[0]); // 闭合线
            }
            currentDrawingContour = null;
        }

        Repaint();
    }

    private int FindClosestPointIndex(List<Vector2> points, Vector2 normalizedMousePos, Rect imageRect)
    {
        int closestIndex = -1;
        float minDistance = 10f;

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 p = points[i];
            Vector2 pointScreenPos = new Vector2(p.x * imageRect.width, p.y * imageRect.height) + imageRect.position;
            float distance = Vector2.Distance(Event.current.mousePosition, pointScreenPos);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
            }
        }

        return closestIndex;
    }

    private int FindClosestEdgeIndex(List<Vector2> points, Vector2 normalizedMousePos, Rect imageRect)
    {
        int closestEdgeIndex = -1;
        float minDistance = 10f;

        for (int i = 0; i < points.Count - 1; i++)
        {
            Vector2 p1 = points[i];
            Vector2 p2 = points[i + 1];
            Vector2 p1ScreenPos = new Vector2(p1.x * imageRect.width, p1.y * imageRect.height) + imageRect.position;
            Vector2 p2ScreenPos = new Vector2(p2.x * imageRect.width, p2.y * imageRect.height) + imageRect.position;

            float distance = HandleUtility.DistancePointToLineSegment(Event.current.mousePosition, p1ScreenPos, p2ScreenPos);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestEdgeIndex = i;
            }
        }

        return closestEdgeIndex;
    }

    private void DrawAllContours(Rect imageRect)
    {
        Handles.BeginGUI();

        foreach (var contour in outerContours)
        {
            DrawContour(contour, imageRect, Color.green);

            foreach (var hole in contour.holes)
            {
                DrawContour(hole, imageRect, Color.red);
            }
        }

        Handles.EndGUI();
    }

    private void DrawContour(Contour contour, Rect imageRect, Color color)
    {
        if (contour.points.Count > 0)
        {
            Handles.color = color;

            Vector3[] linePoints = new Vector3[contour.points.Count];
            for (int i = 0; i < contour.points.Count; i++)
            {
                Vector2 p = contour.points[i];
                Vector2 point = new Vector2(p.x * imageRect.width, p.y * imageRect.height) + imageRect.position;
                linePoints[i] = point;
            }

            if (linePoints.Length > 1)
            {
                Handles.DrawAAPolyLine(2, linePoints);
            }

            for (int i = 0; i < linePoints.Length; i++)
            {
                Handles.DrawSolidDisc(linePoints[i], Vector3.forward, 4);
            }
        }
    }

    private Object ObjectFieldWithoutThumbnail(string label, Object obj, System.Type objType)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        float originalLabelWidth = EditorGUIUtility.labelWidth;
        EditorGUIUtility.labelWidth = 0;
        obj = EditorGUILayout.ObjectField(obj, objType, false, GUILayout.Height(16));
        EditorGUIUtility.labelWidth = originalLabelWidth;
        EditorGUILayout.EndHorizontal();
        return obj;
    }

    private GUIStyle GetButtonStyle(bool isActive)
    {
        GUIStyle style = new GUIStyle(GUI.skin.button);
        style.normal.textColor = isActive ? Color.green : GUI.skin.button.normal.textColor;
        style.normal.background = isActive ? MakeTex(2, 2, Color.green) : GUI.skin.button.normal.background;
        return style;
    }

    private Texture2D MakeTex(int width, int height, Color col)
    {
        Color[] pix = new Color[width * height];
        for (int i = 0; i < pix.Length; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    public class Contour
    {
        public string name;
        public bool isDrawing = false;
        public List<Vector2> points = new List<Vector2>();
        public List<Contour> holes = new List<Contour>();

        public Contour(string name)
        {
            this.name = name;
        }
    }
}
