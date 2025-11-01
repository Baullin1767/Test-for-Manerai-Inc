using UniRx;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[DisallowMultipleComponent]
public class HeadUprightController : MonoBehaviour
{
    [Header("Joint & Bodies")] 
    [SerializeField]
    private ConfigurableJoint joint;

    [SerializeField] private Rigidbody headBody;

    [Header("Upright Target")] 
    [SerializeField]
    private Vector3 worldUp;

    [Tooltip("Dead zone around upright (degrees) before updating targetRotation.")] [SerializeField]
    private float deadZoneDeg = 1.0f;

    [Header("Slerp Drive (Joint)")] 
    [Tooltip("Spring strength driving head to upright (Slerp drive).")] [SerializeField]
    private float slerpSpring = 8f;

    [Tooltip("Damping to reduce oscillation (Slerp drive).")] [SerializeField]
    private float slerpDamper = 1f;

    [Tooltip("Maximum force the drive may apply. Use Infinity for no cap.")] [SerializeField]
    private float slerpMaxForce = Mathf.Infinity;

    [Header("Return Boost")]
    [Tooltip("Accelerate return when deflection is large (multiplies spring and damper).")]
    [SerializeField]
    private bool useReturnBoost = true;

    [Tooltip("Angle threshold (degrees) above which boost is applied.")] [SerializeField]
    private float boostAngleDeg = 10f;

    [Tooltip("Spring multiplier when boost is active.")] [SerializeField]
    private float boostSpringMul = 2.0f;

    [Tooltip("Damper multiplier when boost is active.")] [SerializeField]
    private float boostDamperMul = 1.2f;

    [Header("Rigidbody Tweaks")]
    [Tooltip("Head rigidbody max angular velocity. Increase for faster return.")] [SerializeField]
    private float maxAngularVelocity = 25f;

    private Quaternion _startLocalRotation;
    private CompositeDisposable _disposables;

    private void Reset()
    {
        if (joint && !headBody) headBody = joint.GetComponent<Rigidbody>();
        if (worldUp == Vector3.zero) worldUp = Vector3.up;

        if (joint != null)
        {
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            var d = joint.slerpDrive;
            d.positionSpring = slerpSpring;
            d.positionDamper = slerpDamper;
            d.maximumForce = slerpMaxForce;
            joint.slerpDrive = d;

            joint.xMotion = ConfigurableJointMotion.Locked;
            joint.yMotion = ConfigurableJointMotion.Locked;
            joint.zMotion = ConfigurableJointMotion.Locked;
        }
    }

    private void Awake()
    {
        if (joint && !headBody) headBody = joint.GetComponent<Rigidbody>();
        if (worldUp == Vector3.zero) worldUp = Vector3.up;

        CacheStartLocalRotation();
        ApplyDriveTuning();

        if (headBody)
        {
            headBody.maxAngularVelocity = Mathf.Max(headBody.maxAngularVelocity, maxAngularVelocity);
        }
    }

    private void OnValidate()
    {
        if (joint)
        {
            joint.rotationDriveMode = RotationDriveMode.Slerp;
            ApplyDriveTuning();
        }

        if (headBody)
        {
            headBody.maxAngularVelocity = Mathf.Max(headBody.maxAngularVelocity, maxAngularVelocity);
        }
    }

    private void OnEnable()
    {
        _disposables = new CompositeDisposable();
        Observable.EveryFixedUpdate()
            .Subscribe(_ => StepUprightControl())
            .AddTo(_disposables);
    }

    private void OnDisable()
    {
        _disposables?.Dispose();
        _disposables = null;
    }

    private void CacheStartLocalRotation()
    {
        if (!joint || !headBody) return;
        Quaternion connectedRot = joint && joint.connectedBody
            ? joint.connectedBody.rotation
            : Quaternion.identity;
        _startLocalRotation = Quaternion.Inverse(connectedRot) * headBody.rotation;
    }

    private void ApplyDriveTuning()
    {
        if (!joint) return;
        var d = joint.slerpDrive;
        d.positionSpring = slerpSpring;
        d.positionDamper = slerpDamper;
        d.maximumForce = slerpMaxForce;
        joint.slerpDrive = d;
    }

    private void ApplyDriveTuning(float spring, float damper)
    {
        if (!joint) return;
        var d = joint.slerpDrive;
        d.positionSpring = spring;
        d.positionDamper = damper;
        d.maximumForce = slerpMaxForce;
        joint.slerpDrive = d;
    }

    private void StepUprightControl()
    {
        if (!joint || !headBody) return;

        Vector3 upTarget = (worldUp == Vector3.zero ? Vector3.up : worldUp).normalized;
        Vector3 upCurrent = headBody.transform.up;

        var angleErr = Vector3.Angle(upCurrent, upTarget);
        if (angleErr <= deadZoneDeg)
        {
            return;
        }

        Quaternion toUpright = Quaternion.FromToRotation(upCurrent, upTarget);
        Quaternion desiredWorldRot = toUpright * headBody.rotation;

        Quaternion connectedRot = joint.connectedBody ? joint.connectedBody.rotation : Quaternion.identity;
        Quaternion targetLocal = Quaternion.Inverse(connectedRot) * desiredWorldRot;

        joint.rotationDriveMode = RotationDriveMode.Slerp;
        joint.targetRotation = Quaternion.Inverse(targetLocal) * _startLocalRotation;

        if (useReturnBoost && angleErr >= boostAngleDeg)
        {
            ApplyDriveTuning(slerpSpring * boostSpringMul, slerpDamper * boostDamperMul);
        }
        else
        {
            ApplyDriveTuning();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!joint) return;
        Vector3 axisWorld = joint.transform.TransformDirection(joint.axis).normalized;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(joint.transform.position, joint.transform.position + axisWorld * 0.5f);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(joint.transform.position, joint.transform.position + Vector3.up * 0.5f);
    }
}