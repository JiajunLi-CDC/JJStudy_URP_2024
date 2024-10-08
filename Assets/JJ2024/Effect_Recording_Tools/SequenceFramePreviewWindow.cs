using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO; // 用于文件保存

namespace SpecialEffectsRecorder
{
    public class SequenceFramePreviewWindow : EditorWindow
    {
        private List<RenderTexture> frameRenderTextures; // 用于存储传递进来的序列帧
        private List<Texture2D> cachedThumbnails = new List<Texture2D>(); // 缓存缩略图
        private Vector2 scrollPosition; // 用于实现滚动视图
        private List<int> selectedFrameIndices = new List<int>(); // 记录添加到序列帧的图片索引集合
        private int hoveredFrameIndex = -1; // 悬停的缩略图索引
        private int previewFrameIndex = -1; // 用于左键点击选择图片的预览索引
        private Texture2D sequenceFrameTexture; // 合成的序列帧纹理
        private int sequenceColumns = 4; // 序列帧的宽度（列数）
        private int sequenceRows = 4; // 序列帧的高度（行数）
        private bool needsUpdate = false; // 标记是否需要更新序列帧
        private float lastUpdateTime = 0f; // 上一次更新的时间
        private string saveFileName = "SequenceFrame"; // 保存图片的名称
        private string saveFilePath = ""; // 保存图片的路径

        private ComputeShader my_computeShader;

        private int lastColumns = 4; // 记录上一次的列数
        private int lastRows = 4; // 记录上一次的行数

        // 单次定义 maxSelectableFrames
        private int maxSelectableFrames => sequenceColumns * sequenceRows;

        public static void ShowWindow(List<RenderTexture> textures, ComputeShader computeShader)
        {
            var window = GetWindow<SequenceFramePreviewWindow>("序列帧预览");
            window.frameRenderTextures = textures; // 将序列帧列表传递给窗口
            window.CacheThumbnails(); // 缓存缩略图
            window.my_computeShader = computeShader;
        }

        private void CacheThumbnails()
        {
            // 清理旧的缩略图缓存，避免内存泄漏
            foreach (var thumbnail in cachedThumbnails)
            {
                if (thumbnail != null)
                {
                    Object.DestroyImmediate(thumbnail);
                }
            }

            cachedThumbnails.Clear();

            foreach (var renderTexture in frameRenderTextures)
            {
                cachedThumbnails.Add(ConvertRenderTextureToTexture2D(renderTexture)); // 生成并缓存缩略图
            }
        }
        
        private void OnDisable()
        {
            // 清理缓存的缩略图
            foreach (var thumbnail in cachedThumbnails)
            {
                if (thumbnail != null)
                {
                    Object.DestroyImmediate(thumbnail);
                }
            }
            cachedThumbnails.Clear();

            // 清理合成的序列帧纹理
            if (sequenceFrameTexture != null)
            {
                Object.DestroyImmediate(sequenceFrameTexture);
                sequenceFrameTexture = null;
            }

            // 清理选中帧的预览纹理
            if (previewFrameIndex != -1 && cachedThumbnails.Count > previewFrameIndex)
            {
                Object.DestroyImmediate(cachedThumbnails[previewFrameIndex]);
                previewFrameIndex = -1;
            }
        }



