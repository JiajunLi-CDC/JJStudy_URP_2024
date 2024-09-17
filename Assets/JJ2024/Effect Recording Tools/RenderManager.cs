using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace SpecialEffectsRecorder
{
    public class RenderManager
    {
        public RenderTexture renderTexture;
        public Vector2 resolution = new Vector2(512, 512); // 初始默认分辨率
        private Camera currentCamera;
        public Texture2D finalTexture; // 存储合成后的最终序列帧图像

        public void CreateRenderTexture()
        {
            // 确保 resolution 的宽度和高度大于 0
            if (resolution.x <= 0 || resolution.y <= 0)
            {
                Debug.LogError("Resolution width and height must be larger than 0. Setting default resolution (512x512).");
                resolution = new Vector2(512, 512); // 默认设置
            }

            renderTexture = new RenderTexture((int)resolution.x, (int)resolution.y, 16);
        }

        public void ReleaseResources()
        {
            if (renderTexture != null)
            {
                renderTexture.Release();
                renderTexture = null;
            }

            if (finalTexture != null)
            {
                Object.DestroyImmediate(finalTexture);
                finalTexture = null;
            }
        }

        public void ApplyResolutionSettings(Camera camera, Vector2 newResolution, float newCameraSize)
        {
            if (newResolution.x <= 0 || newResolution.y <= 0)
            {
                Debug.LogError("New resolution width and height must be larger than 0. Using the current resolution.");
                newResolution = resolution; // 使用当前 resolution 而不是 0
            }

            resolution = newResolution;
            currentCamera = camera;

            if (camera != null)
            {
                camera.orthographicSize = newCameraSize;

                if (renderTexture != null)
                {
                    renderTexture.Release();
                }

                renderTexture = new RenderTexture((int)resolution.x, (int)resolution.y, 16);
                camera.targetTexture = renderTexture;
            }
        }

        public void DrawCanvasPreview()
        {
            if (renderTexture != null && currentCamera != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Camera View", EditorStyles.boldLabel);

                // 计算画布尺寸
                float windowWidth = EditorGUIUtility.currentViewWidth;
                float canvasWidth = windowWidth * 0.8f;
                float canvasHeight = canvasWidth * (resolution.y / resolution.x);

                // 绘制相机画面
                Rect canvasRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight);
                GUI.DrawTexture(canvasRect, renderTexture, ScaleMode.ScaleToFit, false);
            }
        }

    }
}
