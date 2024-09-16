using UnityEditor;
using UnityEngine;
using System.IO;

public class EffectClipperTool : EditorWindow
{
    private Texture2D selectedImage;
    private Mesh generatedMesh = null;
    private ContourManager contourManager;
    private Texture2D cachedTexture = null;
    private Texture2D originalImage = null;

    private string saveFolderPath = "";
    private string saveFileName = "GeneratedMesh.obj";
    private Channel selectedChannel = Channel.RGBA;

    [HideInInspector] public enum Channel { RGBA, R, G, B, A }

    [MenuItem("Tools/特效工具/特效裁剪工具")]
    static void ShowWindow()
    {
        EffectClipperTool window = GetWindow<EffectClipperTool>("特效裁剪工具");
        window.Show();
    }

    private void OnEnable()
    {
        contourManager = new ContourManager();
        if (contourManager.OuterContours.Count == 0)
        {
            contourManager.AddOuterContour("外轮廓1");
        }
    }

    private void OnGUI()
    {
        DrawToolbar();
        DrawChannelSelector();
        DrawImageArea();
        DrawContourControls();
        DrawMeshControls();
        DrawSaveControls();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("绘制/删除", GetButtonStyle(contourManager.IsDrawMode), GUILayout.Width(150), GUILayout.Height(50)))
        {
            contourManager.IsDrawMode = true;
        }
        if (GUILayout.Button("移动/添加", GetButtonStyle(!contourManager.IsDrawMode), GUILayout.Width(150), GUILayout.Height(50)))
        {
            contourManager.IsDrawMode = false;
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void DrawChannelSelector()
    {
        GUILayout.Label("通道选择", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        selectedChannel = (Channel)EditorGUILayout.EnumPopup("选择通道: ", selectedChannel);
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();
    }

    private void DrawImageArea()
    {
        Texture2D newSelectedImage = ObjectFieldWithoutThumbnail("放置图片", selectedImage, typeof(Texture2D)) as Texture2D;
        if (newSelectedImage != selectedImage)
        {
            selectedImage = newSelectedImage;
            contourManager.ResetContours();
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
                cachedTexture = TextureUtils.GenerateTextureWithChannel(selectedImage, selectedChannel);
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
            contourManager.HandleMouseEvents(imageRect);

            if (generatedMesh != null)
            {
                MeshUtils.DrawMeshOverlay(imageRect, generatedMesh);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("请在上方放置一张图片。", MessageType.Info);
        }
    }

    private void DrawContourControls()
    {
        GUILayout.Space(20);
        GUILayout.Label("外轮廓", EditorStyles.boldLabel);
        GUILayout.BeginVertical("box");
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("添加外轮廓", GUILayout.Width(120)))
        {
            contourManager.AddOuterContour("外轮廓" + (contourManager.OuterContours.Count + 1));
        }
        EditorGUILayout.EndHorizontal();
        contourManager.DrawOuterContourList();
        GUILayout.EndVertical();
    }

    private void DrawMeshControls()
    {
        GUILayout.Label("生成网格", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("生成网格", GUILayout.Width(150)))
        {
            generatedMesh = contourManager.GenerateMeshFromContours();
        }

        if (GUILayout.Button("清除网格", GUILayout.Width(150)))
        {
            generatedMesh = null;
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSaveControls()
    {
        GUILayout.Label("保存路径", EditorStyles.boldLabel);
        saveFolderPath = EditorGUILayout.TextField("输入文件夹路径", saveFolderPath);
        saveFileName = EditorGUILayout.TextField("输入文件名", saveFileName);

        if (GUILayout.Button("保存网格到文件", GUILayout.Width(200)))
        {
            if (!string.IsNullOrEmpty(saveFolderPath) && generatedMesh != null)
            {
                MeshExporter.SaveMeshToFile(generatedMesh, Path.Combine(saveFolderPath, saveFileName + ".obj"));
            }
            else
            {
                Debug.LogError("保存路径或网格无效。");
            }
        }
    }

    private GUIStyle GetButtonStyle(bool isActive)
    {
        return isActive ? GUIUtils.GetActiveButtonStyle() : GUIUtils.GetInactiveButtonStyle();
    }

    private Object ObjectFieldWithoutThumbnail(string label, Object obj, System.Type objType)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel(label);
        obj = EditorGUILayout.ObjectField(obj, objType, false);
        EditorGUILayout.EndHorizontal();
        return obj;
    }
}