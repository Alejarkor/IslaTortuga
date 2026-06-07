using UnityEngine;
using UnityEngine.InputSystem;

namespace IslaTortuga.Unity.Cameras
{
    public sealed class ThirdPersonOrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        [SerializeField] private Transform target;
        [SerializeField] private float targetHeight = 1.6f;
        [SerializeField] private float followSmoothTime = 0.08f;

        [Header("Distance")]
        [SerializeField] private float minDistance = 3f;
        [SerializeField] private float maxDistance = 7f;
        [SerializeField] private float zoomStep = 0.5f;
        [SerializeField] private float distanceSmoothTime = 0.15f;

        [Header("Speed Lag")]
        [SerializeField] private float speedForMaxDistance = 8f;

        [Header("Rotation")]
        [SerializeField] private float yawSensitivity = 0.12f;
        [SerializeField] private float pitchSensitivity = 0.12f;
        [SerializeField] private float minPitch = -20f;
        [SerializeField] private float maxPitch = 70f;
        [SerializeField] private float initialPitch = 20f;

        private Vector3 _currentFocusPoint;
        private Vector3 _focusVelocity;
        private Vector3 _lastTargetPosition;
        private float _yaw;
        private float _pitch;
        private float _manualDistance;
        private float _currentDistance;
        private float _distanceVelocity;
        private bool _hasTargetPosition;

        private void Awake()
        {
            minDistance = Mathf.Max(0.1f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            _pitch = Mathf.Clamp(initialPitch, minPitch, maxPitch);
            _manualDistance = minDistance;
            _currentDistance = _manualDistance;

            if (target != null)
            {
                SnapToTarget();
            }
        }

        private void OnValidate()
        {
            minDistance = Mathf.Max(0.1f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            ReadZoomInput();
            ReadRotationInput();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            var targetPoint = target.position + Vector3.up * targetHeight;
            _currentFocusPoint = Vector3.SmoothDamp(
                _currentFocusPoint,
                targetPoint,
                ref _focusVelocity,
                followSmoothTime);

            var planarSpeed = GetPlanarTargetSpeed();
            var extraDistance = 0f;
            if (_manualDistance < maxDistance && speedForMaxDistance > 0f)
            {
                var normalizedSpeed = Mathf.Clamp01(planarSpeed / speedForMaxDistance);
                extraDistance = normalizedSpeed * (maxDistance - _manualDistance);
            }

            var desiredDistance = Mathf.Clamp(_manualDistance + extraDistance, minDistance, maxDistance);
            _currentDistance = Mathf.SmoothDamp(
                _currentDistance,
                desiredDistance,
                ref _distanceVelocity,
                distanceSmoothTime);

            var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            var cameraOffset = rotation * new Vector3(0f, 0f, -_currentDistance);

            transform.position = _currentFocusPoint + cameraOffset;
            transform.rotation = Quaternion.LookRotation(_currentFocusPoint - transform.position, Vector3.up);
        }

        private void ReadZoomInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var scrollY = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) < 0.01f)
            {
                return;
            }

            _manualDistance = Mathf.Clamp(
                _manualDistance - Mathf.Sign(scrollY) * zoomStep,
                minDistance,
                maxDistance);
        }

        private void ReadRotationInput()
        {
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            var mouseDelta = mouse.delta.ReadValue();
            _yaw += mouseDelta.x * yawSensitivity;
            _pitch = Mathf.Clamp(_pitch - mouseDelta.y * pitchSensitivity, minPitch, maxPitch);
        }

        private float GetPlanarTargetSpeed()
        {
            var currentTargetPosition = target.position;
            if (!_hasTargetPosition)
            {
                _lastTargetPosition = currentTargetPosition;
                _hasTargetPosition = true;
                return 0f;
            }

            var frameDisplacement = currentTargetPosition - _lastTargetPosition;
            _lastTargetPosition = currentTargetPosition;
            frameDisplacement.y = 0f;

            if (Time.deltaTime <= Mathf.Epsilon)
            {
                return 0f;
            }

            return frameDisplacement.magnitude / Time.deltaTime;
        }

        private void SnapToTarget()
        {
            _currentFocusPoint = target.position + Vector3.up * targetHeight;
            _lastTargetPosition = target.position;
            _hasTargetPosition = true;

            var directionToCamera = transform.position - _currentFocusPoint;
            if (directionToCamera.sqrMagnitude > 0.001f)
            {
                _currentDistance = Mathf.Clamp(directionToCamera.magnitude, minDistance, maxDistance);
                _manualDistance = _currentDistance;

                var flatDirection = Vector3.ProjectOnPlane(directionToCamera, Vector3.up);
                if (flatDirection.sqrMagnitude > 0.001f)
                {
                    _yaw = Quaternion.LookRotation(flatDirection, Vector3.up).eulerAngles.y;
                }

                var pitchRotation = Quaternion.LookRotation(directionToCamera.normalized, Vector3.up).eulerAngles.x;
                _pitch = NormalizePitch(pitchRotation);
                _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
            }
        }

        private static float NormalizePitch(float angle)
        {
            if (angle > 180f)
            {
                angle -= 360f;
            }

            return angle;
        }
    }
}
