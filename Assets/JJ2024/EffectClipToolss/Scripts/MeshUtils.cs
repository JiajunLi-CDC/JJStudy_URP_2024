using UnityEngine;
using System.Collections.Generic;
using Habrador_Computational_Geometry;
using UnityEditor;
using System.Linq;
using System.IO;

namespace JJEffectClipperTool
{
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
                null, hullPoints_2d_normalized, allHolePoints_2d_normalized, shouldRemoveTriangles: true,
                new HalfEdgeData2());
            HalfEdgeData2 unNormalizedTriangleData = normalizer.UnNormalize(triangleData_normalized);

            HashSet<Triangle2> triangles =
                _TransformBetweenDataStructures.HalfEdge2ToTriangle2(unNormalizedTriangleData);
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

        // 镜像网格，修正三角形顺序并根据画布比例缩放顶点
        public static Mesh MirrorMesh(Mesh originalMesh, float aspectRatio)
        {
            originalMesh.RecalculateNormals();
            Mesh mirroredMesh = new Mesh();
            Vector3[] vertices = originalMesh.vertices;
            Vector3[] normals = originalMesh.normals; // 获取原始法线
            Vector2[] uvs = new Vector2[vertices.Length];
            int[] triangles = originalMesh.triangles;

            for (int i = 0; i < vertices.Length; i++)
            {
                // 归一化 UV
                uvs[i] = new Vector2(vertices[i].x, vertices[i].z);

                // 将顶点移动到中心 [-0.5, 0.5] 范围
                vertices[i].x -= 0.5f;
                vertices[i].z -= 0.5f;

                // 左右镜像处理
                vertices[i].x = -vertices[i].x;

                // 根据画布长宽比例缩放 Z 轴和 X 轴
                if (aspectRatio > 1.0f)
                {
                    // 如果宽大于高（如2:1），缩放Z轴，使得长宽比对齐
                    vertices[i].z *= 1 / aspectRatio; // 缩放 Z 轴
                }
                else
                {
                    // 如果高大于宽（如1:2），缩放X轴，使得长宽比对齐
                    vertices[i].x *= aspectRatio; // 缩放 X 轴
                }
            }
            
            for (int i = 0; i < vertices.Length; i++)
            {
                // 左右镜像处理
                // vertices[i].x = -vertices[i].x;
                
                // 翻转轴向
                vertices[i].y = vertices[i].z;
                vertices[i].z = 0;
            }

            // 修正三角形顺序，防止法线反转
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int temp = triangles[i];
                triangles[i] = triangles[i + 2];
                triangles[i + 2] = temp;
            }

            // 更新镜像后的网格数据
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
                Vector2 pointScreenPos =
                    new Vector2(p.x * imageRect.width, p.y * imageRect.height) + imageRect.position;
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
                float distance =
                    HandleUtility.DistancePointToLineSegment(Event.current.mousePosition, p1ScreenPos, p2ScreenPos);

                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestEdgeIndex = i;
                }
            }

            return closestEdgeIndex;
        }

    }
}
