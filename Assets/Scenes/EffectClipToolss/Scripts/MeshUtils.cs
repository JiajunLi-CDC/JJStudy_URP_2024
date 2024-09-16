using UnityEngine;
using System.Collections.Generic;
using Habrador_Computational_Geometry;
using UnityEditor;
using System.Linq;
using System.IO;

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
            return meshPart;
    }
    
    private static bool IsPolygonCounterClockwise(List<MyVector2> points)
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
    public static int FindClosestPointIndex(List<Vector2> points, Vector2 normalizedMousePos, Rect imageRect)
    {
        int closestIndex = -1;
        float minDistance = 10f; // 最小的距离阈值

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

    public static int FindClosestEdgeIndex(List<Vector2> points, Vector2 normalizedMousePos, Rect imageRect)
    {
        int closestEdgeIndex = -1;
        float minDistance = 10f; // 最小的距离阈值

        for (int i = 0; i < points.Count; i++)
        {
            // 处理最后一个点与第一个点之间的直线（闭合多边形）
            Vector2 p1 = points[i];
            Vector2 p2 = points[(i + 1) % points.Count]; // 使用 % 确保最后一个点连接第一个点

            Vector2 p1ScreenPos = new Vector2(p1.x * imageRect.width, p1.y * imageRect.height) + imageRect.position;
            Vector2 p2ScreenPos = new Vector2(p2.x * imageRect.width, p2.y * imageRect.height) + imageRect.position;

            // 计算鼠标位置与该线段的距离
            float distance = HandleUtility.DistancePointToLineSegment(Event.current.mousePosition, p1ScreenPos, p2ScreenPos);

            if (distance < minDistance)
            {
                minDistance = distance;
                closestEdgeIndex = i;
            }
        }

        return closestEdgeIndex;
    }


}
