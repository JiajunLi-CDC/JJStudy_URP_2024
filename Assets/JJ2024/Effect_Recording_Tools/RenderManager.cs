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

        public void CreateRenderTexture()
        {
            // 确保 resolution 的宽度和高度大于 0
            if (resolution.x <= 0 || resolution.y <= 0)
            {
                EditorUtility.DisplayDialog(
                    "Message",
                    "Resolution width and height must be larger than 0. Setting default resolution (512x512).",
                    "OK"
                );
                // Debug.LogError("Resolution width and height must be larger than 0. Setting default resolution (512x512).");
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
        }

        public void ApplyResolutionSettings(Camera camera, Vector2 newResolution, float newCameraSize)
        {
            if (newResolution.x <= 0 || newResolution.y <= 0)
            {
                EditorUtility.DisplayDialog(
                    "Message",
                    "New resolution width and height must be larger than 0. Using the current resolution.",
                    "OK"
                );
                // Debug.LogError("New resolution width and height must be larger than 0. Using the current resolution.");
                newResolution = resolution; // 使用当前 resolution 而不是 0
            }

            resolution = newResolution;
            currentCamera = camera;
            currentCamera.backgroundColor = Color.black;

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

        public void ApplyBoundingBox(Camera camera, Rect boundingbox, Vector2 boundingboxCenter, Vector2 myResolution,out float newCamSize)
        {
            if (boundingbox.x <= 0 || boundingbox.y <= 0)
            {
                EditorUtility.DisplayDialog(
                    "Message",
                    "boundingbox大小为0",
                    "OK"
                );
            }

            newCamSize = camera.orthographicSize;
            currentCamera = camera;
            currentCamera.backgroundColor = Color.black;
            this.resolution = myResolution;

            // resolution = newResolution;
            if (camera != null)
            {
                float totalWorldSize = camera.orthographicSize * 2f;
                Vector2 cha = boundingboxCenter - 0.5f * resolution;
                Vector2 ratio = cha / resolution;
                
                Vector3 newCameraPosition = camera.transform.position;
                newCameraPosition.x += ratio.x * totalWorldSize;
                newCameraPosition.z += ratio.y * totalWorldSize;
                camera.transform.position = newCameraPosition;

                float width = boundingbox.width;
                float height = boundingbox.height;
                float newSize01 = (width / resolution.x) * camera.orthographicSize * 2f;
                float newSize02 = (height / resolution.y) * camera.orthographicSize * 2f;
                float newSize = Mathf.Max(newSize01, newSize02);
                
                newCamSize = newSize * 0.5f *1.05f;
                camera.orthographicSize = newCamSize;
                
               

                // camera.orthographicSize = CameraSize;

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