using UnityEditor;
using UnityEngine;
using System.IO;

public class EffectClipperToolNew : EditorWindow
{
    private Texture2D selectedImage;
    private Mesh generatedMesh = null;
    private ContourManager contourManager;
    private Texture2D cachedTexture = null;
    private Texture2D originalImage = null;

    private string saveFolderPath = "";
    private string saveFileName = "GeneratedMesh.obj";
    private Channel selectedChannel = Channel.RGBA;

    private Vector2 scrollPosition; // 滚动视图的位置

    [HideInInspector]
    public enum Channel
    {
        RGBA,
        R,
        G,
        B,
        A
    }

    private ImageMode selectedImageMode = ImageMode.Normal; // 新增图片模式选择

    private bool isImageLoaded = false; // 标记图片是否已加载

    // 新增枚举用于图片模式选择
    public enum ImageMode
    {
        Normal,
        Sequence
    }

    [MenuItem("Tools/特效工具/特效裁剪工具New")]
    static void ShowWindow()
    {
        EffectClipperToolNew window = GetWindow<EffectClipperToolNew>("UC特效裁剪工具");
        window.Show();
    }

    private void OnEnable()
    {
        contourManager = new ContourManager(this);
        if (contourManager.OuterContours.Count == 0)
        {
            contourManager.AddOuterContour("外轮廓1");
        }
    }

    private void OnGUI()
    {
        HandleTabKeyPress(); // 处理Tab键切换模式
        HandleEnterKeyPress(); // 处理回车键结束绘制

        // 开始滚动视图
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawToolbar();
        DrawImageModeSelector();
        DrawImageArea();
        DrawContourControls();
        DrawMeshControls();
        DrawSaveControls();

        // 结束滚动视图
        EditorGUILayout.EndScrollView();
    }

