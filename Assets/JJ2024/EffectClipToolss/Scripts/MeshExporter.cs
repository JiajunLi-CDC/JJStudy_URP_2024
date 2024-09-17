using UnityEngine;
using System.IO;
using UnityEditor;

namespace JJEffectClipperTool
{
    public static class MeshExporter
    {
        public static void SaveMeshToFile(Mesh mesh, string path, float aspectRatio)
        {
            if (mesh == null || string.IsNullOrEmpty(path))
            {
                Debug.LogError("没有生成网格或路径无效。");
                return;
            }

            // 传递长宽比以应用缩放和镜像操作
            Mesh mirroredMesh = MeshUtils.MirrorMesh(mesh, aspectRatio);

            try
            {
                using (StreamWriter writer = new StreamWriter(path))
                {
                    writer.Write(MeshToString(mirroredMesh, Path.GetFileNameWithoutExtension(path)));
                    Debug.Log("网格已成功保存到: " + path);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError("保存网格时发生错误: " + e.Message);
            }

            AssetDatabase.Refresh();
        }


        private static string MeshToString(Mesh mesh, string meshName)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
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
    }
}