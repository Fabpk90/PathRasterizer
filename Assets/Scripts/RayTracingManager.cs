using System;
using System.Collections.Generic;
using System.Linq;
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
        public ComputeShader shaderVertexWorlder;
        public ComputeShader shaderRT;
        public RenderTexture tex;
        public RenderTexture cumulationTex;
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
            
            cumulationTex = new RenderTexture(Screen.width, Screen.height, 0) {enableRandomWrite = true};
            cumulationTex.format = tex.format;
            cumulationTex.Create();
            
            
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
            //kernels.Add(shaderRT.FindKernel("AOPass"));
            

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
           
            

            shaderRT.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
            shaderRT.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);

            var position = directionalLight.transform.forward;
            shaderRT.SetVector("directionalLight", new Vector4(position.x, position.y, position.z, directionalLight.intensity));
            
            shaderRT.SetVector("cameraPlanes", new Vector4(_cam.nearClipPlane, _cam.farClipPlane));
            
            
            
//            shaderRT.Dispatch(kernelIndex, tex.width / 8, tex.height / 8, 1);

            RenderPipelineManager.endCameraRendering += RenderPipelineManagerOnendCameraRendering;

            float depth = Mathf.Abs(0.0923f - 1) * 2.0f - 1.0f;
            
            Vector4 uv = new Vector4(0, 0, depth, 1.0f);
            Vector4 viewPos = _cam.projectionMatrix.inverse * uv;
            viewPos /= viewPos.w;

            Vector3 worldPos = _cam.cameraToWorldMatrix.inverse * viewPos;

        }

        private void RenderPipelineManagerOnendCameraRendering(ScriptableRenderContext arg1, Camera arg2)
        {
            if (arg2.name != "Main Camera") return;
            
            // if (_sampleRate < 500) //stacking frames for TAA
            {
                buffer.SetData(spheres);
                shaderRT.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
                shaderRT.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);
                shaderRT.SetFloats("pixelOffset", Random.value, Random.value);
                shaderRT.SetFloat("seed", Random.value);

                shaderRT.Dispatch(0, tex.width / 8, tex.height / 8, 1);
                shaderRT.Dispatch(1, tex.width / 8, tex.height / 8, 1);
                
            
                AA.SetFloat(Sample, _sampleRate);
                _sampleRate++;
            }
            //Graphics.Blit(tex, cumulationTex, AA);

            Mixer.SetTexture(PathTracedShadowsReflections, tex);
            
            CommandBuffer cmd = new CommandBuffer();
            cmd.Blit(tex, arg2.activeTexture, Mixer);
            arg1.ExecuteCommandBuffer(cmd);
            cmd.Release();

            //Graphics.Blit(screenTex, arg2.activeTexture, Mixer);
            //Graphics.Blit(cumulationTex, arg2.camera.activeTexture);
        }


        private void CreateMeshBuffers()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> ebos = new List<int>();
            List<ShaderMesh> meshObjects = new List<ShaderMesh>(Subscribers.Count);
            List<MeshRenderer> renderers = new List<MeshRenderer>(Subscribers.Count);

            for (int i = 0; i < Subscribers.Count; i++)
            {
                var meshObject = Subscribers[i].gameObject;
                var renderer = meshObject.GetComponent<MeshRenderer>();
                var mesh = meshObject.GetComponent<MeshFilter>().sharedMesh;
                
                print(renderer.transform);
                renderers.Add(renderer);

                //TODO: sort the meshes for the bvh, if we want to include more than a primitive (mesh) in a node
                ShaderMesh m;
                m.eboOffset = ebos.Count;
                m.localToWorld = meshObject.transform.localToWorldMatrix;
                var c = renderer.material.color;
                m.color = c;

                var indices = mesh.GetIndices(0);
                ebos.AddRange(indices.Select(index => index + vertices.Count));
                m.eboCount = ebos.Count;
                
                vertices.AddRange(mesh.vertices);

                meshObjects.Add(m);
            }

            bufferMeshVertices = new ComputeBuffer(vertices.Count, sizeof(float) * 3);
            bufferMeshVertices.SetData(vertices);
            
            bufferMeshEbo = new ComputeBuffer(ebos.Count, sizeof(int));
            bufferMeshEbo.SetData(ebos);

            //shaderVertexWorlder.SetBuffer(0, "meshVertices", bufferMeshVertices);

            //ComputeBuffer bufferVertexWorld = new ComputeBuffer(vertices.Count, sizeof(float) * 3);
            
            //shaderVertexWorlder.SetBuffer(0, "output", bufferVertexWorld);

            //shaderVertexWorlder.Dispatch(0, bufferMeshEbo.count / 1024, 1, 1);
            //bufferMeshVertices.Release();
            //bufferMeshVertices = bufferVertexWorld;
            

            bufferMeshes = new ComputeBuffer(meshObjects.Count, sizeof(float) * 16 + 2 * sizeof(int) + 4 * sizeof(float));
            bufferMeshes.SetData(meshObjects);

            bvh.BuildBVH(renderers.ToArray());
            
            BVHBuffer = new ComputeBuffer(bvh.bvh.flatTree.Length, sizeof(float) * 6 + sizeof(int) * 2);
            BVHBuffer.SetData(bvh.bvh.flatTree);
        }

        private void Update()
        {
            bool hasChanged = false;
            if (Input.GetKey(KeyCode.LeftArrow))
            {
                var tmp = spheres[0];
                tmp.Move(Vector3.left * Time.deltaTime);
                spheres[0] = tmp;

                hasChanged = true;
            }
            else if (Input.GetKey(KeyCode.RightArrow))
            {
                var tmp = spheres[0];
                tmp.Move(Vector3.right * Time.deltaTime);
                spheres[0] = tmp;
                hasChanged = true;
            }
            
            
            if (Input.GetKey(KeyCode.UpArrow))
            {
                var tmp = spheres[0];
                tmp.Move(Vector3.up * Time.deltaTime);
                spheres[0] = tmp;
                hasChanged = true;
            }
            else if (Input.GetKey(KeyCode.DownArrow))
            {
                var tmp = spheres[0];
                tmp.Move(Vector3.down * Time.deltaTime);
                spheres[0] = tmp;
                hasChanged = true;
            }

            if (!hasChanged)
            {
                foreach (Transform transform1 in _transformsToWatch)
                {
                    if (transform1.hasChanged)
                        hasChanged = true;

                    transform1.hasChanged = false;
                }
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
        }
    }
}