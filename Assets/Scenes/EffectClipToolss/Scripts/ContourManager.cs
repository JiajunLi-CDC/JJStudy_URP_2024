using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Habrador_Computational_Geometry;

public class ContourManager
{
    private EditorWindow window; // 保存 EditorWindow 的引用

    public List<Contour> OuterContours { get; private set; } = new List<Contour>();
    public bool IsDrawMode { get; set; } = true;

    private Contour currentDrawingContour = null;
    private bool isDrawing = false;
    private int draggedPointIndex = -1;

    // 构造函数，接收 EditorWindow 实例
    public ContourManager(EditorWindow window)
    {
        this.window = window;
        ResetContours();
    }

    // 重置轮廓，清空所有现有轮廓并添加一个默认外轮廓
    public void ResetContours()
    {
        OuterContours.Clear();
        AddOuterContour("外轮廓1");
    }

    // 添加外轮廓
    public void AddOuterContour(string name)
    {
        OuterContours.Add(new Contour(name));
    }

    // 处理鼠标事件
    public void HandleMouseEvents(Rect imageRect)
    {
        Event e = Event.current;
        if (currentDrawingContour == null || !imageRect.Contains(e.mousePosition)) return;

        Vector2 mousePos = e.mousePosition;
        Vector2 point = mousePos - imageRect.position;
        point.x /= imageRect.width;
        point.y /= imageRect.height;

        if (IsDrawMode)
        {
            HandleDrawDeleteMode(point, e, imageRect);
        }
        else
        {
            HandleMoveAddMode(point, e, imageRect);
        }
    }

    // 绘制/删除模式
    private void HandleDrawDeleteMode(Vector2 point, Event e, Rect imageRect)
    {
        if (e.type == EventType.MouseDown && e.button == 0) // 左键添加点
        {
            currentDrawingContour.points.Add(point);
            e.Use();
        }
        else if (e.type == EventType.MouseDown && e.button == 1) // 右键删除最近的点
        {
            int index = MeshUtils.FindClosestPointIndex(currentDrawingContour.points, point, imageRect);
            if (index >= 0)
            {
                currentDrawingContour.points.RemoveAt(index);
            }

            e.Use();
        }
    }

