﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TMPro;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Random = UnityEngine.Random;

namespace UnityTemplateProjects
{
    [Serializable]
    public struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Vector3 color;
        public Vector3 specular;
        public Vector3 emission;
        public float smoothness;
        
        public Sphere(Vector3 position, float radius, Vector3 color, Vector3 specular, Vector3 emission, float smoothness)
        {
            this.position = position;
            this.radius = radius;
            this.color = color;
            this.specular = specular;
            this.emission = emission;
            this.smoothness = smoothness;
        }

        public void Move(Vector3 delta)
        {
            position += delta;
        }
    }

    public struct ShaderMesh
    {
        public Matrix4x4 localToWorld;
        public int eboOffset;
        public int eboCount;
        public Vector4 color;
    }
    
    public struct MeshBoundingBox
    {
        public int indexMesh;
        public Vector3 max;
        public Vector3 min;
    }
    
    public class RayTracingManager : MonoBehaviour
    {
        public ComputeShader shaderSorter;
        private ComputeBuffer bufferRays;
        private ComputeBuffer bufferRaysCount;
        
        public ComputeShader shaderReflections;
        public ComputeShader shaderRT;
        public RenderTexture tex;
        public RenderTexture cumulationTex;
        public RenderTexture reflectionTexture;
        private ComputeBuffer buffer;
        public Texture skybox;
        
        private ComputeBuffer bufferMeshVertices;
        private ComputeBuffer bufferMeshEbo;
        private ComputeBuffer bufferMeshes;
        private ComputeBuffer bufferMeshesUV;

        public List<Sphere> spheres;
        public int sphereAmount;
        public int sphereSeed;

        private static List<RayTracingSubscriber> Subscribers;
        private static List<Transform> _transformsToWatch;

        private uint _sampleRate;
        private static readonly int Sample = Shader.PropertyToID("_Sample");
        private Material AA;

        private Material Mixer;

        private Camera _cam;
        public Light directionalLight;
        public RenderTexture tex0;
        public static RayTracingManager instance;
        private Texture2D screenTex;
        private static readonly int PathTracedShadowsReflections = Shader.PropertyToID("_PathTracedShadowsReflections");

        private List<int> kernels;

        public BVHDebuger bvh;
        private ComputeBuffer BVHBuffer;

        public bool raySortingMode;
        public TextMeshProUGUI text;

        private void Awake()
        {
            Subscribers = new List<RayTracingSubscriber>();
            _transformsToWatch = new List<Transform>();
            _cam = GetComponent<Camera>();
            _cam.depthTextureMode |= DepthTextureMode.Depth;
            
            kernels = new List<int>();

            instance = this;
        }

        public static void Register(RayTracingSubscriber sub)
        {
            Subscribers.Add(sub);
            _transformsToWatch.Add(sub.transform);
        }

        public static void UnRegister(RayTracingSubscriber sub)
        {
            Subscribers.Remove(sub);
            _transformsToWatch.Remove(sub.transform);
        }

        List<Sphere> GenerateSpheres(int amount)
        {
            List<Sphere> sphereList = new List<Sphere>(amount);
            for (int i = 0; i < amount; i++)
            {
                Sphere s = new Sphere();
                float emittingChance = Random.value;
                
                if (emittingChance < 0.2f)
                {
                    bool metal = Random.value < 0.2f;
                    Color randomColor = Random.ColorHSV();
                    
                    s.color = metal ? Vector3.zero : new Vector3(randomColor.r, randomColor.g, randomColor.b);
                    s.specular = metal
                        ? new Vector3(randomColor.r, randomColor.g, randomColor.b)
                        : new Vector3(0.04f, 0.04f, 0.04f);
                    s.smoothness = Random.value;
                }
                else
                {
                    //creates a random light color
                    Color emission = Random.ColorHSV(0, 1, 0, 1, 3.0f, 8.0f);
                    s.emission = new Vector3(emission.r, emission.g, emission.b);
                }
                
                s.position = (Random.insideUnitSphere * 10) + Vector3.forward * 2;
                s.radius = Random.value * 2.0f;
                
                
                sphereList.Add(s);
            }

            return sphereList;
        }

        private void OnDrawGizmos()
        {
            if (spheres != null)
            {
                foreach (Sphere sphere in spheres)
                {
                    Gizmos.DrawWireSphere(sphere.position, sphere.radius);
                }
            }
        }

        private void Start()
        {
            screenTex = new Texture2D(Screen.width, Screen.height);
            
            tex = new RenderTexture(Screen.width, Screen.height, 0) {enableRandomWrite = true};
            //tex.format = RenderTextureFormat.RGB111110Float;
            tex.Create();
            
            cumulationTex = new RenderTexture(Screen.width / 4, Screen.height / 4, 0) {enableRandomWrite = true};
            cumulationTex.Create();
            
            reflectionTexture = new RenderTexture(Screen.width / 4, Screen.height / 4, 0) { enableRandomWrite = true};
            reflectionTexture.Create();
            
            
            tex0 = new RenderTexture(Screen.width, Screen.height, 0);
            tex0.Create();

            Mixer = new Material(Shader.Find("Hidden/Mixer"));
            
            AA = new Material(Shader.Find("Hidden/AART"));

            Random.InitState(sphereSeed);
            spheres = GenerateSpheres(sphereAmount);

            buffer = new ComputeBuffer(spheres.Count, sizeof(float) * 14);
            buffer.SetData(spheres);

            CreateMeshBuffers();

            kernels.Add(shaderRT.FindKernel("ShadowPass"));
            kernels.Add(shaderRT.FindKernel("ReflectionPass"));
           // kernels.Add(shaderRT.FindKernel("AOPass"));
            

            foreach (int kernel in kernels)
            {
                shaderRT.SetTexture(kernel, "texOut", tex);
                shaderRT.SetBuffer(kernel, "spheres", buffer);
            
                shaderRT.SetBuffer(kernel, "meshes", bufferMeshes);
                shaderRT.SetBuffer(kernel, "meshVertices", bufferMeshVertices);
                shaderRT.SetBuffer(kernel, "meshEbo", bufferMeshEbo);
                shaderRT.SetBuffer(kernel, "bvhTree", BVHBuffer);
            
                shaderRT.SetTexture(kernel, "skybox", skybox);
            }
           
            shaderRT.SetTexture(1, "texOut", reflectionTexture);
            
            shaderReflections.SetTexture(0, "texOut", reflectionTexture);
            shaderReflections.SetBuffer(0, "bvhTree", BVHBuffer);
            shaderReflections.SetTexture(0, "skybox", skybox);

            shaderReflections.SetBuffer(0, "meshes", bufferMeshes);
            shaderReflections.SetBuffer(0, "meshVertices", bufferMeshVertices);
            shaderReflections.SetBuffer(0, "meshEbo", bufferMeshEbo);
            
            shaderReflections.SetMatrix("worldToCamera", _cam.worldToCameraMatrix);
            shaderReflections.SetMatrix("invProj", _cam.projectionMatrix.inverse);

            shaderRT.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
            shaderRT.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);


            shaderSorter.SetTexture(0, "texOut", reflectionTexture);
            shaderSorter.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
            shaderSorter.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);
            

            bufferRays = new ComputeBuffer(reflectionTexture.width * reflectionTexture.height, sizeof(float) * 12,
                ComputeBufferType.Append);
            bufferRays.SetCounterValue(0);
            
            bufferRaysCount = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);
            
            shaderSorter.SetBuffer(0, "rays", bufferRays);
            shaderReflections.SetBuffer(0, "rays", bufferRays);

            var position = directionalLight.transform.forward;
            shaderRT.SetVector("directionalLight", new Vector4(position.x, position.y, position.z, directionalLight.intensity));

            Mixer.SetTexture("_TracedShadows", tex);
            Mixer.SetTexture("_TracedReflections", cumulationTex);

            RenderPipelineManager.endCameraRendering += RenderPipelineManagerOnendCameraRendering;
        }

        private void RenderPipelineManagerOnendCameraRendering(ScriptableRenderContext arg1, Camera arg2)
        {
            if (arg2.name != "Main Camera") return;
            
            CommandBuffer cmd = new CommandBuffer();
            cmd.name = "Hybrid Renderer";
            
            shaderRT.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
            shaderRT.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);
            
             if (_sampleRate < 100) //stacking frames for TAA
            {
                if (raySortingMode)
                {
                    reflectionTexture.Release();
                    reflectionTexture.Create();
               
                    shaderSorter.SetTexture(0, "texOut", reflectionTexture);
                    shaderReflections.SetTexture(0, "texOut", reflectionTexture);

                    shaderSorter.SetFloats("pixelOffset", Random.value, Random.value);
                    shaderSorter.SetFloat("seed", Random.value);
                
                    shaderReflections.SetMatrix("worldToCamera", _cam.worldToCameraMatrix);
                    shaderReflections.SetMatrix("invProj", _cam.projectionMatrix);
                
                    shaderSorter.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
                    shaderSorter.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);
                
                    bufferRays.SetCounterValue(0);
                
                    shaderSorter.Dispatch( 0,reflectionTexture.width / 8, reflectionTexture.height / 8, 1);

                    ComputeBuffer.CopyCount(bufferRays, bufferRaysCount, 0);
                    int[] a = new int[1];
                
                    bufferRaysCount.GetData(a);
                
                    if(a[0] > 64)
                        shaderReflections.Dispatch(0, a[0] / 64, 1, 1);
                }
                else
                {
                    cmd.DispatchCompute(shaderRT, 1, reflectionTexture.width / 8, reflectionTexture.height / 8, 1);
                }
                
                
                cmd.Blit(reflectionTexture, cumulationTex, AA);

                AA.SetFloat(Sample, _sampleRate);
                _sampleRate++;
            }

            cmd.DispatchCompute(shaderRT, 0, tex.width / 8, tex.height / 8, 1);

            cmd.Blit(tex, arg2.activeTexture, Mixer);
            arg1.ExecuteCommandBuffer(cmd);
            cmd.Release();
        }


        private void CreateMeshBuffers()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> ebos = new List<int>();
            List<ShaderMesh> meshObjects = new List<ShaderMesh>(Subscribers.Count);
            List<MeshRenderer> renderers = new List<MeshRenderer>(Subscribers.Count);
            List<Vector2Int> meshesVertexOffsets = new List<Vector2Int>(Subscribers.Count);
            
            
            foreach (RayTracingSubscriber subscriber in Subscribers)
            {
                var renderer = subscriber.GetComponent<MeshRenderer>();
                renderers.Add(renderer);
            }
            
            //TODO: try to launch a thread that updates the bvh, concurrently dispatch the cs, join the thread after that update bvh,
            //update meshes vertices infos
            Bounds[] bounds = new Bounds[renderers.Count];
            Matrix4x4[] localToWorlds = new Matrix4x4[renderers.Count];
            for (int i = 0; i < renderers.Count; i++)
            {
                bounds[i] = renderers[i].bounds;
                localToWorlds[i] = renderers[i].transform.localToWorldMatrix;
            }
            
            bvh.bvh = new BVH(bounds, localToWorlds, ref Subscribers);
            
            renderers.Clear();

            for (int i = 0; i < Subscribers.Count; i++)
            {
                var meshObject = Subscribers[i].gameObject;
                var renderer = meshObject.GetComponent<MeshRenderer>();
                var mesh = meshObject.GetComponent<MeshFilter>().sharedMesh;
                
//                print(renderer.transform);
                renderers.Add(renderer);
                
                ShaderMesh m;
                m.eboOffset = ebos.Count;
                m.localToWorld = meshObject.transform.localToWorldMatrix;
                var c = renderer.material.color;
                m.color = c;
                
                var indices = mesh.GetIndices(0);

                ebos.AddRange(indices.Select(index => index + vertices.Count));
                m.eboCount = ebos.Count;
                
                Vector2Int offsets = Vector2Int.zero;
                offsets.x = vertices.Count;
                vertices.AddRange(mesh.vertices);
                offsets.y = vertices.Count;
                
                meshesVertexOffsets.Add(offsets);

                meshObjects.Add(m);
            }

            bufferMeshEbo = new ComputeBuffer(ebos.Count, sizeof(int));
            bufferMeshEbo.SetData(ebos);

            bufferMeshes = new ComputeBuffer(meshObjects.Count, sizeof(float) * 16 + 2 * sizeof(int) + 4 * sizeof(float));
            bufferMeshes.SetData(meshObjects);
            
            for (int i = 0; i < meshObjects.Count; i++)
            {
                Vector2Int m = meshesVertexOffsets[i];
                Matrix4x4 localToWorld = meshObjects[i].localToWorld;
                for (int j = m.x ; j < m.y; j++)
                {
                    Vector4 v = vertices[j];
                    v.w = 1.0f;

                    vertices[j] = localToWorld * v;
                }
            }

            //TODO: make this a compute shader
           /* Thread th = new Thread(o =>
            {
                for (int i = 0; i < meshObjects.Count; i++)
                {
                    Vector2Int m = meshesVertexOffsets[i];
                    Matrix4x4 localToWorld = meshObjects[i].localToWorld;
                    for (int j = m.x ; j < m.y; j++)
                    {
                        Vector4 v = vertices[j];
                        v.w = 1.0f;

                        vertices[j] = localToWorld * v;
                    }
                }
            });
            th.Start();
            
            th.Join();*/
            
            bufferMeshVertices = new ComputeBuffer(vertices.Count, sizeof(float) * 3);
            bufferMeshVertices.SetData(vertices);
            
            BVHBuffer = new ComputeBuffer(bvh.bvh.flatTree.Length, sizeof(float) * 6 + sizeof(int) * 2);
            BVHBuffer.SetData(bvh.bvh.flatTree);
        }

        private void Update()
        {
            bool hasChanged = false;

            if (Input.GetKeyDown(KeyCode.O))
            {
                raySortingMode = !raySortingMode;

                text.text = raySortingMode ? "Sorting rays: activated" : "Sorting rays: deactivated";
            }

            foreach (Transform transform1 in _transformsToWatch)
            {
                if (transform1.hasChanged)
                    hasChanged = true;

                transform1.hasChanged = false;
            }

            if (hasChanged || transform.hasChanged)
            {
                _sampleRate = 0;
                transform.hasChanged = false;
            }
        }

        private void OnDisable()
        {
            RenderPipelineManager.endCameraRendering -= RenderPipelineManagerOnendCameraRendering;
            buffer.Release();
            bufferMeshes.Release();
            bufferMeshEbo.Release();
            bufferMeshVertices.Release();
            BVHBuffer.Release();
            bufferRays.Release();
            bufferRaysCount.Release();
        }
    }
}