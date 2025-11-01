using System;
using UnityEngine;
using UniRx;
using Zenject;

namespace Test
{
    public class ForwardHitter : MonoBehaviour
    {
        [Header("Setup")]
        [SerializeField] private AudioClip hitSound1;
        [SerializeField] private AudioClip hitSound2;        
        private AudioSource audioSource;
        private Animator animator;

        [Header("Hit Settings")]
        [SerializeField] private KeyCode hitKey = KeyCode.Mouse0;
        [SerializeField]
        private float hitCooldown = 0.35f;

        private bool _isRightHit = false;
        private float _nextShootTime = 0f;

        [Header("Detection")]
        [SerializeField] private Transform leftFist;
        [SerializeField] private Transform rightFist;
        [SerializeField] private float hitRange = 1.5f;
        [SerializeField] private float hitRadius = 0.25f;
        [SerializeField] private LayerMask enemyMask = ~0;

        [Header("Collision Emitters")]
        [SerializeField] private HitCollisionEmitter leftEmitter;
        [SerializeField] private HitCollisionEmitter rightEmitter;
        [SerializeField] private float gateDuration = 0.15f;

        [Header("Particles")]
        [Inject] private HitParticlePool _particlePool;                    

        private CompositeDisposable _disposables;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            audioSource = GetComponent<AudioSource>();

            if (!leftEmitter && leftFist) leftEmitter = leftFist.GetComponent<HitCollisionEmitter>();
            if (!rightEmitter && rightFist) rightEmitter = rightFist.GetComponent<HitCollisionEmitter>();
        }

        void OnEnable()
        {
            _disposables = new CompositeDisposable();

            Observable.EveryUpdate()
                .Where(_ => Input.GetKeyDown(hitKey))
                .ThrottleFirst(TimeSpan.FromSeconds(Mathf.Max(0f, hitCooldown)))
                .Subscribe(_ => TriggerHit())
                .AddTo(_disposables);
        }

        void OnDisable()
        {
            _disposables?.Dispose();
            _disposables = null;
        }

        private void TriggerHit()
        {
            animator.SetTrigger(_isRightHit ? "RightHit" : "LeftHit");
            _isRightHit = !_isRightHit;
        }

        public void HitEventLeft()  { OpenGate(leftEmitter);  TryDetectAndPlay(leftFist);  HitSound(); }
        public void HitEventRight() { OpenGate(rightEmitter); TryDetectAndPlay(rightFist); HitSound(); }
        
        private void OpenGate(HitCollisionEmitter emitter)
        {
            if (emitter) emitter.StartGate(gateDuration);
        }

        private void TryDetectAndPlay(Transform originT)
        {
            Vector3 origin = originT ? originT.position : transform.position;
            Vector3 dir    = originT ? originT.forward  : transform.forward;

            if (hitRadius > 0f)
            {
                var hits = Physics.SphereCastAll(origin, hitRadius, dir, hitRange, enemyMask, QueryTriggerInteraction.Ignore);
                if (hits != null && hits.Length > 0)
                {
                    System.Array.Sort(hits, (a,b) => a.distance.CompareTo(b.distance));
                    PlayParticlesAtHit(hits[0]);
                }
            }
            else if (Physics.Raycast(origin, dir, out var h, hitRange, enemyMask, QueryTriggerInteraction.Ignore))
            {
                PlayParticlesAtHit(h);
            }
        }

        private void PlayParticlesAtHit(RaycastHit h)
        {
            if (!_particlePool) return;
            _particlePool.PlayAt(h.point, h.normal);
        }

        public void HitSound()
        {
            audioSource.clip = _isRightHit ? hitSound1 : hitSound2;
            audioSource.Play();
        }
    }
}