        private void OnGUI()
        {
            if (frameRenderTextures == null || frameRenderTextures.Count == 0)
            {
                GUILayout.Label("没有可显示的序列帧。", EditorStyles.boldLabel);
                return;
            }

            // 序列帧的列数和行数调节
            GUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            GUILayout.Label("设置序列帧网格:", EditorStyles.boldLabel);
            sequenceColumns = EditorGUILayout.IntField("列数", sequenceColumns);
            sequenceRows = EditorGUILayout.IntField("行数", sequenceRows);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            // 添加保存图片的输入框
            GUILayout.BeginVertical("box");
            EditorGUI.indentLevel++;
            GUILayout.Label("保存设置:", EditorStyles.boldLabel);
            saveFileName = EditorGUILayout.TextField("图片名称", saveFileName);
            saveFilePath = EditorGUILayout.TextField("保存路径", saveFilePath);
            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();

            if (GUILayout.Button("保存序列帧图片"))
            {
                SaveSequenceFrameImageWithComputeShader(my_computeShader);
            }

            GUILayout.Space(10);

            // 检测是否更改了行列数，如果更改则标记为需要更新
            if (sequenceColumns != lastColumns || sequenceRows != lastRows)
            {
                lastColumns = sequenceColumns;
                lastRows = sequenceRows;

                // 检查是否需要剔除超出范围的图片
                if (selectedFrameIndices.Count > maxSelectableFrames)
                {
                    selectedFrameIndices.RemoveRange(maxSelectableFrames,
                        selectedFrameIndices.Count - maxSelectableFrames); // 剔除超出部分
                    needsUpdate = true; // 标记为需要更新
                }
            }

            GUILayout.BeginVertical("box");
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace(); // 添加弹性空间，使图像居中显示

            // 绘制合成的序列帧显示区域
            DrawSequenceFrame();

            GUILayout.FlexibleSpace();
            GUILayout.FlexibleSpace(); // 添加弹性空间，使图像居中显示

            // 绘制当前选中的图片预览
            DrawSelectedPreview();

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);


            GUILayout.Label("序列帧预览", EditorStyles.boldLabel);

            int thumbSize = 100; // 缩略图的尺寸
            int spacing = 10; // 每个缩略图之间的间距
            float windowWidth = position.width - 40; // 获取窗口的宽度，减去滚动条和边距的宽度
            int columns = Mathf.Max(1, Mathf.FloorToInt((windowWidth + spacing) / (thumbSize + spacing))); // 确保至少有一列

            for (int i = 0; i < cachedThumbnails.Count; i += columns)
            {
                GUILayout.BeginHorizontal();

                for (int j = 0; j < columns && (i + j) < cachedThumbnails.Count; j++)
                {
                    int frameIndex = i + j;
                    Texture2D thumb = cachedThumbnails[frameIndex];

                    if (thumb != null)
                    {
                        GUILayout.BeginVertical(GUILayout.Width(thumbSize + spacing)); // 增加宽度，给边框留空间
                        GUILayout.Space(spacing / 2); // 在每个缩略图上方增加垂直间距
                        Rect thumbRect = GUILayoutUtility.GetRect(thumbSize, thumbSize);
                        GUI.Box(thumbRect, GUIContent.none); // 绘制边框
                        GUI.DrawTexture(thumbRect, thumb, ScaleMode.ScaleToFit);

                        Event currentEvent = Event.current;

                        // 检测点击事件，左键选择预览并添加，右键取消选择
                        if (thumbRect.Contains(currentEvent.mousePosition))
                        {
                            if (currentEvent.type == EventType.MouseDown)
                            {
                                if (currentEvent.button == 0) // 左键点击选择并添加
                                {
                                    previewFrameIndex = frameIndex; // 更新预览图片

                                    // 检查选择的图片是否超出帧数限制
                                    if (frameIndex >= frameRenderTextures.Count)
                                    {
                                        EditorUtility.DisplayDialog(
                                            "提示",
                                            "选择的图片超出了序列帧总数",
                                            "OK"
                                        );
                                        return;
                                    }

                                    if (!selectedFrameIndices.Contains(frameIndex) &&
                                        selectedFrameIndices.Count < maxSelectableFrames)
                                    {
                                        selectedFrameIndices.Add(frameIndex); // 添加到选中的图片中
                                        selectedFrameIndices.Sort(); // 按照原始顺序排序
                                        needsUpdate = true; // 标记为需要更新
                                    }

                                    Repaint();
                                }
                                else if (currentEvent.button == 1) // 右键点击取消选择
                                {
                                    if (selectedFrameIndices.Contains(frameIndex))
                                    {
                                        selectedFrameIndices.Remove(frameIndex); // 从选中的图片中移除
                                        needsUpdate = true; // 标记为需要更新
                                    }

                                    Repaint(); // 重新绘制窗口
                                }
                            }
                        }

                        // 如果是选中的帧，加个半透明绿色覆盖，并显示序号
                        if (selectedFrameIndices.Contains(frameIndex))
                        {
                            GUI.color = new Color(0f, 1f, 0f, 0.3f); // 绿色，带透明度
                            GUI.DrawTexture(thumbRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
                            GUI.color = Color.white;

                            // 绘制选择的序号（从 0 开始）
                            int selectionIndex = selectedFrameIndices.IndexOf(frameIndex);
                            GUIStyle labelStyle = new GUIStyle(EditorStyles.boldLabel)
                            {
                                fontSize = 16,
                                alignment = TextAnchor.MiddleCenter,
                                normal = { textColor = Color.white }
                            };

                            GUI.Label(thumbRect, selectionIndex.ToString(), labelStyle);
                        }

                        // 给左键点击的预览图片加半透明框
                        if (previewFrameIndex == frameIndex)
                        {
                            GUI.color = new Color(1f, 1f, 1f, 0.4f); // 半透明白色
                            GUI.DrawTexture(thumbRect, Texture2D.whiteTexture, ScaleMode.StretchToFill, true);
                            GUI.color = Color.white;
                        }

                        // 图片下方显示居中的帧名称，从 0 开始
                        GUILayout.BeginHorizontal();
                        GUILayout.FlexibleSpace();
                        GUILayout.Label($"Frame {frameIndex}", EditorStyles.miniLabel); // 从 0 开始编号
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                        GUILayout.Space(spacing / 2); // 在每个缩略图下方增加垂直间距
                        GUILayout.EndVertical();
                    }
                }

                GUILayout.EndHorizontal();
                GUILayout.Space(spacing); // 每行之间增加垂直间距
            }

            EditorGUILayout.EndScrollView();

            // 仅在有需要更新时，更新序列帧，限制刷新频率
            if (needsUpdate && Time.realtimeSinceStartup - lastUpdateTime > 0.5f)
            {
                GenerateSequenceFrame();
                needsUpdate = false; // 重置标志
                lastUpdateTime = Time.realtimeSinceStartup;
            }
        }

