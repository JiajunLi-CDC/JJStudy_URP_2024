using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using System.Collections.Generic;

namespace SpecialEffectsRecorder
{
    public class RecordingManager
    {
        public bool IsRecording { get; private set; }
        public Texture2D FinalTexture { get; private set; }
        private double startTime;
        private int currentFrameIndex = -1;
        private EditorApplication.CallbackFunction updateAction;
        private List<RenderTexture> renderTextures; // 用于存储每一帧的 RenderTexture

        public void StartRecording(Camera camera, PlayableDirector director, Vector2 frameCount, RenderManager renderManager)
        {
            if (camera == null || director == null) return;

            director.Play();
            director.time = 0;

            startTime = EditorApplication.timeSinceStartup;
            currentFrameIndex = -1;
            IsRecording = true;

            int totalFrames = (int)(frameCount.x * frameCount.y);
            float totalTime = (float)director.duration;
            float frameDuration = totalTime / totalFrames;

            // 创建一个 Texture2D，用于存储最终的帧
            FinalTexture = new Texture2D((int)frameCount.x * (int)renderManager.resolution.x, (int)frameCount.y * (int)renderManager.resolution.y, TextureFormat.RGBA32, false);

            // 初始化存储 RenderTexture 的列表
            renderTextures = new List<RenderTexture>();

            updateAction = () => RecordFrames(camera, director, frameCount, totalFrames, totalTime, frameDuration, renderManager);
            EditorApplication.update += updateAction;
        }

        private void RecordFrames(Camera camera, PlayableDirector director, Vector2 frameCount, int totalFrames, float totalTime, float frameDuration, RenderManager renderManager)
        {
            double elapsedTime = EditorApplication.timeSinceStartup - startTime;
            int frameIndex = Mathf.FloorToInt((float)(elapsedTime / frameDuration));

            if (elapsedTime >= totalTime || frameIndex >= totalFrames)
            {
                IsRecording = false;
                EditorApplication.update -= updateAction;

                // 在录制完成时将所有 RenderTexture 合并到 FinalTexture
                CombineRenderTexturesToFinal(frameCount, renderManager);
                ShowCompletionDialog();
                return;
            }

            if (frameIndex <= currentFrameIndex) return;
            currentFrameIndex = frameIndex;

            // 渲染当前帧到一个新的 RenderTexture
            RenderTexture frameRT = new RenderTexture((int)renderManager.resolution.x, (int)renderManager.resolution.y, 0);
            camera.targetTexture = frameRT;
            camera.Render();

            // 存储 RenderTexture 以便在录制完成后处理
            renderTextures.Add(frameRT);

            // 重置相机目标
            camera.targetTexture = null;
        }

        private void CombineRenderTexturesToFinal(Vector2 frameCount, RenderManager renderManager)
        {
            int frameWidth = (int)renderManager.resolution.x;
            int frameHeight = (int)renderManager.resolution.y;

            for (int i = 0; i < renderTextures.Count; i++)
            {
                RenderTexture.active = renderTextures[i];
                Texture2D tempTexture = new Texture2D(frameWidth, frameHeight, TextureFormat.RGB24, false);
                tempTexture.ReadPixels(new Rect(0, 0, frameWidth, frameHeight), 0, 0);
                tempTexture.Apply();

                int posX = (i % (int)frameCount.x) * frameWidth;
                int posY = (i / (int)frameCount.x) * frameHeight;

                // 将 RenderTexture 中的内容写入到 FinalTexture
                FinalTexture.SetPixels(posX, FinalTexture.height - posY - frameHeight, frameWidth, frameHeight, tempTexture.GetPixels());

                // 释放临时的 Texture2D 和 RenderTexture
                Object.DestroyImmediate(tempTexture);
                renderTextures[i].Release();
                Object.DestroyImmediate(renderTextures[i]);
            }

            FinalTexture.Apply();
            RenderTexture.active = null;

            Debug.Log("合并完成，录制已结束。");
        }

        private void ShowCompletionDialog()
        {
            // 使用 EditorUtility.DisplayDialog 显示弹窗，通知录制完成
            EditorUtility.DisplayDialog("录制完成", "序列帧录制已经完成，您可以保存图像。", "确定");
        }

        public void DrawProgressBar()
        {
            GUILayout.Space(10);
            GUILayout.Label("Recording Progress");
            float progress = (float)currentFrameIndex / 100; // 假设进度为100帧
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, "录制进度");
        }

        public bool HasFinalTexture()
        {
            return FinalTexture != null;
        }

        // 新增的方法，用于绘制最终序列帧的预览
        public void DrawFinalTexturePreview()
        {
            if (FinalTexture != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Final Sequence Frame Preview", EditorStyles.boldLabel);

                float windowWidth = EditorGUIUtility.currentViewWidth;
                float canvasWidth = windowWidth * 0.8f;
                float canvasHeight = canvasWidth * ((float)FinalTexture.height / FinalTexture.width);

                // 绘制合成后的最终序列帧
                Rect finalTextureRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight);
                GUI.DrawTexture(finalTextureRect, FinalTexture, ScaleMode.ScaleToFit, false);
            }
        }
    }
}
