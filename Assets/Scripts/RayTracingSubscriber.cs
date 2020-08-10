using System;
using UnityEngine;

namespace UnityTemplateProjects
{
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class RayTracingSubscriber : MonoBehaviour
    {
        private void Start()
        {
            RayTracingManager.Register(this);

            //GetComponent<MeshRenderer>().enabled = false;
        }

        private void OnDisable()
        {
            RayTracingManager.UnRegister(this);
        }
    }
}