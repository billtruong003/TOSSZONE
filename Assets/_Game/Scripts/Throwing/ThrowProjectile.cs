using BillGameCore;
using UnityEngine;

namespace TossZone.Throwing
{
    /// <summary>
    /// The flying ball — a pooled, <see cref="BillTween"/>-driven projectile that replays a TRUE BALLISTIC
    /// trajectory from the player's release velocity under a tunable gravity (NOT a designer arc, NOT a
    /// rigidbody). It goes exactly where + how hard you threw — accurate aim. Stretches along its velocity,
    /// drags a trail, then pops + fires <see cref="BallLandedEvent"/> on landing. Spawn via
    /// <c>Bill.Pool.Spawn("throwprojectile")</c> then call <see cref="Launch"/>.
    /// </summary>
    public class ThrowProjectile : PooledObject
    {
        private const float StretchPerSpeed = 0.018f;
        private const float MaxStretch = 1.7f;
        private const float MaxFlightTime = 5f;

        [SerializeField] private TrailRenderer _trail;

        private GameObject _weaponVisual;
        private TossZone.Combat.WeaponConfig _weaponVisualCfg;

        private ThrowConfig _config;
        private Vector3 _origin;
        private Vector3 _v0;          // launch velocity (world)
        private Vector3 _gravity;     // world gravity vector (down * g)
        private float _power;
        private Vector3 _baseScale = Vector3.one;
        private float _elapsed;
        private bool _live;
        private bool _matSet;
        private MeshRenderer _mr;
        private System.Action<float> _onFlightCb;   // cached → no per-throw delegate alloc
        private System.Action _onLandedCb;
        private System.Action _returnCb;
        private bool _trailBaseSaved;
        private Color _trailBaseStart;
        private Color _trailBaseEnd;
        private static Material _ballMat;

        /// <summary>True while the ball is in flight and catchable (false for uncatchable weapons).</summary>
        public bool IsCatchable { get; private set; } = true;
        /// <summary>True when this throw was launched as a Power throw (purple; extra damage, uncatchable).</summary>
        public bool IsPower { get; private set; }

        /// <summary>Mark the ball as power-throw (called by ThrowController before Launch).</summary>
        public void SetPower(bool power) { IsPower = power; IsCatchable = !power; }

        /// <summary>Mark uncatchable regardless of power state (called when weapon.isUncatchable).</summary>
        public void SetUncatchable() { IsCatchable = false; }

        /// <summary>T20 — dress the flying ball as the EQUIPPED weapon's shot (Rock chunk, grenade, mine…).
        /// Null (default equip / no model wired) keeps the plain sphere. The cosmetic is cached per config —
        /// consecutive throws of the same weapon reuse one instance across pool lives, no per-throw
        /// Instantiate.</summary>
        public void ApplyWeaponVisual(TossZone.Combat.WeaponConfig cfg)
        {
            if (cfg != _weaponVisualCfg)
            {
                if (_weaponVisual != null) { Destroy(_weaponVisual); _weaponVisual = null; }
                _weaponVisualCfg = cfg;
                _weaponVisual = TossZone.Combat.WeaponVisuals.SpawnProjectileVisual(cfg, transform);
            }
            bool custom = _weaponVisual != null;
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mr != null) _mr.enabled = !custom;
            if (_weaponVisual != null) _weaponVisual.SetActive(true);
        }

        /// <summary>Called by CatchController when the ball enters the catch zone. Stops flight and pools.</summary>
        public void OnCaught()
        {
            if (!_live) return;
            _live = false;
            BillTween.KillTarget(this);
            gameObject.ReturnToPool();
        }

