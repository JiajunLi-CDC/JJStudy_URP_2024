using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using Habrador_Computational_Geometry;
using System.Linq;
using System.IO;

public class EffectClipperTool : EditorWindow
{
    private Texture2D selectedImage;
    private List<Contour> outerContours = new List<Contour>();
    private Contour currentDrawingContour = null;
    private bool isDrawing = false;
    private bool isDrawMode = true;
    private int draggedPointIndex = -1;

    private Mesh generatedMesh = null;

    private enum Channel { RGBA, R, G, B, A }
    private Channel selectedChannel = Channel.RGBA;

    private Texture2D cachedTexture = null;
    private Texture2D originalImage = null;

    // 新增的路径和文件名字段
    private string saveFolderPath = "";
    private string saveFileName = "GeneratedMesh.obj";

    [MenuItem("Tools/特效工具/特效裁剪工具")]
    static void ShowWindow()
    {
        EffectClipperTool window = GetWindow<EffectClipperTool>("特效裁剪工具ol");
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

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
        {
            if (isDrawing && currentDrawingContour != null)
            {
                ToggleDrawing(currentDrawingContour);
                Repaint();
                e.Use();
            }
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
        {
            isDrawMode = !isDrawMode;
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

        GUILayout.Label("通道选择", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        selectedChannel = (Channel)EditorGUILayout.EnumPopup("选择通道: ", selectedChannel);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        Texture2D newSelectedImage = ObjectFieldWithoutThumbnail("放置图片", selectedImage, typeof(Texture2D)) as Texture2D;
        if (newSelectedImage != selectedImage)
        {
            selectedImage = newSelectedImage;
            outerContours.Clear();
            outerContours.Add(new Contour("外轮廓1"));
            cachedTexture = null;
        }

        if (selectedImage != null)
        {
            if (!selectedImage.isReadable)
            {
                EditorGUILayout.HelpBox("图片未启用读写，请在图片导入设置中启用读写功能。", MessageType.Error);
            }

            if (originalImage != selectedImage || cachedTexture == null)
            {
                originalImage = selectedImage;
                cachedTexture = GenerateTextureWithChannel(selectedImage, selectedChannel);
            }

            float windowWidth = position.width;
            float canvasWidth = windowWidth * 0.8f;
            float aspectRatio = (float)selectedImage.width / selectedImage.height;
            float canvasHeight = canvasWidth / aspectRatio;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect imageRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.Width(canvasWidth), GUILayout.Height(canvasHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUI.DrawTexture(imageRect, cachedTexture, ScaleMode.ScaleToFit);
            Handles.DrawSolidRectangleWithOutline(imageRect, Color.clear, Color.white);
            HandleMouseEvents(imageRect);
            DrawAllContours(imageRect);

            // 显示生成的网格叠加在画布上
            if (generatedMesh != null)
            {
                DrawMeshOverlay(imageRect, generatedMesh);
            }
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

        EditorGUILayout.Space(20);
        GUILayout.BeginVertical("box");
        GUILayout.Label("生成网格", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成网格", GUILayout.Width(150)))
        {
            GenerateMeshFromContours();
        }

        if (GUILayout.Button("清除网格", GUILayout.Width(150)))
        {
            ClearMesh();
        }
        EditorGUILayout.EndHorizontal();
        GUILayout.EndVertical();

        // 添加输入框和保存按钮
        EditorGUILayout.Space(20); // 添加10像素的空行
       
        GUILayout.Label("保存路径", EditorStyles.boldLabel);
        GUILayout.BeginVertical("box");
        EditorGUI.indentLevel++;
        saveFolderPath = EditorGUILayout.TextField("输入文件夹路径", saveFolderPath);
        saveFileName = EditorGUILayout.TextField("输入文件名", saveFileName);

        if (GUILayout.Button("保存网格到文件", GUILayout.Width(200)))
        {
            SaveMeshToFile(Path.Combine(saveFolderPath, saveFileName + ".obj"));
        }
        EditorGUI.indentLevel--;
        GUILayout.EndVertical();
    }

    // 画布上显示半透明网格
    private void DrawMeshOverlay(Rect imageRect, Mesh mesh)
    {
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Handles.color = new Color(0, 1, 0, 0.3f); // 半透明绿色

        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector2 v0 = WorldToCanvas(vertices[triangles[i]], imageRect);
            Vector2 v1 = WorldToCanvas(vertices[triangles[i + 1]], imageRect);
            Vector2 v2 = WorldToCanvas(vertices[triangles[i + 2]], imageRect);

            Handles.DrawAAConvexPolygon(new Vector3[] { v0, v1, v2 });
            Handles.DrawLine(v0, v1);
            Handles.DrawLine(v1, v2);
            Handles.DrawLine(v2, v0);
        }
    }

    // 将网格顶点坐标转换到画布上的位置
    private Vector2 WorldToCanvas(Vector3 worldPoint, Rect canvasRect)
    {
        float x = worldPoint.x * canvasRect.width + canvasRect.x;
        float y = (1 - worldPoint.z) * canvasRect.height + canvasRect.y;

        return new Vector2(x, y);
    }

    private void GenerateMeshFromContours()
    {
        ClearMesh();
        List<Mesh> meshes = new List<Mesh>();

        foreach (var contour in outerContours)
        {
            List<MyVector2> hullPoints_2d = contour.points.Select(p => new MyVector2(p.x, 1 - p.y)).ToList();
            if (IsPolygonCounterClockwise(hullPoints_2d))
            {
                hullPoints_2d.Reverse();
            }

            HashSet<List<MyVector2>> holePointsSets_2d = new HashSet<List<MyVector2>>();
            foreach (var hole in contour.holes)
            {
                List<MyVector2> holePoints_2d = hole.points.Select(p => new MyVector2(p.x, 1 - p.y)).ToList();
                if (!IsPolygonCounterClockwise(holePoints_2d))
                {
                    holePoints_2d.Reverse();
                }

                holePointsSets_2d.Add(holePoints_2d);
            }

            List<MyVector2> allPoints = new List<MyVector2>();
            allPoints.AddRange(hullPoints_2d);
            foreach (List<MyVector2> holePoints in holePointsSets_2d)
            {
                allPoints.AddRange(holePoints);
            }

            Normalizer2 normalizer = new Normalizer2(allPoints);
            List<MyVector2> hullPoints_2d_normalized = normalizer.Normalize(hullPoints_2d);
            HashSet<List<MyVector2>> allHolePoints_2d_normalized = new HashSet<List<MyVector2>>();
            foreach (List<MyVector2> hole in holePointsSets_2d)
            {
                List<MyVector2> hole_normalized = normalizer.Normalize(hole);
                allHolePoints_2d_normalized.Add(hole_normalized);
            }

            HalfEdgeData2 triangleData_normalized = _Delaunay.ConstrainedBySloan(
                null, hullPoints_2d_normalized, allHolePoints_2d_normalized, shouldRemoveTriangles: true, new HalfEdgeData2());
            HalfEdgeData2 unNormalizedTriangleData = normalizer.UnNormalize(triangleData_normalized);

            HashSet<Triangle2> triangles = _TransformBetweenDataStructures.HalfEdge2ToTriangle2(unNormalizedTriangleData);
            triangles = HelpMethods.OrientTrianglesClockwise(triangles);

            HashSet<Triangle3<MyVector3>> triangles3D = new HashSet<Triangle3<MyVector3>>();
            foreach (var triangle in triangles)
            {
                triangles3D.Add(new Triangle3<MyVector3>(
                    triangle.p1.ToMyVector3_Yis3D(),
                    triangle.p2.ToMyVector3_Yis3D(),
                    triangle.p3.ToMyVector3_Yis3D()
                ));
            }

            Mesh meshPart = _TransformBetweenDataStructures.Triangle3ToCompressedMesh(triangles3D);
            meshes.Add(meshPart);
        }

        generatedMesh = CombineMeshes(meshes);
    }

    private bool IsPolygonCounterClockwise(List<MyVector2> points)
    {
        float sum = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            MyVector2 current = points[i];
            MyVector2 next = points[(i + 1) % points.Count];
            sum += (next.x - current.x) * (next.y + current.y);
        }

        return sum > 0;
    }

    private Mesh CombineMeshes(List<Mesh> meshes)
    {
        CombineInstance[] combine = new CombineInstance[meshes.Count];
        for (int i = 0; i < meshes.Count; i++)
        {
            combine[i].mesh = meshes[i];
            combine[i].transform = Matrix4x4.identity;
        }

        Mesh combinedMesh = new Mesh();
        combinedMesh.CombineMeshes(combine, true, false);
        return combinedMesh;
    }

    private void ClearMesh()
    {
        generatedMesh = null;
    }

    private Texture2D GenerateTextureWithChannel(Texture2D image, Channel channel)
    {
        Texture2D tempTexture = new Texture2D(image.width, image.height);
        Color[] pixels = image.GetPixels();
        bool hasAlpha = image.format == TextureFormat.ARGB32 || image.format == TextureFormat.RGBA32 ||
                        image.format == TextureFormat.DXT5 || image.format == TextureFormat.PVRTC_RGBA4 ||
                        image.format == TextureFormat.ETC2_RGBA8;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color pixel = pixels[i];
            switch (channel)
            {
                case Channel.RGBA:
                    tempTexture.SetPixel(i % image.width, i / image.width, pixel);
                    break;
                case Channel.R:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(pixel.r, 0, 0, pixel.a));
                    break;
                case Channel.G:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, pixel.g, 0, pixel.a));
                    break;
                case Channel.B:
                    tempTexture.SetPixel(i % image.width, i / image.width, new Color(0, 0, pixel.b, pixel.a));
                    break;
                case Channel.A:
                    if (hasAlpha)
                    {
                        tempTexture.SetPixel(i % image.width, i / image.width, new Color(pixel.a, pixel.a, pixel.a, 1));
                    }
                    else
                    {
                        tempTexture.SetPixel(i % image.width, i / image.width, Color.black);
                    }
                    break;
            }
        }

        tempTexture.Apply();
        return tempTexture;
    }

