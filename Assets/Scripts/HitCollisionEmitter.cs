using System;
using UniRx;
using UnityEngine;
using Zenject;

[RequireComponent(typeof(Collider))]
public class HitCollisionEmitter : MonoBehaviour
{
    [Header("Filter")]
    [Tooltip("Only collisions with these layers will emit.")] [SerializeField] 
    private  LayerMask targetMask = ~0;
    [Tooltip("If true, collisions only emit while gate is active.")] [SerializeField] 
    private  bool requireGate = true;

    [Header("Particles")]
    [SerializeField] 
    private HitParticlePool poolOverride;
    [InjectOptional] 
    private HitParticlePool _injectedPool;
    private HitParticlePool _pool;

    [Header("Spam Control")]
    [Tooltip("Min time between emits from this emitter (seconds).")] [SerializeField] 
    private  float emitCooldown = 0.05f;
    private float _nextEmitTime = 0f;

    private readonly SerialDisposable _gateDisposable = new SerialDisposable();
    private bool _gateActive;

    private void Awake()
    {
        _pool = poolOverride ? poolOverride : _injectedPool;
    }

    public void StartGate(float duration)
    {
        _gateActive = true;
        _gateDisposable.Disposable = Observable.Timer(TimeSpan.FromSeconds(Mathf.Max(0f, duration)))
            .Subscribe(_ => _gateActive = false);
    }

    private void OnDisable()
    {
        _gateDisposable.Disposable = null;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_pool == null) return;
        if (requireGate && !_gateActive) return;
        if (((1 << collision.gameObject.layer) & targetMask) == 0) return;
        if (Time.time < _nextEmitTime) return;

        Vector3 origin = transform.position;
        Vector3 pos;
        Vector3 normal;

        var contacts = collision.contacts;
        if (contacts is { Length: > 0 })
        {
            var cp = contacts[0];
            Vector3 dir = cp.point - origin;
            var dist = dir.magnitude + 0.05f;
            if (dist > 1e-4f)
            {
                dir /= dist;
                if (collision.collider.Raycast(new Ray(origin, dir), out var hit, dist))
                {
                    pos = hit.point;
                    normal = hit.normal;
                }
                else
                {
                    pos = cp.point;
                    normal = cp.normal;
                }
            }
            else
            {
                pos = cp.point;
                normal = cp.normal;
            }
        }
        else
        {
            var col = collision.collider;
            Vector3 closest = col.ClosestPoint(origin);
            Vector3 dir = (closest - origin);
            var dist = Mathf.Max(0.01f, dir.magnitude + 0.05f);
            dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : transform.forward;
            if (col.Raycast(new Ray(origin, dir), out var hit, dist))
            {
                pos = hit.point;
                normal = hit.normal;
            }
            else
            {
                pos = closest;
                normal = (origin - closest).sqrMagnitude > 1e-6f ? (closest - origin).normalized : transform.forward;
            }
        }

        _pool.PlayAt(pos, normal);
        _nextEmitTime = Time.time + emitCooldown;
    }
}