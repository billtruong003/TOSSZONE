using System.Collections.Generic;
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
        private const int FireballPoolSize = 8;
        private const int FlashPoolSize = 3;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private static Transform _root;
        private static Material _sharedMat;
        private static MaterialPropertyBlock _mpb;
        private static readonly List<Transform> _fireballs = new List<Transform>();
        private static readonly List<Light> _flashes = new List<Light>();
        private static int _fireballCursor;
        private static int _flashCursor;

        public static void Play(Vector3 point, float radius)
        {
            SpawnFireball(point, radius);
            ImpactBurst.Show(point, Mathf.Clamp01(radius / 4.5f));
            PulseHands(point, radius);
            if (radius >= NukeRadiusThreshold) SpawnFlash(point, radius);
        }

        private static Transform Root()
        {
            if (_root != null) return _root;
            var go = new GameObject("ExplosionFxPool");
            Object.DontDestroyOnLoad(go);
            _root = go.transform;
            return _root;
        }

        private static Material SharedMaterial()
        {
            if (_sharedMat != null) return _sharedMat;
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit");
            _sharedMat = new Material(sh != null ? sh : Shader.Find("Sprites/Default")) { name = "ExplosionFireball(shared)" };
            _sharedMat.SetFloat("_Surface", 1f);
            _sharedMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _sharedMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            _sharedMat.SetInt("_ZWrite", 0);
            _sharedMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _sharedMat.renderQueue = 3000;
            return _sharedMat;
        }

        private static void SpawnFireball(Vector3 point, float radius)
        {
            Transform tr = NextFireball();
            MeshRenderer mr = tr.GetComponent<MeshRenderer>();
            tr.position = point;
            tr.localScale = Vector3.one * 0.1f;
            tr.gameObject.SetActive(true);
            Tint(mr, 0.85f);

            BillTween.KillTarget(tr);
            BillTween.Scale(tr, radius * 2f, FireballSeconds)?.SetEase(EaseType.OutCubic).SetTarget(tr);
            BillTween.Float(0.85f, 0f, FireballSeconds, a => Tint(mr, a))
                ?.SetEase(EaseType.OutQuad)
                .SetTarget(tr)
                .OnComplete(() => { if (tr != null) tr.gameObject.SetActive(false); });
        }

        private static void Tint(MeshRenderer mr, float alpha)
        {
            if (mr == null) return;
            _mpb ??= new MaterialPropertyBlock();
            _mpb.SetColor(BaseColorId, new Color(1f, 0.45f, 0.1f, alpha));
            mr.SetPropertyBlock(_mpb);
        }

        private static Transform NextFireball()
        {
            if (_fireballs.Count < FireballPoolSize)
            {
                Transform fresh = CreateFireball();
                _fireballs.Add(fresh);
                return fresh;
            }
            _fireballCursor = (_fireballCursor + 1) % _fireballs.Count;
            Transform reused = _fireballs[_fireballCursor];
            if (reused == null) { reused = CreateFireball(); _fireballs[_fireballCursor] = reused; }
            return reused;
        }

        private static Transform CreateFireball()
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "ExplosionFireball";
            Object.Destroy(ball.GetComponent<Collider>());
            ball.GetComponent<MeshRenderer>().sharedMaterial = SharedMaterial();
            ball.transform.SetParent(Root(), false);
            ball.SetActive(false);
            return ball.transform;
        }

        private static void SpawnFlash(Vector3 point, float radius)
        {
            Light light = NextFlash();
            light.transform.position = point + Vector3.up * 1.5f;
            light.range = radius * 6f;
            light.intensity = 10f;
            light.enabled = true;

            BillTween.KillTarget(light.transform);
            BillTween.Float(10f, 0f, 0.5f, v => { if (light != null) light.intensity = v; })
                ?.SetEase(EaseType.OutQuad)
                .SetTarget(light.transform)
                .OnComplete(() => { if (light != null) light.enabled = false; });
        }

        private static Light NextFlash()
        {
            if (_flashes.Count < FlashPoolSize)
            {
                Light fresh = CreateFlash();
                _flashes.Add(fresh);
                return fresh;
            }
            _flashCursor = (_flashCursor + 1) % _flashes.Count;
            Light reused = _flashes[_flashCursor];
            if (reused == null) { reused = CreateFlash(); _flashes[_flashCursor] = reused; }
            return reused;
        }

        private static Light CreateFlash()
        {
            var go = new GameObject("ExplosionFlash");
            go.transform.SetParent(Root(), false);
            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.85f, 0.6f);
            light.enabled = false;
            return light;
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
