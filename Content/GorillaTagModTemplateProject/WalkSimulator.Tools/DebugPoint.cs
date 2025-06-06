<<<<<<< HEAD
﻿using System.Collections.Generic;
using UnityEngine;

namespace WalkSimulator.Tools
{
    public class DebugPoint : MonoBehaviour
    {
        private static readonly Dictionary<string, DebugPoint> points =
            new Dictionary<string, DebugPoint>();

        public float size = 0.1f;
        public Color color = Color.white;

        private Material material;

        private void Awake()
        {
            if (Plugin.Instance?.bundle == null)
            {
                Debug.LogError("Plugin instance or bundle is null.");
                Destroy(this); // Destroy the component if dependencies are missing
                return;
            }

            material = Instantiate(Plugin.Instance.bundle.LoadAsset<Material>("m_xRay"));
            if (material == null)
            {
                Debug.LogError("Failed to load material 'm_xRay' from asset bundle.");
                Destroy(this);
                return;
            }

            material.color = color;
            GetComponent<MeshRenderer>().material = material;
        }

        private void FixedUpdate()
        {
            if (material != null && GorillaLocomotion.GTPlayer.Instance != null)
            {
                material.color = color;
                transform.localScale = Vector3.one * size *
                                       GorillaLocomotion.GTPlayer.Instance.scale;
            }
        }

        private void OnDestroy()
        {
            points.Remove(name);
            if (material != null)
            {
                Destroy(material); // Clean up the instantiated material
            }
        }

        public static Transform Get(
            string name, Vector3 position, Color color = default, float size = 0.1f)
        {
            if (points.TryGetValue(name, out DebugPoint debugPoint))
            {
                debugPoint.color = color;
                debugPoint.transform.position = position;
                debugPoint.size = size;
                return debugPoint.transform;
            }
            else
            {
                return Create(name, position, color);
            }
        }

        private static Transform Create(string name, Vector3 position, Color color)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Transform transform = sphere.transform;
            transform.name = "Cipher Debugger (" + name + ")";
            transform.localScale = Vector3.one * 0.2f;
            transform.position = position;

            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Material material =
                Instantiate(GorillaTagger.Instance.offlineVRRig.mainSkin.material);
            transform.GetComponent<Renderer>().material = material;

            DebugPoint debugPoint = sphere.AddComponent<DebugPoint>();
            debugPoint.color = color;
            points.Add(name, debugPoint);
            return transform;
        }
    }
}
=======
﻿using System.Collections.Generic;
using UnityEngine;

namespace WalkSimulator.Tools
{
    public class DebugPoint : MonoBehaviour
    {
        private static readonly Dictionary<string, DebugPoint> points =
            new Dictionary<string, DebugPoint>();

        public float size = 0.1f;
        public Color color = Color.white;

        private Material material;

        private void Awake()
        {
            if (Plugin.Instance?.bundle == null)
            {
                Debug.LogError("Plugin instance or bundle is null.");
                Destroy(this); // Destroy the component if dependencies are missing
                return;
            }

            material = Instantiate(Plugin.Instance.bundle.LoadAsset<Material>("m_xRay"));
            if (material == null)
            {
                Debug.LogError("Failed to load material 'm_xRay' from asset bundle.");
                Destroy(this);
                return;
            }

            material.color = color;
            GetComponent<MeshRenderer>().material = material;
        }

        private void FixedUpdate()
        {
            if (material != null && GorillaLocomotion.GTPlayer.Instance != null)
            {
                material.color = color;
                transform.localScale = Vector3.one * size *
                                       GorillaLocomotion.GTPlayer.Instance.scale;
            }
        }

        private void OnDestroy()
        {
            points.Remove(name);
            if (material != null)
            {
                Destroy(material); // Clean up the instantiated material
            }
        }

        public static Transform Get(
            string name, Vector3 position, Color color = default, float size = 0.1f)
        {
            if (points.TryGetValue(name, out DebugPoint debugPoint))
            {
                debugPoint.color = color;
                debugPoint.transform.position = position;
                debugPoint.size = size;
                return debugPoint.transform;
            }
            else
            {
                return Create(name, position, color);
            }
        }

        private static Transform Create(string name, Vector3 position, Color color)
        {
            GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Transform transform = sphere.transform;
            transform.name = "Cipher Debugger (" + name + ")";
            transform.localScale = Vector3.one * 0.2f;
            transform.position = position;

            Collider collider = sphere.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
            }

            Material material =
                Instantiate(GorillaTagger.Instance.offlineVRRig.mainSkin.material);
            transform.GetComponent<Renderer>().material = material;

            DebugPoint debugPoint = sphere.AddComponent<DebugPoint>();
            debugPoint.color = color;
            points.Add(name, debugPoint);
            return transform;
        }
    }
}
>>>>>>> 31ec9aed7aef5857e654107e2b42ea026d51c0de
