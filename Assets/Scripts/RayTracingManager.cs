using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;
using Random = UnityEngine.Random;

namespace UnityTemplateProjects
{
    [Serializable]
    public struct Sphere
    {
        public Vector3 position;
        public float radius;
        public Color color;
        public float specular;

        public Sphere(Vector3 position, float radius, Color color, float specular)
        {
            this.position = position;
            this.radius = radius;
            this.color = color;
            this.specular = specular;
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
    }
    
    public class RayTracingManager : MonoBehaviour
    {
        public ComputeShader shaderRT;
        public RenderTexture tex;
        public RenderTexture cumulationTex;
        private ComputeBuffer buffer;
        public Texture skybox;
        
        private ComputeBuffer bufferMeshVertices;
        private ComputeBuffer bufferMeshEbo;
        private ComputeBuffer bufferMeshes;
        
        public List<Sphere> spheres;
        public int sphereSeed;
        private int kernelIndex;

        private static List<RayTracingSubscriber> Subscribers;
        private static List<Transform> _transformsToWatch;

        private uint _sampleRate;
        private static readonly int Sample = Shader.PropertyToID("_Sample");
        private Material AA;

        private Camera _cam;
        public Light directionalLight;

        private void Awake()
        {
            Subscribers = new List<RayTracingSubscriber>();
            _transformsToWatch = new List<Transform>();
            _cam = GetComponent<Camera>();
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
                Sphere s;
                s.color = Random.ColorHSV();
                s.position = (Random.insideUnitSphere * 5) + Vector3.forward * 2;
                s.radius = Random.value * 2.0f;
                s.specular = Random.value;
                
                sphereList.Add(s);
            }

            return sphereList;
        }

        private void Start()
        {
            tex = new RenderTexture(Screen.width, Screen.height, 0) {enableRandomWrite = true};
            //tex.format = RenderTextureFormat.RGB111110Float;
            tex.Create();
            
            cumulationTex = new RenderTexture(Screen.width, Screen.height, 0) {enableRandomWrite = true};
            cumulationTex.format = tex.format;
            cumulationTex.Create();
            
            AA = new Material(Shader.Find("Hidden/AART"));

            Random.InitState(sphereSeed);
            spheres = GenerateSpheres(4);

            buffer = new ComputeBuffer(spheres.Count, sizeof(float) * 9);
            buffer.SetData(spheres);

            CreateMeshBuffers();

            kernelIndex = shaderRT.FindKernel("CSMain");
            shaderRT.SetTexture(kernelIndex, "texOut", tex);
            shaderRT.SetBuffer(kernelIndex, "spheres", buffer);
            
            shaderRT.SetBuffer(kernelIndex, "meshes", bufferMeshes);
            shaderRT.SetBuffer(kernelIndex, "meshVertices", bufferMeshVertices);
            shaderRT.SetBuffer(kernelIndex, "meshEbo", bufferMeshEbo);

            shaderRT.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
            shaderRT.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);

            var position = directionalLight.transform.forward;
            shaderRT.SetVector("directionalLight", new Vector4(position.x, position.y, position.z, directionalLight.intensity));
            
            shaderRT.SetTexture(kernelIndex, "skybox", skybox);
            
            shaderRT.Dispatch(kernelIndex, tex.width / 8, tex.height / 8, 1);

            RenderPipelineManager.endFrameRendering += OnEndFrame;
        }

        private void CreateMeshBuffers()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> ebos = new List<int>();
            List<ShaderMesh> meshObjects = new List<ShaderMesh>(Subscribers.Count);

            for (int i = 0; i < Subscribers.Count; i++)
            {
                var meshObject = Subscribers[i].gameObject;
                var mesh = meshObject.GetComponent<MeshFilter>().sharedMesh;

                ShaderMesh m;
                m.eboOffset = vertices.Count;
                m.localToWorld = meshObject.transform.localToWorldMatrix;

                vertices.AddRange(mesh.vertices);

                var indices = mesh.GetIndices(0);
                ebos.AddRange(indices.Select(index => index + m.eboOffset));
                m.eboCount = indices.Length;
                
                meshObjects.Add(m);
            }

            bufferMeshVertices = new ComputeBuffer(vertices.Count, sizeof(float) * 3);
            bufferMeshVertices.SetData(vertices);
            
            bufferMeshEbo = new ComputeBuffer(ebos.Count, sizeof(int));
            bufferMeshEbo.SetData(ebos);

            bufferMeshes = new ComputeBuffer(meshObjects.Count, sizeof(float) * 16 + 2 * sizeof(int));
            bufferMeshes.SetData(meshObjects);
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

        private void OnEndFrame(ScriptableRenderContext arg1, Camera[] cameras)
        {
            if (_sampleRate < 300) //stacking 300 frames for TAA
            {
                buffer.SetData(spheres);
                shaderRT.SetMatrix("cameraToWorld", _cam.cameraToWorldMatrix);
                shaderRT.SetMatrix("cameraInvProj", _cam.projectionMatrix.inverse);
                shaderRT.SetFloats("pixelOffset", Random.value, Random.value);
                shaderRT.SetFloat("seed", Random.value);
                shaderRT.Dispatch(kernelIndex, tex.width / 8, tex.height / 8, 1);
            
                AA.SetFloat(Sample, _sampleRate);
                _sampleRate++;
            }
            

            Graphics.Blit(tex, cumulationTex, AA);
            Graphics.Blit(cumulationTex, cameras[0].activeTexture);
        }

        private void OnDisable()
        {
            RenderPipelineManager.endFrameRendering -= OnEndFrame;
            buffer.Release();
            buffer.Dispose();
        }
    }
}