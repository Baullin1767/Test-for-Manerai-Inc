using System;
using UniRx;
using UnityEngine;

public class HitParticlePool : MonoBehaviour
{
    [Header("Pool Setup")]
    [Tooltip("Particle prefab (root has ParticleSystem or children do).")] [SerializeField] 
    private GameObject particlePrefab;
    [Tooltip("How many pooled instances to pre-create.")] [SerializeField] 
    private int poolSize = 8;
    [Tooltip("Optional parent for pooled instances. Defaults to this transform.")] [SerializeField] 
    private Transform poolParent;

    [Header("Placement")]
    [Tooltip("Offset along surface normal to avoid spawning inside geometry.")] [SerializeField] 
    private float surfaceOffset = 0.02f;
    [Tooltip("Rotate particle so its forward faces opposite to the surface normal.")] [SerializeField] 
    private bool alignToNormal = true;

    [Header("Lifetime")]
    [Tooltip("Extra seconds added to computed lifetime before auto-deactivation.")] [SerializeField] 
    private float extraLifetime = 0.2f;
    [Tooltip("Fallback lifetime if duration cannot be computed from ParticleSystems.")] [SerializeField] 
    private float fallbackLifetime = 2.0f;

    private Entry[] _entries;
    private int _next;

    private void Awake()
    {
        EnsurePool();
    }

    private void OnDisable()
    {
        if (_entries == null) return;
        foreach (var entry in _entries)
        {
            entry.Life.Disposable = null;
        }
    }

    public void PlayAt(Vector3 position, Vector3 normal)
    {
        if (!EnsurePool()) return;

        var e = _entries[_next];
        _next = (_next + 1) % _entries.Length;

        var n = normal.sqrMagnitude > 0f ? normal.normalized : Vector3.up;
        Vector3 spawnPos = position + n * surfaceOffset;
        e.Go.transform.position = spawnPos;
        if (alignToNormal)
        {
            e.Go.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
        }

        e.Go.SetActive(true);
        foreach (var ps in e.Systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            ps.Simulate(0f, true, true);
            ps.Play(true);
        }

        var life = ComputeLifetime(e.Systems) + Mathf.Max(0f, extraLifetime);
        e.Life.Disposable = Observable.Timer(TimeSpan.FromSeconds(life))
            .Subscribe(_ => Deactivate(e));
    }

    private void Deactivate(Entry e)
    {
        if (e == null || e.Go == null) return;
        foreach (var ps in e.Systems)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        e.Go.SetActive(false);
    }

    private bool EnsurePool()
    {
        if (particlePrefab == null || poolSize <= 0) return false;
        if (_entries != null && _entries.Length == poolSize) return true;

        poolParent = poolParent ? poolParent : transform;
        _entries = new Entry[poolSize];
        _next = 0;
        for (int i = 0; i < poolSize; i++)
        {
            var go = Instantiate(particlePrefab, poolParent);
            go.SetActive(false);
            var systems = go.GetComponentsInChildren<ParticleSystem>(true);
            _entries[i] = new Entry
            {
                Go = go,
                Systems = systems,
                Life = new SerialDisposable()
            };
        }
        return true;
    }

    private float ComputeLifetime(ParticleSystem[] systems)
    {
        var max = 0f;
        if (systems == null || systems.Length == 0) return fallbackLifetime;
        foreach (var ps in systems)
        {
            var m = ps.main;
            var dur = m.duration;
            var life = m.startLifetime.constantMax; 
            max = Mathf.Max(max, dur + life);
        }
        if (max <= 0.01f) max = fallbackLifetime;
        return max;
    }

    private class Entry
    {
        public GameObject Go;
        public ParticleSystem[] Systems;
        public SerialDisposable Life;
    }
}