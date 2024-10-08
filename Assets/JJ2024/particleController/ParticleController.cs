using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UCParticleControl
{
    [System.Serializable]
    public struct ParticleSettings
    {
        public int ParticleNumber;
        public float startDelay;
        public float startLifetime;
        public float startSize;
        [Header("速度参数")]
        public Vector3 velocity;
        public bool randomSpeed;
        public Vector3 minVelocity;
        public Vector3 maxVelocity;
        [Header("旋转参数")]
        public Vector3 Rotation;
        public bool randomRotation;
        public Vector3 minRotation;
        public Vector3 maxRotation;

        public ParticleSettings(float startDelay = 0, float lifetime = 1.0f, float size = 1.0f,
            Vector3? velocity = null, int particleNumber = 1, Vector3? rotation = null)
        {
            this.startDelay = startDelay;
            startLifetime = lifetime;
            startSize = size;
            this.velocity = velocity ?? Vector3.zero;
            ParticleNumber = particleNumber;
            this.Rotation = rotation ?? Vector3.zero;

            randomSpeed = false;
            minVelocity = Vector3.zero;
            maxVelocity = Vector3.zero;

            randomRotation = false;
            minRotation = Vector3.zero;
            maxRotation = Vector3.zero;
        }
    }

    [System.Serializable]
    public class EmitParamsEntry
    {
        public ParticleSystem.EmitParams emitParams;
        public int ParticleIndex; // 粒子索引

        public EmitParamsEntry(ParticleSystem.EmitParams emitParams, int index)
        {
            this.emitParams = emitParams;
            this.ParticleIndex = index;
        }
    }

    [ExecuteAlways]
    public class ParticleController : MonoBehaviour
    {
        public enum CoordinateSpace
        {
            World,
            Local
        }

        private ParticleSystem particleSystem;
        [Header("粒子位置")]
        public CoordinateSpace coordinateSpace = CoordinateSpace.Local;
        public GameObject parentObject;
        [SerializeField] public List<Transform> transformList = new List<Transform>();

        [Header("Debug位置可视化")]
        public bool showPointsInGizmos = false;
        public float gizmoSphereSize = 0.1f;

        [Header("粒子属性")]
        public List<ParticleSettings> particleSettingLists;

        private bool isPlaying = false;
        private float startTime;
        int timeIndex = 0;

        [SerializeField, HideInInspector]
        private Dictionary<float, List<EmitParamsEntry>> emitParamsDict = new Dictionary<float, List<EmitParamsEntry>>();
        private List<float> sortedEmissionTimes = new List<float>(); // 存储排序后的发射时间列

        void Awake()
        {
            InitializeParticle();
        }

        // private void Start()
        // {
        //     InitializeParticle();
        // }

        void OnEnable()
        {
            // // 确保粒子系统正在播放并调用发射
            // if (!isPlaying)
            // {
                Debug.Log("开始发射粒子");
                startTime = Time.time;
                isPlaying = true;  // 设置为 true 后再启动协程
                timeIndex = 0;
                StartCoroutine(EmitParticlesAtPositions());
            // }
        }

        
        void OnValidate()
        {
            InitializeParticle();
        }

        public void InitializeParticle()
        {
            particleSystem = GetComponent<ParticleSystem>();

            if (particleSystem == null)
            {
                Debug.LogError("请确保该物体上添加了粒子系统组件！！！");
                return;
            }

            // 设置粒子系统的坐标系
            var mainModule = particleSystem.main;
            if (coordinateSpace == CoordinateSpace.World)
            {
                mainModule.simulationSpace = ParticleSystemSimulationSpace.World;
            }
            else if (coordinateSpace == CoordinateSpace.Local)
            {
                mainModule.simulationSpace = ParticleSystemSimulationSpace.Local;
            }
            // 关闭原有粒子的位置与发射信息
            var shapeModule = particleSystem.shape;
            shapeModule.enabled = false;
            var emissionModule = particleSystem.emission;
            emissionModule.enabled = false;

            isPlaying = false;

            UpdateTransformList();
            PrecalculateEmitParams(); // 初始化时预计算发射参数
            Debug.Log("粒子已经初始化");
        }

        void Update()
        {
        #if UNITY_EDITOR
            if (particleSystem.isPlaying && !isPlaying)
            {
                Debug.Log("开始发射粒子");
                startTime = Time.time;
                isPlaying = true;  // 设置为 true 后再启动协程
                timeIndex = 0;
                StartCoroutine(EmitParticlesAtPositions());
            }
            else if (timeIndex >= sortedEmissionTimes.Count && !particleSystem.isPlaying)
            {
                isPlaying = false;
            }
        #endif
        }

        private void PrecalculateEmitParams()
        {
            emitParamsDict.Clear();
            int particleIndex = 0; // 初始化粒子索引为0

            for (int i = 0; i < transformList.Count; i++)
            {
                var transform = transformList[i];
                ParticleSettings settings = particleSettingLists[i];

                if (transform != null)
                {
                    for (int j = 0; j < settings.ParticleNumber; j++)
                    {
                        var emitParams = new ParticleSystem.EmitParams
                        {
                            // position = transform.localPosition,
                            applyShapeToPosition = false,
                            startLifetime = settings.startLifetime,
                            startSize = settings.startSize,
                            velocity = settings.randomSpeed
                                ? new Vector3(
                                    Random.Range(settings.minVelocity.x, settings.maxVelocity.x),
                                    Random.Range(settings.minVelocity.y, settings.maxVelocity.y),
                                    Random.Range(settings.minVelocity.z, settings.maxVelocity.z))
                                : settings.velocity,
                            rotation3D = settings.randomRotation
                                ? new Vector3(
                                    Random.Range(settings.minRotation.x, settings.maxRotation.x),
                                    Random.Range(settings.minRotation.y, settings.maxRotation.y),
                                    Random.Range(settings.minRotation.z, settings.maxRotation.z))
                                : settings.Rotation
                        };
                        
                        //选择坐标系
                        emitParams.position = coordinateSpace == CoordinateSpace.World 
                            ? transform.position 
                            : transform.localPosition; 

                        var entry = new EmitParamsEntry(emitParams, particleIndex);

                        // 将粒子按照发射时间分组
                        if (!emitParamsDict.ContainsKey(settings.startDelay))
                        {
                            emitParamsDict[settings.startDelay] = new List<EmitParamsEntry>();
                        }
                        emitParamsDict[settings.startDelay].Add(entry);

                        particleIndex++;
                    }
                }
            }

            // 获取并排序所有的发射时间
            sortedEmissionTimes = new List<float>(emitParamsDict.Keys);
            sortedEmissionTimes.Sort();
        }
        

        private void OnDrawGizmos()
        {
            if (showPointsInGizmos && transformList != null)
            {
                Gizmos.color = Color.red;
                foreach (var trans in transformList)
                {
                    if (trans != null)
                    {
                        Gizmos.DrawSphere(trans.position, gizmoSphereSize);
                    }
                }
            }
        }
        
        private IEnumerator EmitParticlesAtPositions()
        {
            if (transformList.Count == 0)
            {
                Debug.LogError("Transform list is empty, cannot emit particles.");
                yield break;
            }

            if (particleSettingLists.Count < transformList.Count)
            {
                Debug.LogError("Particle settings list is shorter than transform list.");
                yield break;
            }

            particleSystem.Play(); // 开始播放粒子系统
    
            while (timeIndex < sortedEmissionTimes.Count)
            {
                float currentTime = Time.time - startTime;
                float emissionTime = sortedEmissionTimes[timeIndex];

                // 计算从当前时间到下次发射的等待时间
                float waitTime = emissionTime - currentTime;
                if (waitTime > 0)
                {
                    // 只在需要等待的情况下才暂停协程
                    yield return new WaitForSeconds(waitTime);
                }

                // 发射粒子
                var entries = emitParamsDict[emissionTime];
                foreach (var entry in entries)
                {
                    particleSystem.Emit(entry.emitParams, 1);
                    // Debug.Log($"已经发射粒子 索引: {entry.ParticleIndex}");
                }
                timeIndex++;
            }
    
            // // 发射完成后重置 isPlaying 状态
            // isPlaying = false;
        }


        private void UpdateTransformList()
        {
            if (parentObject != null)
            {
                transformList.Clear();
                foreach (Transform child in parentObject.transform)
                {
                    transformList.Add(child);
                }
            }
        }

        public void PlayParticles()
        {
            if (!particleSystem.isPlaying)
            {
                particleSystem.Play();
            }
        }

        public void StopParticles()
        {
            if (particleSystem.isPlaying)
            {
                particleSystem.Stop();
            }
        }
    }
}
