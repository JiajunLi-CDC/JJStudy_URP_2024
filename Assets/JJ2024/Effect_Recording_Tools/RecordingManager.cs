using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace SpecialEffectsRecorder
{
    public class RecordingManager
    {
        public bool IsRecording { get; private set; }
        public RenderTexture FinalTexture { get; private set; }
        private double elapsedTimeSinceStart;
        private float customStartTime;
        private float customEndTime;
        private int currentFrameIndex = -1;
        private EditorApplication.CallbackFunction updateAction;
        private RenderTexture blackRenderTexture;
        private RenderTexture whiteRenderTexture;
        private ComputeShader computeShader;
        private int computeKernelIndex;
        
        // 这里不需要重新定义 OutputMode，而是直接使用传递的 selectedOutputMode
        private SpecialEffectsRecorderWindow.OutputMode selectedOutputMode;
        private int framesPerSecond; // 每秒输出帧数
        
        // 用于存储每帧的 RenderTexture 列表
        public List<RenderTexture> frameRenderTextures = new List<RenderTexture>();
        
         // 用于存储黑白背景的 RenderTexture 列表
        private List<RenderTexture> blackFrameTextures = new List<RenderTexture>();
        private List<RenderTexture> whiteFrameTextures = new List<RenderTexture>();

        public void StartRecording(Camera camera, PlayableDirector director, Vector2 frameCount, RenderManager renderManager, ComputeShader shader, float startTime, float endTime, SpecialEffectsRecorderWindow.OutputMode outputMode, int fps = 30)
        {
            if (camera == null || director == null) return;

            ReleaseRenderTextures();          // 录制完成后释放
            ClearFrameRenderTextures();       // 清理帧数据
            
            computeShader = shader;
            computeKernelIndex = computeShader.FindKernel("CSMain");

            customStartTime = startTime;
            customEndTime = endTime;
            selectedOutputMode = outputMode;  // 使用传入的模式
            framesPerSecond = fps;

            director.time = customStartTime;
            director.Play();

            currentFrameIndex = -1;
            IsRecording = true;

            elapsedTimeSinceStart = EditorApplication.timeSinceStartup;

            int totalFrames;
            if (selectedOutputMode == SpecialEffectsRecorderWindow.OutputMode.SingleFinalTex)
            {
                totalFrames = (int)(frameCount.x * frameCount.y);
            }
            else
            {
                totalFrames = Mathf.CeilToInt((customEndTime - customStartTime) * framesPerSecond);
            }

            float totalTime = customEndTime - customStartTime;
            float frameDuration = totalTime / totalFrames;

            if (selectedOutputMode == SpecialEffectsRecorderWindow.OutputMode.SingleFinalTex)
            {
                RenderTextureDescriptor descriptor00 = new RenderTextureDescriptor((int)frameCount.x *(int)renderManager.resolution.x, (int)frameCount.y *(int)renderManager.resolution.y);
                descriptor00.colorFormat = RenderTextureFormat.ARGBFloat;
                descriptor00.sRGB = false;  // 禁用 sRGB 色彩空间

                FinalTexture = new RenderTexture(descriptor00);
                FinalTexture.enableRandomWrite = true;
                FinalTexture.Create();
                
                RenderTextureDescriptor descriptor = new RenderTextureDescriptor((int)renderManager.resolution.x, (int)renderManager.resolution.y);
                descriptor.colorFormat = RenderTextureFormat.ARGBFloat;
                descriptor.sRGB = false;  // 禁用 sRGB 色彩空间

                blackRenderTexture = new RenderTexture(descriptor);
                blackRenderTexture.enableRandomWrite = true;
                blackRenderTexture.Create();

                whiteRenderTexture = new RenderTexture(descriptor);
                whiteRenderTexture.enableRandomWrite = true;
                whiteRenderTexture.Create();
            }
            else
            {
                // 初始化黑白背景图像的 RenderTexture 列表，提前创建好
                blackFrameTextures.Clear();
                whiteFrameTextures.Clear();
                frameRenderTextures.Clear();
                for (int i = 0; i < totalFrames; i++)
                {
                    RenderTexture blackFrameTexture = new RenderTexture((int)renderManager.resolution.x, (int)renderManager.resolution.y, 0);
                    blackFrameTexture.enableRandomWrite = true;
                    blackFrameTexture.Create();
                    blackFrameTextures.Add(blackFrameTexture); // 预先创建黑色背景的 RenderTexture

                    RenderTexture whiteFrameTexture = new RenderTexture((int)renderManager.resolution.x, (int)renderManager.resolution.y, 0);
                    whiteFrameTexture.enableRandomWrite = true;
                    whiteFrameTexture.Create();
                    whiteFrameTextures.Add(whiteFrameTexture); // 预先创建白色背景的 RenderTexture
                }
            
                // 初始化 frameRenderTextures 列表
                for (int i = 0; i < totalFrames; i++)
                {
                    RenderTexture frameTexture = new RenderTexture((int)renderManager.resolution.x, (int)renderManager.resolution.y, 0);
                    frameTexture.enableRandomWrite = true;
                    frameTexture.Create();
                    frameRenderTextures.Add(frameTexture);  // 预先创建并添加到列表中
                }
                
            }
            

            updateAction = () => RecordFrames(camera, director, frameCount, totalFrames, totalTime, frameDuration, renderManager);
            EditorApplication.update += updateAction;
        }

        private void RecordFrames(Camera camera, PlayableDirector director, Vector2 frameCount, int totalFrames, float totalTime, float frameDuration, RenderManager renderManager)
        {
            double elapsedTime = EditorApplication.timeSinceStartup - elapsedTimeSinceStart;
            int frameIndex = Mathf.FloorToInt((float)(elapsedTime / frameDuration));

            if (elapsedTime >= totalTime || frameIndex >= totalFrames)
            {
                IsRecording = false;
                EditorApplication.update -= updateAction;
                if (selectedOutputMode == SpecialEffectsRecorderWindow.OutputMode.AllFrameTex)
                {
                    ProcessAllFrames();  // 录制完成后处理所有帧
                }
                ShowCompletionDialog(totalTime);  // 传递总时长信息
                return;
            }

            if (frameIndex <= currentFrameIndex) return;
            currentFrameIndex = frameIndex;
         

            if (selectedOutputMode == SpecialEffectsRecorderWindow.OutputMode.SingleFinalTex)
            {
                // 重用已经创建的黑色和白色背景的 RenderTexture
                camera.targetTexture = blackRenderTexture;
                camera.backgroundColor = Color.black;
                camera.Render();

                camera.targetTexture = whiteRenderTexture;
                camera.backgroundColor = Color.white;
                camera.Render();
                camera.targetTexture = null;
                
                WriteToFinalTexture(frameIndex, frameCount, renderManager); // 合成到 FinalTexture
            }
            else if (selectedOutputMode == SpecialEffectsRecorderWindow.OutputMode.AllFrameTex)
            {
                // 渲染黑色背景
                camera.targetTexture = blackFrameTextures[frameIndex];
                camera.backgroundColor = Color.black;
                camera.Render();

                // 渲染白色背景
                camera.targetTexture = whiteFrameTextures[frameIndex];
                camera.backgroundColor = Color.white;
                camera.Render();
                camera.targetTexture = null;
                
                // // 从 frameRenderTextures 中复用已经创建的 RenderTexture
                // RenderTexture savedFrame = frameRenderTextures[frameIndex]; // 直接复用已创建的 RenderTexture
                // ProcessSingleFrameWithComputeShader(savedFrame);
            }
        }
        
        private void ProcessAllFrames()
        {
            for (int i = 0; i < frameRenderTextures.Count; i++)
            {
                RenderTexture savedFrame = frameRenderTextures[i];

                // 设置 ComputeShader 参数
                computeShader.SetTexture(computeKernelIndex, "Result", savedFrame);
                computeShader.SetTexture(computeKernelIndex, "BlackFrame", blackFrameTextures[i]);
                computeShader.SetTexture(computeKernelIndex, "WhiteFrame", whiteFrameTextures[i]);
                
                computeShader.SetInts("FramePos", new int[] { 0, 0 });
                
                int groupX = Mathf.CeilToInt((float)blackFrameTextures[i].width / 16);
                int groupY = Mathf.CeilToInt((float)blackFrameTextures[i].height / 16);

                computeShader.Dispatch(computeKernelIndex, groupX, groupY, 1);
            }
        }

        
        private void ProcessSingleFrameWithComputeShader(RenderTexture savedFrame)
        {
            // 设置 ComputeShader 参数
            computeShader.SetTexture(computeKernelIndex, "Result", savedFrame); // 保存最终处理结果到 RenderTexture
            computeShader.SetTexture(computeKernelIndex, "BlackFrame", blackRenderTexture); // 黑色背景
            computeShader.SetTexture(computeKernelIndex, "WhiteFrame", whiteRenderTexture); // 白色背景
            
            computeShader.SetInts("FramePos", new int[] { 0, 0 });
            
            int groupX = Mathf.CeilToInt((float)blackRenderTexture.width / 16);
            int groupY = Mathf.CeilToInt((float)blackRenderTexture.height / 16);

            // 调用 ComputeShader 来处理帧合并
            computeShader.Dispatch(computeKernelIndex, groupX, groupY, 1);
            
        }

        
        private void WriteToFinalTexture(int frameIndex, Vector2 frameCount, RenderManager renderManager)
        {
            int frameWidth = (int)renderManager.resolution.x;
            int frameHeight = (int)renderManager.resolution.y;

            int posX = (frameIndex % (int)frameCount.x) * frameWidth;
            int posY = (frameIndex / (int)frameCount.x) * frameHeight;

            computeShader.SetTexture(computeKernelIndex, "Result", FinalTexture);
            computeShader.SetTexture(computeKernelIndex, "BlackFrame", blackRenderTexture);
            computeShader.SetTexture(computeKernelIndex, "WhiteFrame", whiteRenderTexture);
            computeShader.SetInts("FramePos", new int[] { posX, FinalTexture.height - posY - frameHeight });

            int groupX = Mathf.CeilToInt((float)frameWidth / 16);
            int groupY = Mathf.CeilToInt((float)frameHeight / 16);

            computeShader.Dispatch(computeKernelIndex, groupX, groupY, 1);
        }

        private void ShowCompletionDialog(float totalTime)
        {
            string totalTimeMessage = $"总时长: {totalTime:F2} 秒";
            EditorUtility.DisplayDialog("录制完成", $"序列帧录制已经完成，您可以保存图像。\n{totalTimeMessage}", "确定");
        }

        public void DrawProgressBar()
        {
            GUILayout.Space(10);
            GUILayout.Label("Recording Progress");
            float progress = (float)currentFrameIndex / 100;
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), progress, "录制进度");
        }

        public bool HasFinalTexture()
        {
            return FinalTexture != null;
        }

        public void DrawFinalTexturePreview()
        {
            if (FinalTexture != null)
            {
                GUILayout.Space(10);
                GUILayout.Label("Final Sequence Frame Preview", EditorStyles.boldLabel);

                float windowWidth = EditorGUIUtility.currentViewWidth;
                float canvasWidth = windowWidth * 0.8f;
                float canvasHeight = canvasWidth * ((float)FinalTexture.height / FinalTexture.width);

                Rect finalTextureRect = GUILayoutUtility.GetRect(canvasWidth, canvasHeight);
                GUI.DrawTexture(finalTextureRect, FinalTexture, ScaleMode.ScaleToFit, false);
            }
        }
        
        
        
        public void ReleaseRenderTextures()
        {
            if (FinalTexture != null)
            {
                FinalTexture.Release();
                Object.DestroyImmediate(FinalTexture);
            }

            if (blackRenderTexture != null)
            {
                blackRenderTexture.Release();
                Object.DestroyImmediate(blackRenderTexture);
            }

            if (whiteRenderTexture != null)
            {
                whiteRenderTexture.Release();
                Object.DestroyImmediate(whiteRenderTexture);
            }

          
        }


        public void ClearFrameRenderTextures()
        {
            // 释放提前创建的黑白背景帧的 RenderTexture 列表
            foreach (var rt in blackFrameTextures)
            {
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            blackFrameTextures.Clear();

            foreach (var rt in whiteFrameTextures)
            {
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            whiteFrameTextures.Clear();
            
            foreach (var rt in frameRenderTextures)
            {
                if (rt != null)
                {
                    rt.Release();
                    Object.DestroyImmediate(rt);
                }
            }
            frameRenderTextures.Clear();
        }

    }
}