    private void HandleMouseEvents(Rect imageRect)
    {
        Event e = Event.current;

        if (currentDrawingContour == null)
        {
            return;
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
                if (e.button == 0)
                {
                    currentDrawingContour.points.Add(point);
                    e.Use();
                    Repaint();
                }
                else if (e.button == 1)
                {
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
                if (e.button == 0)
                {
                    draggedPointIndex = FindClosestPointIndex(currentDrawingContour.points, point, imageRect);
                    e.Use();
                }
                else if (e.button == 1)
                {
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
            draggedPointIndex = -1;
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
                ToggleDrawing(contour);
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
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                return;
            }
            GUI.enabled = true;

            if (GUILayout.Button("添加Hole", GUILayout.Width(80)))
            {
                int holeCount = contour.holes.Count + 1;
                contour.holes.Add(new Contour("Hole" + holeCount));
            }
            EditorGUILayout.EndHorizontal();

            DrawHoleList(contour.holes);

            GUILayout.EndVertical();
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
                EndAllDrawings();
                ToggleDrawing(hole);
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
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
                return;
            }
            GUI.enabled = true;

            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel--;
    }

    private void EndAllDrawings()
    {
        foreach (var contour in outerContours)
        {
            if (contour.isDrawing)
            {
                contour.isDrawing = false;
            }

            foreach (var hole in contour.holes)
            {
                if (hole.isDrawing)
                {
                    hole.isDrawing = false;
                }
            }
        }

        isDrawing = false;
        currentDrawingContour = null;
    }

    private void ToggleDrawing(Contour contour)
    {
        // 如果当前轮廓正在绘制，则结束绘制
        if (contour.isDrawing)
        {
            contour.isDrawing = false;
            isDrawing = false;
            currentDrawingContour = null;
        }
        else
        {
            // 如果其他轮廓正在绘制，结束它们的绘制状态
            EndAllDrawings();

            // 开始新的轮廓绘制
            contour.isDrawing = true;
            isDrawing = true;
            currentDrawingContour = contour;
        }

        // 重新绘制界面
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
                Handles.DrawAAPolyLine(2, new Vector3[] { linePoints[linePoints.Length - 1], linePoints[0] });
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
        for (int i = 0; pix.Length > i; i++)
            pix[i] = col;

        Texture2D result = new Texture2D(width, height);
        result.SetPixels(pix);
        result.Apply();
        return result;
    }

    // 保存网格到文件
    // 保存网格到文件
    private void SaveMeshToFile(string path)
    {
        if (generatedMesh == null || string.IsNullOrEmpty(path))
        {
            Debug.LogError("没有生成网格或路径无效。");
            return;
        }

        // 左右镜像顶点
        Mesh mirroredMesh = MirrorMesh(generatedMesh);

        try
        {
            using (StreamWriter writer = new StreamWriter(path))
            {
                // 使用用户输入的文件名作为网格名称
                writer.Write(MeshToString(mirroredMesh, saveFileName));
                Debug.Log("网格已成功保存到: " + path);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("保存网格时发生错误: " + e.Message);
        }

        // 自动刷新文件夹
        AssetDatabase.Refresh();
    }


    // 将网格数据转换为字符串
    private string MeshToString(Mesh mesh, string meshName)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        // 将网格的名称设置为用户输入的文件名
        sb.Append($"g {meshName}\n");

        foreach (Vector3 v in mesh.vertices)
        {
            sb.Append($"v {v.x} {v.y} {v.z}\n");
        }

        foreach (Vector3 n in mesh.normals)
        {
            sb.Append($"vn {n.x} {n.y} {n.z}\n");
        }

        foreach (Vector2 uv in mesh.uv)
        {
            sb.Append($"vt {uv.x} {uv.y}\n");
        }

        for (int i = 0; i < mesh.triangles.Length; i += 3)
        {
            sb.Append($"f {mesh.triangles[i] + 1}/{mesh.triangles[i] + 1}/{mesh.triangles[i] + 1} ");
            sb.Append($"{mesh.triangles[i + 1] + 1}/{mesh.triangles[i + 1] + 1}/{mesh.triangles[i + 1] + 1} ");
            sb.Append($"{mesh.triangles[i + 2] + 1}/{mesh.triangles[i + 2] + 1}/{mesh.triangles[i + 2] + 1}\n");
        }

        return sb.ToString();
    }


// 镜像网格并修正三角形顺序
    private Mesh MirrorMesh(Mesh originalMesh)
    {
        originalMesh.RecalculateNormals();
        Mesh mirroredMesh = new Mesh();
        Vector3[] vertices = originalMesh.vertices;
        Vector3[] normals = originalMesh.normals; // 获取原始法线
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = originalMesh.triangles;

        for (int i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(vertices[i].x, vertices[i].z); //归一化
            vertices[i].x -= 0.5f;
            vertices[i].z -= 0.5f;  //回到中心
            vertices[i].x = -vertices[i].x; // 左右镜像,因为坐标系原因，unity会做默认的镜像
        }

        // 修正三角形顺序，防止法线反转1
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int temp = triangles[i];
            triangles[i] = triangles[i + 2];
            triangles[i + 2] = temp;
        }

        mirroredMesh.vertices = vertices;
        mirroredMesh.uv = uvs;
        mirroredMesh.triangles = triangles;
        mirroredMesh.normals = normals; // 应用处理后的法线
        mirroredMesh.RecalculateNormals();


        return mirroredMesh;
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