using BillGameCore;
using TossZone.Throwing;
using UnityEngine;
using UnityEngine.XR;

namespace TossZone.Combat
{
    public static class ExplosionFx
    {
        private const float NukeRadiusThreshold = 3.5f;
        private const float FireballSeconds = 0.35f;

        public static void Play(Vector3 point, float radius)
        {
            SpawnFireball(point, radius);
            ImpactBurst.Show(point, Mathf.Clamp01(radius / 4.5f));
            PulseHands(point, radius);
            if (radius >= NukeRadiusThreshold) SpawnFlash(point, radius);
        }

        private static void SpawnFireball(Vector3 point, float radius)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "ExplosionFireball";
            Object.Destroy(ball.GetComponent<Collider>());
            ball.transform.position = point;
            ball.transform.localScale = Vector3.one * 0.1f;

            Material mat = CreateFireballMaterial();
            ball.GetComponent<Renderer>().material = mat;

            BillTween.Scale(ball.transform, radius * 2f, FireballSeconds)
                ?.SetEase(EaseType.OutCubic)
                .SetTarget(ball.transform);
            BillTween.Float(0.85f, 0f, FireballSeconds, a =>
                {
                    if (mat != null) mat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.1f, a));
                })
                ?.SetEase(EaseType.OutQuad)
                .SetTarget(ball.transform)
                .OnComplete(() =>
                {
                    BillTween.KillTarget(ball.transform);
                    Object.Destroy(ball);
                });
        }

        private static Material CreateFireballMaterial()
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            var mat = new Material(sh != null ? sh : Shader.Find("Sprites/Default"));
            mat.SetFloat("_Surface", 1f);
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = 3000;
            mat.SetColor("_BaseColor", new Color(1f, 0.45f, 0.1f, 0.85f));
            return mat;
        }

        private static void SpawnFlash(Vector3 point, float radius)
        {
            var go = new GameObject("ExplosionFlash");
            go.transform.position = point + Vector3.up * 1.5f;
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.range = radius * 6f;
            light.intensity = 10f;
            BillTween.Float(10f, 0f, 0.5f, v => { if (light != null) light.intensity = v; })
                ?.SetEase(EaseType.OutQuad)
                .SetTarget(go.transform)
                .OnComplete(() =>
                {
                    BillTween.KillTarget(go.transform);
                    Object.Destroy(go);
                });
        }

        private static void PulseHands(Vector3 point, float radius)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float dist = Vector3.Distance(cam.transform.position, point);
            float strength = Mathf.Clamp01(1f - dist / Mathf.Max(radius * 6f, 4f));
            if (strength <= 0.05f) return;
            float duration = radius >= NukeRadiusThreshold ? 0.5f : 0.15f;
            Pulse(XRNode.LeftHand, strength, duration);
            Pulse(XRNode.RightHand, strength, duration);
        }

        private static void Pulse(XRNode node, float amplitude, float duration)
        {
            InputDevice dev = InputDevices.GetDeviceAtXRNode(node);
            if (dev.isValid && dev.TryGetHapticCapabilities(out HapticCapabilities caps) && caps.supportsImpulse)
                dev.SendHapticImpulse(0, amplitude, duration);
        }
    }
}