        // 绘制合成的序列帧
        private void DrawSequenceFrame()
        {
            GUILayout.BeginVertical(GUILayout.Width(350)); // 固定宽度为 350
            GUILayout.Label("合成的序列帧预览", EditorStyles.boldLabel);

            if (sequenceFrameTexture != null)
            {
                Rect canvasRect = GUILayoutUtility.GetRect(350, 350); // 固定尺寸的画布
                GUI.DrawTexture(canvasRect, sequenceFrameTexture, ScaleMode.ScaleToFit);
            }

            GUILayout.EndVertical();
        }

        // 绘制当前选中的图片预览
        private void DrawSelectedPreview()
        {
            GUILayout.BeginVertical(GUILayout.Width(350)); // 固定宽度为 350
            GUILayout.Label("当前选中的图片预览", EditorStyles.boldLabel);

            if (previewFrameIndex != -1 && cachedThumbnails.Count > previewFrameIndex)
            {
                Rect previewRect = GUILayoutUtility.GetRect(350, 350); // 固定尺寸的预览框
                GUI.DrawTexture(previewRect, cachedThumbnails[previewFrameIndex], ScaleMode.ScaleToFit);
            }

            GUILayout.EndVertical();
        }

        // 保存合成的序列帧图像并刷新 Asset
        private void SaveSequenceFrameImageWithComputeShader(ComputeShader computeShader)
        {
            if (sequenceFrameTexture == null || selectedFrameIndices.Count == 0)
            {
                EditorUtility.DisplayDialog("保存失败", "没有生成的序列帧可以保存。", "确定");
                return;
            }

            // 获取原始的 RenderTexture 并保持原始的 Alpha 通道
            int frameWidth = frameRenderTextures[0].width;
            int frameHeight = frameRenderTextures[0].height;
            int finalWidth = sequenceColumns * frameWidth;
            int finalHeight = sequenceRows * frameHeight;

            // 创建目标 RenderTexture
            RenderTexture finalRenderTexture =
                new RenderTexture(finalWidth, finalHeight, 0, RenderTextureFormat.ARGB32);
            finalRenderTexture.enableRandomWrite = true;
            finalRenderTexture.Create();

            int combineKernelIndex = computeShader.FindKernel("CombineTextures");

            for (int i = 0; i < selectedFrameIndices.Count && i < maxSelectableFrames; i++)
            {
                int frameIndex = selectedFrameIndices[i];
                RenderTexture renderTexture = frameRenderTextures[frameIndex];

                int col = i % sequenceColumns;
                int row = i / sequenceColumns;
                int posX = col * frameWidth;
                int posY = (sequenceRows - row - 1) * frameHeight; // 逆序Y坐标

                // 设置参数
                computeShader.SetTexture(combineKernelIndex, "BlackFrame", renderTexture);
                computeShader.SetTexture(combineKernelIndex, "Result", finalRenderTexture);
                computeShader.SetInts("FramePos", new int[] { posX, posY });

                // 计算工作组数
                int groupsX = Mathf.CeilToInt((float)frameWidth / 16);
                int groupsY = Mathf.CeilToInt((float)frameHeight / 16);

                // 调用 ComputeShader 进行合成
                computeShader.Dispatch(combineKernelIndex, groupsX, groupsY, 1);
            }

            // 将最终 RenderTexture 转为 Texture2D
            Texture2D finalTexture = new Texture2D(finalWidth, finalHeight, TextureFormat.RGBA32, false);
            RenderTexture.active = finalRenderTexture;
            finalTexture.ReadPixels(new Rect(0, 0, finalWidth, finalHeight), 0, 0);
            finalTexture.Apply();

            // 保存 PNG
            byte[] pngData = finalTexture.EncodeToPNG();
            string fullPath = Path.Combine(saveFilePath, $"{saveFileName}.png");
            File.WriteAllBytes(fullPath, pngData);
            EditorUtility.DisplayDialog("保存成功", $"序列帧图片已保存到：{fullPath}", "确定");

            // 刷新 Asset 数据库，确保保存的文件在 Unity 项目中可见
            AssetDatabase.Refresh();

            // 销毁 RenderTexture 和 Texture2D 对象，避免内存泄漏
            RenderTexture.active = null;
            Object.DestroyImmediate(finalRenderTexture);
            Object.DestroyImmediate(finalTexture); // 销毁生成的 Texture2D
        }