    // 移动/添加模式
    private void HandleMoveAddMode(Vector2 point, Event e, Rect imageRect)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            draggedPointIndex = MeshUtils.FindClosestPointIndex(currentDrawingContour.points, point, imageRect);
            e.Use();
        }
        else if (e.type == EventType.MouseDrag && draggedPointIndex != -1 && e.button == 0)
        {
            currentDrawingContour.points[draggedPointIndex] = point;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            draggedPointIndex = -1;
        }

        if (e.type == EventType.MouseDown && e.button == 1)
        {
            int edgeIndex = MeshUtils.FindClosestEdgeIndex(currentDrawingContour.points, point, imageRect);
            if (edgeIndex >= 0)
            {
                currentDrawingContour.points.Insert((edgeIndex + 1) % currentDrawingContour.points.Count, point);
            }

            e.Use();
        }
    }

    public Mesh GenerateMeshFromContours()
    {
        List<Mesh> meshes = new List<Mesh>();
        bool hasEmptyContours = false; // 用于检查是否有空的轮廓
        bool hasValidContours = false; // 用于检查是否有有效的轮廓

        foreach (var contour in OuterContours)
        {
            // 如果轮廓没有任何点，则跳过该轮廓，不进行网格生成
            if (contour.points.Count == 0)
            {
                hasEmptyContours = true; // 标记发现空轮廓
                continue;
            }

            meshes.Add(MeshUtils.GenerateMeshFromContour(contour));
            hasValidContours = true; // 标记至少有一个有效的轮廓
        }

        // 在面板上显示警告
        if (hasEmptyContours)
        {
            EditorGUILayout.HelpBox("部分轮廓没有点，跳过网格生成。", MessageType.Warning);
        }

        // 如果没有任何有效轮廓，直接返回 null 并显示警告
        if (!hasValidContours)
        {
            EditorGUILayout.HelpBox("没有有效轮廓，未生成任何网格。", MessageType.Warning);
            return null;
        }

        return MeshUtils.CombineMeshes(meshes);
    }


    // 绘制轮廓
    public void DrawContours(Rect imageRect)
    {
        Handles.BeginGUI();
        Handles.color = Color.green;

        foreach (var contour in OuterContours)
        {
            DrawContour(contour, imageRect, Color.green);
            foreach (var hole in contour.holes)
            {
                DrawContour(hole, imageRect, Color.red);
            }
        }

        Handles.EndGUI();
    }

    // 绘制单个轮廓
    private void DrawContour(Contour contour, Rect imageRect, Color color)
    {
        if (contour.points.Count > 0)
        {
            Handles.color = color;

            Vector3[] linePoints = new Vector3[contour.points.Count];
            for (int i = 0; i < contour.points.Count; i++)
            {
                Vector2 p = contour.points[i];
                Vector2 canvasPoint = new Vector2(p.x * imageRect.width, p.y * imageRect.height) + imageRect.position;
                linePoints[i] = canvasPoint;
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

    // 新增的用于检查是否有任何轮廓或Hole正在绘制的方法
    public bool IsAnyContourDrawing()
    {
        foreach (var contour in OuterContours)
        {
            if (contour.isDrawing) return true;
            foreach (var hole in contour.holes)
            {
                if (hole.isDrawing) return true;
            }
        }

        return false;
    }

    public void DrawOuterContourList()
    {
        for (int i = 0; i < OuterContours.Count; i++)
        {
            Contour contour = OuterContours[i];
            GUILayout.BeginVertical("box");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(contour.name);
            GUILayout.FlexibleSpace();

            Color originalColor = GUI.backgroundColor;

            // 开始/结束绘制按钮
            GUI.backgroundColor = contour.isDrawing ? new Color(0.8f, 0, 0) : Color.white;
            if (GUILayout.Button(contour.isDrawing ? "结束绘制" : "开始绘制", GUILayout.Width(80)))
            {
                ToggleDrawing(contour);
            }

            GUI.backgroundColor = originalColor;

            // 清除绘制按钮
            if (GUILayout.Button("清除绘制", GUILayout.Width(80)))
            {
                contour.points.Clear();
                contour.isDrawing = false;
                isDrawing = false;
                currentDrawingContour = null;
            }

            // 删除轮廓按钮
            if (GUILayout.Button("删除轮廓", GUILayout.Width(80)))
            {
                OuterContours.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                GUILayout.EndVertical();
                break; // 跳出循环，避免继续访问已删除的元素
            }

            // 添加 Hole 按钮（调整到最后一个位置）
            if (GUILayout.Button("添加Hole", GUILayout.Width(80)))
            {
                contour.holes.Add(new Contour("Hole" + (contour.holes.Count + 1)));
            }

            EditorGUILayout.EndHorizontal();
            DrawHoleList(contour.holes);
            GUILayout.EndVertical();
        }
    }


    private void DrawHoleList(List<Contour> holes)
    {
        EditorGUI.indentLevel++;

        for (int i = 0; i < holes.Count; i++)
        {
            Contour hole = holes[i];
            EditorGUILayout.BeginHorizontal();

            EditorGUILayout.LabelField(hole.name); // 保持名称在左边

            GUILayout.FlexibleSpace(); // 将按钮推到右侧


            Color originalColor = GUI.backgroundColor;

            // 开始/结束绘制按钮
            GUI.backgroundColor = hole.isDrawing ? new Color(0.8f, 0, 0) : Color.white;
            if (GUILayout.Button(hole.isDrawing ? "结束绘制" : "开始绘制", GUILayout.Width(80)))
            {
                ToggleDrawing(hole);
            }

            GUI.backgroundColor = originalColor;

            // 清除绘制按钮
            if (GUILayout.Button("清除绘制", GUILayout.Width(80)))
            {
                hole.points.Clear();
                isDrawing = false;
                currentDrawingContour = null;
            }

            // 删除Hole按钮
            if (GUILayout.Button("删除", GUILayout.Width(80)))
            {
                holes.RemoveAt(i);
                EditorGUILayout.EndHorizontal();
                EditorGUI.indentLevel--;
                window.Repaint();
                break;
            }

            // 增加空白区域以补偿轮廓中的第四个按钮（"添加Hole"）
            GUILayout.Space(83); // 空白区域，宽度与按钮宽度一致，使Hole按钮和轮廓前三个按钮对齐

            EditorGUILayout.EndHorizontal();
        }

        EditorGUI.indentLevel--;
    }


    private void ToggleDrawing(Contour contour)
    {
        if (currentDrawingContour == contour && isDrawing)
        {
            contour.isDrawing = false;
            isDrawing = false;
            currentDrawingContour = null;
        }
        else
        {
            EndAllDrawings();
            contour.isDrawing = true;
            isDrawing = true;
            currentDrawingContour = contour;
        }
    }

    public void EndAllDrawings()
    {
        foreach (var contour in OuterContours)
        {
            contour.isDrawing = false;
            foreach (var hole in contour.holes)
            {
                hole.isDrawing = false;
            }
        }

        isDrawing = false;
        currentDrawingContour = null;
    }
}

// Contour 类
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