        /// <summary>Launch with a real world-space velocity; the ball flies <c>p(t)=origin + v0·t + ½·g·t²</c>.</summary>
        public void Launch(Vector3 origin, Vector3 velocity, float power, ThrowConfig config)
        {
            _config = config;
            _origin = origin;
            _v0 = velocity;
            _power = power;
            _gravity = Vector3.down * Mathf.Max(config.gravity, 0.01f);

            // Landing plane: straight down from the start (flat floor → exact; uneven → approximate).
            float groundY = origin.y - 3f;
            if (Physics.Raycast(origin + Vector3.up * 0.05f, Vector3.down, out RaycastHit hit, 60f,
                                config.groundMask, QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;

            // Time to fall back to groundY:  origin.y + v0y·t − ½g·t² = groundY  → positive root.
            float g = _gravity.magnitude;
            float v0y = _v0.y;
            float disc = v0y * v0y + 2f * g * Mathf.Max(origin.y - groundY, 0f);
            float tLand = (v0y + Mathf.Sqrt(Mathf.Max(disc, 0f))) / g;
            tLand = Mathf.Clamp(tLand, 0.1f, MaxFlightTime);

            EnsureMaterial(config.ballColor);
            transform.position = origin;
            _elapsed = 0f;
            _live = true;

            _onFlightCb ??= OnFlight;
            _onLandedCb ??= OnLanded;
            BillTween.Float(0f, tLand, tLand, _onFlightCb)   // value == elapsed seconds
                ?.SetEase(EaseType.Linear)
                .SetTarget(this)
                .OnComplete(_onLandedCb);
        }

        public override void OnSpawnedFromPool()
        {
            _baseScale = transform.localScale;
            IsCatchable = true;
            IsPower = false;
            if (_trail != null) _trail.Clear();
        }

        public override void OnReturnedToPool()
        {
            BillTween.KillTarget(this);
            _live = false;
            transform.localScale = _baseScale;
            if (_trail != null)
            {
                _trail.Clear();
                if (_trailBaseSaved)
                {
                    _trail.startColor = _trailBaseStart;
                    _trail.endColor = _trailBaseEnd;
                }
            }
        }

        public void ApplySpeedMultiplier(float mul)
        {
            if (!_live || mul <= 1f) return;
            Vector3 vel = (_v0 + _gravity * _elapsed) * mul;
            BillTween.KillTarget(this);
            Launch(transform.position, vel, _power, _config);
        }

        public void SetTrailTint(Color color)
        {
            if (_trail == null) return;
            if (!_trailBaseSaved)
            {
                _trailBaseSaved = true;
                _trailBaseStart = _trail.startColor;
                _trailBaseEnd = _trail.endColor;
            }
            _trail.startColor = color;
            _trail.endColor = new Color(color.r, color.g, color.b, 0f);
        }

        private void OnFlight(float t)
        {
            _elapsed = t;
            transform.position = _origin + _v0 * t + 0.5f * _gravity * (t * t);

            Vector3 vel = _v0 + _gravity * t;   // instantaneous velocity → facing + stretch
            if (vel.sqrMagnitude > 1e-6f)
            {
                transform.rotation = Quaternion.LookRotation(vel);
                float s = Mathf.Min(1f + vel.magnitude * StretchPerSpeed, MaxStretch);
                float inv = 1f / Mathf.Sqrt(s);
                transform.localScale = new Vector3(_baseScale.x * inv, _baseScale.y * inv, _baseScale.z * s);
            }
        }

        private void OnLanded()
        {
            if (!_live) return;
            _live = false;
            transform.localScale = _baseScale;

            if (Bill.IsReady)
            {
                if (_config != null && !string.IsNullOrEmpty(_config.impactSfx)) Bill.Audio.Play(_config.impactSfx);
                Bill.Events.Fire(new BallLandedEvent { Position = transform.position, Power = _power, Ball = this });
            }

            _returnCb ??= ReturnSelf;
            BillTween.Scale(transform, _baseScale.x * 1.5f, 0.09f)
                ?.SetEase(EaseType.OutQuad).SetTarget(this)
                .OnComplete(_returnCb);
        }

        private void ReturnSelf() => gameObject.ReturnToPool();

        private void EnsureMaterial(Color color)
        {
            if (_matSet) return;
            if (_mr == null) _mr = GetComponent<MeshRenderer>();
            if (_mr != null) _mr.sharedMaterial = BallMaterial(color);
            _matSet = true;
        }

        /// <summary>Shared URP material for the throw ball — a runtime/primitive sphere otherwise gets the
        /// built-in Standard material, which renders pink under URP.</summary>
        public static Material BallMaterial(Color color)
        {
            if (_ballMat == null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                _ballMat = new Material(sh) { name = "ThrowBall(runtime)" };
            }
            if (_ballMat.HasProperty("_BaseColor")) _ballMat.SetColor("_BaseColor", color);
            else if (_ballMat.HasProperty("_Color")) _ballMat.SetColor("_Color", color);
            return _ballMat;
        }
    }
}
