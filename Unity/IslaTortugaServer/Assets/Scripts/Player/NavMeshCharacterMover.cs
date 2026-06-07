using UnityEngine;
using UnityEngine.AI;

namespace IslaTortuga.Unity.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class NavMeshCharacterMover : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;
        [SerializeField] private float arrivalTolerance = 0.05f;

        [Header("Navigation")]
        [SerializeField] private bool projectDestinationOnNavMesh = true;
        [SerializeField] private float destinationProjectionDistance = 2f;
        [SerializeField] private float navMeshBindDistance = 2f;

        [Header("Orientation")]
        [SerializeField] private bool rotateTowardsMovement = true;
        [SerializeField] private float rotationSharpness = 12f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private float walkStartSpeedThreshold = 0.15f;
        [SerializeField] private float walkStopSpeedThreshold = 0.05f;
        [SerializeField] private float runningSpeedThreshold = 4.5f;

        private CharacterController _characterController;
        private NavMeshAgent _navMeshAgent;
        private Vector3 _verticalVelocity;
        private Vector3 _lastPosition;
        private bool _hasLastPosition;
        private bool _isWalking;

        private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
        private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
        private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("verticalSpeed");

        public Vector3 Destination
        {
            get { return _navMeshAgent != null ? _navMeshAgent.destination : transform.position; }
        }

        public bool HasPath
        {
            get { return _navMeshAgent != null && _navMeshAgent.hasPath; }
        }

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _navMeshAgent = GetComponent<NavMeshAgent>();

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            _navMeshAgent.updatePosition = false;
            _navMeshAgent.updateRotation = false;
        }

        private void OnEnable()
        {
            SyncAgentToTransform();
            EnsureAgentIsOnNavMesh();
            _lastPosition = transform.position;
            _hasLastPosition = true;
            _isWalking = false;
        }

        private void OnValidate()
        {
            destinationProjectionDistance = Mathf.Max(0.1f, destinationProjectionDistance);
            navMeshBindDistance = Mathf.Max(0.1f, navMeshBindDistance);
            arrivalTolerance = Mathf.Max(0f, arrivalTolerance);
            rotationSharpness = Mathf.Max(0f, rotationSharpness);
            walkStartSpeedThreshold = Mathf.Max(0f, walkStartSpeedThreshold);
            walkStopSpeedThreshold = Mathf.Clamp(walkStopSpeedThreshold, 0f, walkStartSpeedThreshold);
            runningSpeedThreshold = Mathf.Max(0f, runningSpeedThreshold);
        }

        private void Update()
        {
            if (_characterController == null || _navMeshAgent == null)
            {
                return;
            }

            EnsureAgentIsOnNavMesh();

            if (!_hasLastPosition)
            {
                _lastPosition = transform.position;
                _hasLastPosition = true;
            }

            var previousPosition = transform.position;

            if (_characterController.isGrounded && _verticalVelocity.y < 0f)
            {
                _verticalVelocity.y = groundedStickForce;
            }

            _verticalVelocity.y += gravity * Time.deltaTime;

            var planarVelocity = ResolvePlanarVelocity();
            var motion = planarVelocity;
            motion.y = _verticalVelocity.y;

            _characterController.Move(motion * Time.deltaTime);
            SyncAgentToTransform();

            if (rotateTowardsMovement && planarVelocity.sqrMagnitude > 0.001f)
            {
                var targetRotation = Quaternion.LookRotation(planarVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-rotationSharpness * Time.deltaTime));
            }

            if (HasArrived())
            {
                StopMovement();
            }

            var actualPlanarVelocity = ResolveActualPlanarVelocity(previousPosition, transform.position);
            UpdateAnimatorState(actualPlanarVelocity, planarVelocity);
        }

        public bool MoveTo(Vector3 worldPosition)
        {
            if (_navMeshAgent == null || !EnsureAgentIsOnNavMesh())
            {
                return false;
            }

            var targetPosition = worldPosition;
            if (projectDestinationOnNavMesh)
            {
                if (!NavMesh.SamplePosition(worldPosition, out var hit, destinationProjectionDistance, _navMeshAgent.areaMask))
                {
                    return false;
                }

                targetPosition = hit.position;
            }

            _navMeshAgent.isStopped = false;
            return _navMeshAgent.SetDestination(targetPosition);
        }

        public void StopMovement()
        {
            if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh)
            {
                return;
            }

            _navMeshAgent.ResetPath();
            _navMeshAgent.isStopped = true;
        }

        private bool EnsureAgentIsOnNavMesh()
        {
            if (_navMeshAgent == null)
            {
                return false;
            }

            if (_navMeshAgent.isOnNavMesh)
            {
                return true;
            }

            if (!NavMesh.SamplePosition(transform.position, out var hit, navMeshBindDistance, _navMeshAgent.areaMask))
            {
                return false;
            }

            transform.position = hit.position;
            _navMeshAgent.Warp(hit.position);
            _navMeshAgent.nextPosition = hit.position;
            return true;
        }

        private Vector3 ResolvePlanarVelocity()
        {
            if (_navMeshAgent == null ||
                !_navMeshAgent.isOnNavMesh ||
                _navMeshAgent.isStopped ||
                !ShouldKeepNavigating())
            {
                return Vector3.zero;
            }

            var desiredVelocity = _navMeshAgent.desiredVelocity;
            desiredVelocity.y = 0f;

            if (desiredVelocity.sqrMagnitude <= 0.0001f)
            {
                return Vector3.zero;
            }

            return Vector3.ClampMagnitude(desiredVelocity, _navMeshAgent.speed);
        }

        private bool HasArrived()
        {
            if (_navMeshAgent == null ||
                !_navMeshAgent.isOnNavMesh ||
                _navMeshAgent.pathPending ||
                !_navMeshAgent.hasPath)
            {
                return false;
            }

            if (_navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance + arrivalTolerance)
            {
                return false;
            }

            return _navMeshAgent.desiredVelocity.sqrMagnitude <= 0.01f;
        }

        private void SyncAgentToTransform()
        {
            if (_navMeshAgent == null || !_navMeshAgent.isOnNavMesh)
            {
                return;
            }

            _navMeshAgent.nextPosition = transform.position;
        }

        private Vector3 ResolveActualPlanarVelocity(Vector3 previousPosition, Vector3 currentPosition)
        {
            var displacement = currentPosition - previousPosition;
            displacement.y = 0f;

            _lastPosition = currentPosition;
            if (Time.deltaTime <= Mathf.Epsilon)
            {
                return Vector3.zero;
            }

            return displacement / Time.deltaTime;
        }

        private bool ShouldKeepNavigating()
        {
            if (_navMeshAgent == null ||
                !_navMeshAgent.isOnNavMesh ||
                _navMeshAgent.isStopped ||
                _navMeshAgent.pathPending ||
                !_navMeshAgent.hasPath)
            {
                return false;
            }

            return _navMeshAgent.remainingDistance > _navMeshAgent.stoppingDistance + arrivalTolerance;
        }

        private void UpdateAnimatorState(Vector3 actualPlanarVelocity, Vector3 desiredPlanarVelocity)
        {
            if (animator == null)
            {
                return;
            }

            var isGrounded = _characterController != null && _characterController.isGrounded;
            var actualPlanarSpeed = actualPlanarVelocity.magnitude;
            var desiredPlanarSpeed = desiredPlanarVelocity.magnitude;
            var referencePlanarSpeed = Mathf.Max(actualPlanarSpeed, desiredPlanarSpeed);
            var shouldKeepNavigating = ShouldKeepNavigating();

            if (!_isWalking)
            {
                _isWalking = isGrounded &&
                    (actualPlanarSpeed >= walkStartSpeedThreshold ||
                    (shouldKeepNavigating && desiredPlanarSpeed >= walkStartSpeedThreshold));
            }
            else
            {
                var shouldStopWalking =
                    actualPlanarSpeed <= walkStopSpeedThreshold &&
                    (!shouldKeepNavigating || desiredPlanarSpeed <= walkStopSpeedThreshold);

                if (shouldStopWalking)
                {
                    _isWalking = false;
                }
            }

            var isRunning = _isWalking && referencePlanarSpeed >= runningSpeedThreshold;

            animator.SetBool(IsGroundedHash, isGrounded);
            animator.SetBool(IsWalkingHash, _isWalking);
            animator.SetBool(IsRunningHash, isRunning);
            animator.SetFloat(VerticalSpeedHash, _verticalVelocity.y);
        }
    }
}