    // 处理Tab键切换模式，并阻止Tab影响其他GUI元素
    private void HandleTabKeyPress()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Tab)
        {
            GUIUtility.keyboardControl = 0;

            contourManager.IsDrawMode = !contourManager.IsDrawMode;
            Repaint();

            e.Use();
        }
    }

    // 处理回车键结束绘制
    private void HandleEnterKeyPress()
    {
        Event e = Event.current;

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Return)
        {
            if (contourManager.IsAnyContourDrawing())
            {
                contourManager.EndAllDrawings(); // 结束所有绘制
                Repaint();
            }

            e.Use();
        }
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("（绘制/删除）模式", GetButtonStyle(contourManager.IsDrawMode), GUILayout.Width(150),
                GUILayout.Height(50)))
        {
            contourManager.IsDrawMode = true;
        }

        if (GUILayout.Button("（移动/插入）模式", GetButtonStyle(!contourManager.IsDrawMode), GUILayout.Width(150),
                GUILayout.Height(50)))
        {
            contourManager.IsDrawMode = false;
        }

        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawImageModeSelector()
    {
        GUILayout.Label("图片选项", EditorStyles.boldLabel);
        GUILayout.BeginVertical("box");
        EditorGUI.indentLevel++;

        // 使用下拉列表选择图片模式，并显示为中文
        EditorGUILayout.BeginHorizontal();
        string[] imageModeOptions = new string[] { "普通图片", "序列帧图片" };
        int selectedImageModeIndex = (int)selectedImageMode;
        selectedImageModeIndex = EditorGUILayout.Popup("选择图片模式: ", selectedImageModeIndex, imageModeOptions);
        selectedImageMode = (ImageMode)selectedImageModeIndex; // 更新选中的图片模式
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        // 通道选择
        EditorGUILayout.BeginHorizontal();
        selectedChannel = (Channel)EditorGUILayout.EnumPopup("选择通道: ", selectedChannel);
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
    }


    private void DrawImageArea()
    {
        GUILayout.BeginVertical("box");
        EditorGUI.indentLevel++;
        // 图片选择框
        Texture2D newSelectedImage = ObjectFieldWithoutThumbnail("放置图片", selectedImage, typeof(Texture2D)) as Texture2D;
        if (newSelectedImage != selectedImage)
        {
            selectedImage = newSelectedImage;
            cachedTexture = null;
            isImageLoaded = false; // 当选择新图片时，需要重新点击加载
        }

        // 加载按钮部分始终显示
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // 将按钮推到中间
        if (GUILayout.Button("加载图片", GUILayout.Width(200))) // 按钮始终显示
        {
            // 无论图片是否替换，都会执行清除轮廓、孔和网格数据的操作
            contourManager.ResetContours(); // 清空轮廓和孔
            generatedMesh = null; // 清空生成的网格
            Debug.Log("现有数据已清除。");

            if (selectedImage != null && (originalImage != selectedImage || cachedTexture == null))
            {
                // 如果图片已变更或缓存为空，则重新加载图片
                originalImage = selectedImage;

                // 确保图片可读
                if (originalImage != null && originalImage.isReadable)
                {
                    cachedTexture = TextureUtils.GenerateTextureWithChannel(selectedImage, selectedChannel);
                    isImageLoaded = true; // 标记图片已加载
                    Debug.Log("图片已加载。");
                }
                else
                {
                    Debug.LogError("图片未启用读写，请启用该选项后重试。");
                }
            }

            Repaint(); // 刷新界面
        }

        GUILayout.FlexibleSpace(); // 将按钮推到中间
        GUILayout.EndHorizontal();
        GUILayout.Space(10);
        EditorGUI.indentLevel--;
        GUILayout.EndVertical();

        // 绘制图片
        if (selectedImage != null && isImageLoaded)
        {
            if (!selectedImage.isReadable)
            {
                EditorGUILayout.HelpBox("图片未启用读写，请在图片导入设置中启用读写功能。", MessageType.Error);
            }

            float windowWidth = position.width;
            float canvasWidth = windowWidth * 0.8f;
            float aspectRatio = (float)selectedImage.width / selectedImage.height;
            float canvasHeight = canvasWidth / aspectRatio;

            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            Rect imageRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight, GUILayout.Width(canvasWidth),
                GUILayout.Height(canvasHeight));
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            // 绘制已加载的图片
            GUI.DrawTexture(imageRect, cachedTexture, ScaleMode.ScaleToFit);

            // 绘制画布边框
            Handles.color = Color.white;
            Handles.DrawSolidRectangleWithOutline(imageRect, Color.clear, Color.white);

            // 绘制轮廓
            contourManager.DrawContours(imageRect);

            // 处理鼠标事件
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
        GUILayout.Space(20);
        GUILayout.BeginVertical("box");
        GUILayout.Label("生成网格", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // 将按钮推到中心
        if (GUILayout.Button("生成网格", GUILayout.Width(150)))
        {
            generatedMesh = contourManager.GenerateMeshFromContours();
        }

        if (GUILayout.Button("清除网格", GUILayout.Width(150)))
        {
            generatedMesh = null;
        }

        GUILayout.FlexibleSpace(); // 将按钮推到中心
        EditorGUILayout.EndHorizontal();
        GUILayout.Space(10);
        EditorGUILayout.EndVertical();
    }

    private void DrawSaveControls()
    {
        GUILayout.Space(10);
        GUILayout.Label("保存路径", EditorStyles.boldLabel);
        GUILayout.BeginVertical("box");
        EditorGUI.indentLevel++;
        saveFolderPath = EditorGUILayout.TextField("输入文件夹路径", saveFolderPath);
        saveFileName = EditorGUILayout.TextField("输入文件名", saveFileName);

        // 居中保存按钮
        GUILayout.Space(10);
        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace(); // 将按钮推到中心
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

        GUILayout.FlexibleSpace(); // 将按钮推到中心
        GUILayout.EndHorizontal();

        EditorGUI.indentLevel--;
        EditorGUILayout.EndVertical();
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