        // 根据所选图片生成序列帧
        private void GenerateSequenceFrame()
        {
            if (selectedFrameIndices.Count == 0) return;

            // 检查并销毁旧的 sequenceFrameTexture，以避免内存泄漏
            if (sequenceFrameTexture != null)
            {
                Object.DestroyImmediate(sequenceFrameTexture);
                sequenceFrameTexture = null;
            }

            // 获取每个缩略图的宽高
            int frameWidth = cachedThumbnails[0].width;
            int frameHeight = cachedThumbnails[0].height;
            int finalWidth = sequenceColumns * frameWidth;
            int finalHeight = sequenceRows * frameHeight;

            // 创建合成后的Texture2D
            sequenceFrameTexture = new Texture2D(finalWidth, finalHeight, TextureFormat.RGBA32, false);

            int selectedCount = Mathf.Min(selectedFrameIndices.Count, maxSelectableFrames);

            for (int i = 0; i < selectedCount; i++)
            {
                int frameIndex = selectedFrameIndices[i];
                Texture2D thumbnail = cachedThumbnails[frameIndex];

                // 计算每张缩略图的位置
                int col = i % sequenceColumns;
                int row = i / sequenceColumns;
                int posX = col * frameWidth;
                int posY = (sequenceRows - row - 1) * frameHeight; // 逆序Y坐标

                // 复制缩略图到合成纹理中
                Color[] pixels = thumbnail.GetPixels();
                sequenceFrameTexture.SetPixels(posX, posY, frameWidth, frameHeight, pixels);
            }

            sequenceFrameTexture.Apply();
        }


        // 将 RenderTexture 转换为 Texture2D，并将 alpha 通道强制设置为 1
        private Texture2D ConvertRenderTextureToTexture2D(RenderTexture renderTexture)
        {
            RenderTexture.active = renderTexture;
            Texture2D texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            // 获取原始像素
            Color[] pixels = texture.GetPixels();

            // 修改所有像素的 alpha 值为 1
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i].a = 1f; // 将 alpha 通道强制设置为 1（完全不透明）
            }

            // 将修改后的像素重新设置到 Texture2D 中
            texture.SetPixels(pixels);
            texture.Apply();

            return texture;
        }
    }
}