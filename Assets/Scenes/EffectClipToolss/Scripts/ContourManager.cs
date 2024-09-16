using System.Collections.Generic;  // 引入 List、HashSet 等集合类
using UnityEngine;                 // 引入 Unity 引擎核心功能
using UnityEditor;
using Habrador_Computational_Geometry;  // 引入几何计算库，用于三角剖分等操作

public class ContourManager
{
    public List<Contour> OuterContours { get; private set; } = new List<Contour>();  // 管理所有外轮廓
    public bool IsDrawMode { get; set; } = true;  // 控制是否处于绘制模式

    private Contour currentDrawingContour = null; // 当前正在绘制的轮廓
    private bool isDrawing = false;               // 当前是否正在绘制
    private int draggedPointIndex = -1;           // 当前拖拽的点的索引

    public ContourManager()
    {
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

        if (e.type == EventType.MouseDown)
        {
            if (IsDrawMode)
            {
                HandleDrawModeMouseDown(point, e);
            }
            else
            {
                HandleEditModeMouseDown(point, e, imageRect);
            }
        }
        else if (e.type == EventType.MouseDrag && draggedPointIndex != -1 && e.button == 0)
        {
            currentDrawingContour.points[draggedPointIndex] = point;
            if (draggedPointIndex == 0)
            {
                currentDrawingContour.points[currentDrawingContour.points.Count - 1] = point;
            }
            Event.current.Use();
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            draggedPointIndex = -1;
        }
    }

    // 生成所有轮廓的网格
    public Mesh GenerateMeshFromContours()
    {
        List<Mesh> meshes = new List<Mesh>();

        foreach (var contour in OuterContours)
        {
            // 使用自定义的 MeshUtils 进行网格生成
            meshes.Add(MeshUtils.GenerateMeshFromContour(contour));
        }

        return MeshUtils.CombineMeshes(meshes);
    }

    // 绘制外轮廓列表（包括孔）
    public void DrawOuterContourList()
    {
        for (int i = 0; i < OuterContours.Count; i++)
        {
            Contour contour = OuterContours[i];
            GUILayout.BeginVertical("box");
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(contour.name);

            if (GUILayout.Button(contour.isDrawing ? "结束绘制" : "开始绘制", GUILayout.Width(80)))
            {
                ToggleDrawing(contour);
            }

            if (GUILayout.Button("清除绘制", GUILayout.Width(80)))
            {
                contour.points.Clear();
                isDrawing = false;
                currentDrawingContour = null;
            }

            if (GUILayout.Button("添加Hole", GUILayout.Width(80)))
            {
                contour.holes.Add(new Contour("Hole" + (contour.holes.Count + 1)));
            }

            EditorGUILayout.EndHorizontal();
            DrawHoleList(contour.holes);  // 绘制孔列表
            GUILayout.EndVertical();
        }
    }

    // 绘制孔列表
    private void DrawHoleList(List<Contour> holes)
    {
        for (int i = 0; i < holes.Count; i++)
        {
            Contour hole = holes[i];
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(hole.name);

            if (GUILayout.Button(hole.isDrawing ? "结束绘制" : "开始绘制", GUILayout.Width(80)))
            {
                ToggleDrawing(hole);
            }

            if (GUILayout.Button("清除绘制", GUILayout.Width(80)))
            {
                hole.points.Clear();
                isDrawing = false;
                currentDrawingContour = null;
            }

            if (GUILayout.Button("删除Hole", GUILayout.Width(80)))
            {
                holes.RemoveAt(i);
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    // 处理绘制模式下的鼠标按下事件
    private void HandleDrawModeMouseDown(Vector2 point, Event e)
    {
        if (e.button == 0)
        {
            currentDrawingContour.points.Add(point);
            e.Use();
        }
    }

    // 处理编辑模式下的鼠标按下事件
    private void HandleEditModeMouseDown(Vector2 point, Event e, Rect imageRect)
    {
        draggedPointIndex = MeshUtils.FindClosestPointIndex(currentDrawingContour.points, point, imageRect);
        e.Use();
    }

    // 切换当前绘制状态
    private void ToggleDrawing(Contour contour)
    {
        if (currentDrawingContour == contour && isDrawing)
        {
            isDrawing = false;
            currentDrawingContour = null;
        }
        else
        {
            currentDrawingContour = contour;
            isDrawing = true;
        }
    }
}

// Contour 类表示一个轮廓，可以包含孔
public class Contour
{
    public string name;
    public bool isDrawing = false;  // 当前是否处于绘制状态
    public List<Vector2> points = new List<Vector2>();  // 轮廓上的点
    public List<Contour> holes = new List<Contour>();   // 该轮廓内的孔

    public Contour(string name)
    {
        this.name = name;
    }
}
