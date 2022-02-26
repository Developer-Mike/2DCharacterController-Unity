using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(1)]
public class Follow : MonoBehaviour {
    public Transform target;
    public bool inUpdate;
    public bool inFixedUpdate;
    [Header("Position")]
    public Vector3Int axisToFollow;
    public bool relativeOffset;
    public Vector3 positionOffset;
    public float positionSmoothTime;
    Vector3 positionVelocity;
    [Header("Rotation")]
    public bool rotate;
    public Vector3Int rotationFollowAxis;
    public Vector3 rotationOffset;
    public float rotationSmoothTime;
    Vector3 rotationVelocity;
    [Header("Lean")]
    public bool lean;
    public float leanAmount;
    public float leanSmoothTime;
    float leanVelocity;

    private void LateUpdate() {
        if (!inUpdate) return;
        FollowTarget();
        Rotate();
        Lean();
    }

    private void FixedUpdate() {
        if (!inFixedUpdate) return;
        FollowTarget();
        Rotate();
        Lean();
    }

    void FollowTarget() {
        Vector3 absolutePositionOffset = positionOffset;
        if (relativeOffset) absolutePositionOffset = target.TransformDirection(absolutePositionOffset);

        Vector3 targetPos = transform.position;
        if (axisToFollow.x > 0) targetPos.x = target.position.x + absolutePositionOffset.x;
        if (axisToFollow.y > 0) targetPos.y = target.position.y + absolutePositionOffset.y;
        if (axisToFollow.z > 0) targetPos.z = target.position.z + absolutePositionOffset.z;

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref positionVelocity, positionSmoothTime);
    }

    void Rotate() {
        if (!rotate) return;

        Vector3 fixedCurrentRotation = FixEulerAngles(transform.eulerAngles);

        Vector3 targetRotation = transform.eulerAngles;
        if (rotationFollowAxis.x > 0) targetRotation.x = target.eulerAngles.x + rotationOffset.x;
        if (rotationFollowAxis.y > 0) targetRotation.y = target.eulerAngles.y + rotationOffset.y;
        if (rotationFollowAxis.z > 0) targetRotation.z = target.eulerAngles.z + rotationOffset.z;

        targetRotation = FixEulerAngles(targetRotation);

        transform.eulerAngles = Vector3.SmoothDamp(fixedCurrentRotation, targetRotation, ref rotationVelocity, rotationSmoothTime);
    }

    void Lean() {
        if (!lean) return;

        float currentZRotation = transform.eulerAngles.z;
        if (currentZRotation > 180) currentZRotation -= 360;

        float targetZRotation = target.eulerAngles.y;
        if (targetZRotation > 180) targetZRotation -= 360;

        transform.eulerAngles = new Vector3(
            transform.eulerAngles.x,
            transform.eulerAngles.y,
            Mathf.SmoothDamp(currentZRotation, targetZRotation * leanAmount, ref leanVelocity, leanSmoothTime)
        );
    }

    public Vector3 FixEulerAngles(Vector3 startAngle) {
        Vector3 targetAngle = startAngle;
        
        if (targetAngle.x > 180) targetAngle.x -= 360;
        if (targetAngle.y > 180) targetAngle.y -= 360;
        if (targetAngle.z > 180) targetAngle.z -= 360;

        return targetAngle;
    }
}
