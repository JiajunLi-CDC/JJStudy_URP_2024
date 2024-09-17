using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace SpecialEffectsRecorder
{
    public class SpecialEffectsRecorderWindow : EditorWindow
    {
        private PlayableDirector director;
        private Camera camera;
        private Vector2 newResolution = new Vector2(512, 512);
        private Vector2 frameCount = new Vector2(4, 4);
        private float newCameraSize = 5f;
        private string imageName = "SequenceFrame";
        private string savePath = "";
        private Vector2 scrollPosition;

        // 使用拆分的管理类
        private RenderManager renderManager = new RenderManager();
        private RecordingManager recordingManager = new RecordingManager();

        [MenuItem("Tools/特效工具/序列帧录制工具")]
        public static void ShowWindow()
        {
            GetWindow<SpecialEffectsRecorderWindow>("Sequence Frame Recorder");
        }

        private void OnEnable()
        {
            renderManager.CreateRenderTexture();
        }

        private void OnDisable()
        {
            renderManager.ReleaseResources();
        }

        private void OnGUI()
        {
            DrawUI();
        }

        private void DrawUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            GUILayout.Label("Sequence Frame Recorder", EditorStyles.boldLabel);

            camera = (Camera)EditorGUILayout.ObjectField("Camera", camera, typeof(Camera), true);
            director = (PlayableDirector)EditorGUILayout.ObjectField("PlayableDirector", director, typeof(PlayableDirector), true);

            newResolution = EditorGUILayout.Vector2Field("单帧长宽分辨率", newResolution);
            newCameraSize = EditorGUILayout.FloatField("单帧图片范围（相机Size）", newCameraSize);

            if (GUILayout.Button("Apply Resolution"))
            {
                renderManager.ApplyResolutionSettings(camera, newResolution, newCameraSize);
            }

            frameCount = EditorGUILayout.Vector2Field("序列帧的宽度和高度帧数", frameCount);

            if (GUILayout.Button("Start Recording") && !recordingManager.IsRecording)
            {
                recordingManager.StartRecording(camera, director, frameCount, renderManager);
            }

            if (recordingManager.IsRecording)
            {
                recordingManager.DrawProgressBar();
            }

            imageName = EditorGUILayout.TextField("保存图片的名称", imageName);
            savePath = EditorGUILayout.TextField("保存图片的路径", savePath);

            if (GUILayout.Button("Save Image") && recordingManager.HasFinalTexture())
            {
                Debug.Log("sdfa"+recordingManager.FinalTexture.width+"........."+recordingManager.FinalTexture.height);
                FileManager.SaveFinalTextureAsPNG(recordingManager.FinalTexture, savePath, imageName);
            }

            renderManager.DrawCanvasPreview(); // 确保渲染画布预览的功能完整
            if (recordingManager.HasFinalTexture())
            {
                recordingManager.DrawFinalTexturePreview();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
