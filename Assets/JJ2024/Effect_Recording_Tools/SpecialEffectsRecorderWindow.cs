using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace SpecialEffectsRecorder
{
    public class SpecialEffectsRecorderWindow : EditorWindow
    {
        private PlayableDirector director;
        private Camera camera;
        private Vector2 newResolution = new Vector2(256, 256);
        private Vector2 frameCount = new Vector2(4, 4);
        private float newCameraSize = 5f;
        private string imageName = "SequenceFrame";
        private string savePath = "";
        private float startTime = 0f;  // 新增开始时间
        private float endTime = 1f;    // 新增结束时间
        private Vector2 scrollPosition;
        private int OutputFrameCountPerSecond = 30;
     
        
        [HideInInspector]
        public enum OutputMode
        {
            SingleFinalTex,
            AllFrameTex
        }
        private OutputMode selectedOutputMode = OutputMode.SingleFinalTex; // 新增图片模式选择

        // 使用拆分的管理类
        private RenderManager renderManager = new RenderManager();
        private RecordingManager recordingManager = new RecordingManager();

        // 引入 ComputeShader 来进行写入优化
        private ComputeShader computeShader; // ComputeShader 将传入 RecordingManager

        [MenuItem("Tools/特效工具/序列帧录制工具")]
        public static void ShowWindow()
        {
            GetWindow<SpecialEffectsRecorderWindow>("UC序列帧录制工具");
        }

        private void OnEnable()
        {
            renderManager.CreateRenderTexture();
            LoadComputeShader();
        }

        private void LoadComputeShader()
        {
            // 加载 ComputeShader
            computeShader = (ComputeShader)AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/JJ2024/Effect_Recording_Tools/FrameWritingShader.compute");    //Assets/Scripts/UC/Editor/TA/Effect_Recording_Tools/FrameWritingShader.compute
    
            // 检查是否加载成功
            if (computeShader == null)
            {
                // 弹出错误对话框
                EditorUtility.DisplayDialog(
                    "Compute Shader Not Found", 
                    "Failed to load the ComputeShader at the specified path: 'Assets/JJ2024/Effect Recording Tools/FrameWritingShader.compute'. Please ensure the file exists and the path is correct.", 
                    "OK"
                );
            }
        }

        private void OnDisable()
        {
            renderManager.ReleaseResources();
            recordingManager.ReleaseRenderTextures(); // 释放所有的 RenderTexture
            recordingManager.ClearFrameRenderTextures(); // 清理帧列表中的 RenderTexture
            
        }

        private void OnGUI()
        {
            DrawUI();
        }

        private void drawInput()
        {
            EditorGUILayout.Space();
            GUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            GUILayout.Label("相机与播放控制器", EditorStyles.boldLabel);

            camera = (Camera)EditorGUILayout.ObjectField("Camera", camera, typeof(Camera), true);
            director = (PlayableDirector)EditorGUILayout.ObjectField("PlayableDirector", director, typeof(PlayableDirector), true);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void drawSetCameraParam()
        {
            GUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            GUILayout.Label("单帧参数（相机窗口参数）", EditorStyles.boldLabel);
            newResolution = EditorGUILayout.Vector2Field("单帧长宽分辨率", newResolution);
            newCameraSize = EditorGUILayout.FloatField("单帧图片范围（相机Size）", newCameraSize);
            if (GUILayout.Button("Apply Resolution"))
            {
                renderManager.ApplyResolutionSettings(camera, newResolution, newCameraSize);
            }
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }

        private void drawRecordParam()
        {
            GUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            GUILayout.Label("录制参数设置", EditorStyles.boldLabel);
            
            // 输入开始时间和结束时间
            startTime = EditorGUILayout.FloatField("录制开始时间(秒为单位)", startTime);
            endTime = EditorGUILayout.FloatField("录制结束时间(秒为单位)", endTime);
            
            EditorGUILayout.Space();
            string[] OutputModeOptions = new string[] { "完整序列帧单张", "所有帧图片输出" };
            int selectedOutputModeIndex = (int)selectedOutputMode;
            selectedOutputModeIndex = EditorGUILayout.Popup("输出模式: ", selectedOutputModeIndex, OutputModeOptions);
            selectedOutputMode = (OutputMode)selectedOutputModeIndex; // 更新选中的图片模式
            
            if (selectedOutputMode == OutputMode.SingleFinalTex)
            {
                frameCount = EditorGUILayout.Vector2Field("序列帧的宽度和高度帧数", frameCount);
            }else if (selectedOutputMode == OutputMode.AllFrameTex)
            {
                OutputFrameCountPerSecond = EditorGUILayout.IntField("1s内输出的帧数", OutputFrameCountPerSecond);
            }
            
         
            if (GUILayout.Button("Start Recording") && !recordingManager.IsRecording)
            {
                // 传入 computeShader 和输出模式
                recordingManager.StartRecording(camera, director, frameCount, renderManager, computeShader, startTime, endTime, selectedOutputMode, OutputFrameCountPerSecond);
            }
            

         
            
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // 添加弹性空间，使图像居中显示
            if (GUILayout.Button("相机自动对齐（正方形包围盒）") && !recordingManager.IsRecording)
            {
              
                if (recordingManager.frameRenderTextures != null)
                {
                    Rect boundingBox ;
                    Vector2 boxcenter;
                    if (selectedOutputMode == OutputMode.SingleFinalTex)
                    {
                        (boundingBox,  boxcenter) = FileManager.CalculateBoundingBoxForSequence(recordingManager.FinalTexture, frameCount);
                    }
                    else 
                    {
                        (boundingBox,  boxcenter) = FileManager.CalculateBoundingBoxForMultipleFrames(recordingManager.frameRenderTextures, newResolution);
                    }
                  
                    float newCamSize = newCameraSize;
                    renderManager.ApplyBoundingBox(camera, boundingBox, boxcenter,newResolution,out newCamSize);
                    this.newCameraSize = newCamSize;
                    EditorUtility.DisplayDialog("提示", "已经自动对齐,可以开始重新录制", "确定");
                }
                else
                {
                    EditorUtility.DisplayDialog("提示", "请先拍摄序列帧图片。", "确定");
                }
              
                recordingManager.ReleaseRenderTextures();
                recordingManager.ClearFrameRenderTextures();
            }
            // if (GUILayout.Button("相机自动对齐（正方形包围盒）"))
            // {
            //     // renderManager.ApplyResolutionSettings(camera, newResolution, newCameraSize);
            // }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            
            if (recordingManager.IsRecording)
            {
                recordingManager.DrawProgressBar();
            }
            
            EditorGUI.indentLevel--;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }
        
        private void drawPreviewParam()
        {
            if (selectedOutputMode == OutputMode.AllFrameTex)
            {
                GUILayout.BeginVertical("box");
                EditorGUI.indentLevel++;
                GUILayout.Label("序列帧编辑（所有帧图片输出模式）", EditorStyles.boldLabel);

                // 显示预览序列帧的按钮
                if (GUILayout.Button("编辑序列帧"))
                {
                    if (recordingManager.frameRenderTextures.Count > 0)
                    {
                        SequenceFramePreviewWindow.ShowWindow(recordingManager.frameRenderTextures,computeShader);
                    }else
                    {
                        // 弹出提示框，提示用户先拍摄序列帧
                        EditorUtility.DisplayDialog("提示", "请先拍摄序列帧图片。", "确定");
                    }
                }

       

                EditorGUI.indentLevel--;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.Space();
            }
        }

        private void drawSaveParam()
        {
            GUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            GUILayout.Label("录制参数设置", EditorStyles.boldLabel);

            imageName = EditorGUILayout.TextField("保存图片的名称", imageName);
            savePath = EditorGUILayout.TextField("保存图片的路径", savePath);

          

            if (GUILayout.Button("保存图片"))
            {
                if (selectedOutputMode == OutputMode.SingleFinalTex && recordingManager.HasFinalTexture())
                {
                    FileManager.SaveFinalTextureAsPNG(recordingManager.FinalTexture, savePath, imageName);
                    recordingManager.ReleaseRenderTextures();
                    recordingManager.ClearFrameRenderTextures();
                }
                else if (selectedOutputMode == OutputMode.AllFrameTex && recordingManager.frameRenderTextures.Count != 0)
                {
                    FileManager.SaveFinalTextureListAsPNG(recordingManager.frameRenderTextures, savePath, imageName);
                    recordingManager.ReleaseRenderTextures();
                    recordingManager.ClearFrameRenderTextures();
                }
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }


        private void DrawUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            drawInput();
            drawSetCameraParam();
            drawRecordParam();
            drawPreviewParam();
            drawSaveParam();

            // 渲染画布预览
            renderManager.DrawCanvasPreview();

            // 显示录制好的最终图像
            if (recordingManager.HasFinalTexture())
            {
                recordingManager.DrawFinalTexturePreview();
            }

            EditorGUILayout.EndScrollView();
        }
        
    
    }
}
