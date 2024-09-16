using UnityEngine;
using System.Collections.Generic;
using Habrador_Computational_Geometry;
using UnityEditor;

public static class MeshUtils
{
    // 判断多边形是否是顺时针方向
    public static bool IsPolygonClockwise(List<MyVector2> points)
    {
        float sum = 0f;

        for (int i = 0; i < points.Count; i++)
        {
            MyVector2 current = points[i];
            MyVector2 next = points[(i + 1) % points.Count];
            sum += (next.x - current.x) * (next.y + current.y);
        }

        // 返回是否为顺时针
        return sum < 0;
    }

    // 生成网格从一个轮廓，使用 Habrador_Computational_Geometry 进行三角剖分
    public static Mesh GenerateMeshFromContour(Contour contour)
    {
        if (contour == null || contour.points.Count < 3)
        {
            Debug.LogError("轮廓点数量不足，无法生成网格！");
            return null;
        }

        // 转换轮廓点为 MyVector2 格式
        List<MyVector2> hullPoints = new List<MyVector2>();
        foreach (Vector2 point in contour.points)
        {
            hullPoints.Add(new MyVector2(point.x, point.y));
        }

        // 判断轮廓是否为顺时针，如果是则反转
        if (IsPolygonClockwise(hullPoints))
        {
            hullPoints.Reverse();
        }

        // 处理轮廓中的孔
        HashSet<List<MyVector2>> holes = new HashSet<List<MyVector2>>();
        foreach (var hole in contour.holes)
        {
            List<MyVector2> holePoints = new List<MyVector2>();
            foreach (Vector2 point in hole.points)
            {
                holePoints.Add(new MyVector2(point.x, point.y));
            }

            // 判断孔是否为顺时针，如果不是则反转
            if (!IsPolygonClockwise(holePoints))
            {
                holePoints.Reverse();
            }

            holes.Add(holePoints);
        }

        // 使用 Delaunay 三角剖分生成三角形数据
        HalfEdgeData2 triangleData = _Delaunay.ConstrainedBySloan(null, hullPoints, holes, shouldRemoveTriangles: true, new HalfEdgeData2());

        // 将 2D 三角形转换为 3D 网格
        return ConvertHalfEdgeDataToMesh(triangleData);
    }

    // 将 HalfEdgeData2 转换为 Mesh
    private static Mesh ConvertHalfEdgeDataToMesh(HalfEdgeData2 triangleData)
    {
        // 获取所有三角形
        HashSet<Triangle2> triangles = _TransformBetweenDataStructures.HalfEdge2ToTriangle2(triangleData);

        // 定义顶点和三角形列表
        List<Vector3> vertices = new List<Vector3>();
        List<int> meshTriangles = new List<int>();
        Dictionary<MyVector2, int> vertexIndexMap = new Dictionary<MyVector2, int>();

        // 遍历每个三角形，构建网格顶点和三角形
        foreach (Triangle2 triangle in triangles)
        {
            MyVector2[] trianglePoints = new MyVector2[] { triangle.p1, triangle.p2, triangle.p3 };

            foreach (MyVector2 point in trianglePoints)
            {
                if (!vertexIndexMap.ContainsKey(point))
                {
                    vertexIndexMap[point] = vertices.Count;
                    vertices.Add(new Vector3(point.x, 0, point.y)); // 2D 点转换为 3D 顶点
                }

                meshTriangles.Add(vertexIndexMap[point]);
            }
        }

        // 创建 Unity 的 Mesh 对象
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = meshTriangles.ToArray();
        mesh.RecalculateNormals(); // 计算法线
        mesh.RecalculateBounds();  // 计算包围盒

        return mesh;
    }

    // 合并多个 Mesh 对象
    public static Mesh CombineMeshes(List<Mesh> meshes)
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

    // 绘制网格的叠加层
    public static void DrawMeshOverlay(Rect imageRect, Mesh mesh)
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

    // 将网格的世界坐标转换为画布上的位置
    public static Vector2 WorldToCanvas(Vector3 worldPoint, Rect canvasRect)
    {
        float x = worldPoint.x * canvasRect.width + canvasRect.x;
        float y = (1 - worldPoint.z) * canvasRect.height + canvasRect.y;

        return new Vector2(x, y);
    }

    // 镜像网格，修正三角形顺序
    public static Mesh MirrorMesh(Mesh originalMesh)
    {
        Mesh mirroredMesh = new Mesh();
        Vector3[] vertices = originalMesh.vertices;
        int[] triangles = originalMesh.triangles;

        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i].x = -vertices[i].x;
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int temp = triangles[i];
            triangles[i] = triangles[i + 2];
            triangles[i + 2] = temp;
        }

        mirroredMesh.vertices = vertices;
        mirroredMesh.triangles = triangles;
        mirroredMesh.RecalculateNormals(); // 重新计算法线
        mirroredMesh.RecalculateBounds();  // 重新计算包围盒

        return mirroredMesh;
    }

    // 查找离鼠标最近的点索引
    public static int FindClosestPointIndex(List<Vector2> points, Vector2 normalizedMousePos, Rect imageRect)
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
}
