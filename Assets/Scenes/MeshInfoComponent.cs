using UnityEngine;
using UnityEditor;

[RequireComponent(typeof(MeshFilter))] // 确保对象有MeshFilter
public class MeshInfoComponent : MonoBehaviour
{
    // 这里可以定义你需要的变量或方法，比如开始时获取网格
    // 在Inspector中不会显示
    public Mesh mesh;

    void Start()
    {
        // MeshFilter meshFilter = GetComponent<MeshFilter>();
        // if (meshFilter != null)
        // {
        //     mesh = meshFilter.mesh;
        // }
    }

    // 用于打印顶点和UV信息的函数
    public void PrintMeshInfo()
    {
        if (mesh == null)
        {
            Debug.LogError("没有找到Mesh！");
            return;
        }

        Vector3[] vertices = mesh.vertices;
        Vector2[] uvs = mesh.uv;

        for (int i = 0; i < vertices.Length; i++)
        {
            Debug.Log($"顶点 {i}: 位置 = {vertices[i]}, UV = {(uvs.Length > i ? uvs[i].ToString() : "无")}");
        }
    }
}

// 自定义Inspector，添加按钮
[CustomEditor(typeof(MeshInfoComponent))]
public class MeshInfoComponentEditor : Editor
{
    public override void OnInspectorGUI()
    {
        // 绘制默认的Inspector
        DrawDefaultInspector();

        // 获取目标脚本的引用
        MeshInfoComponent meshInfoComponent = (MeshInfoComponent)target;

        // 在Inspector中添加按钮
        if (GUILayout.Button("打印网格信息"))
        {
            // 调用脚本中的方法，打印网格顶点和UV
            meshInfoComponent.PrintMeshInfo();
        }
    }
}