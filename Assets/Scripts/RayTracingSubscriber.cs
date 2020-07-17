using System;
using UnityEngine;

namespace UnityTemplateProjects
{
    public class RayTracingSubscriber : MonoBehaviour
    {
        private void Start()
        {
            RayTracingManager.Register(this);
        }

        private void OnDisable()
        {
            RayTracingManager.UnRegister(this);
        }
    }
}