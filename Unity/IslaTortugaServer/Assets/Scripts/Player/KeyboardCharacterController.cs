using UnityEngine;
using UnityEngine.InputSystem;

namespace IslaTortuga.Unity.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class KeyboardCharacterController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float runSpeed = 8f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Orientation")]
        [SerializeField] private bool rotateTowardsMovement = true;
        [SerializeField] private bool moveRelativeToCamera = true;
        [SerializeField] private Transform cameraTransform;

        [Header("Animation")]
        [SerializeField] private Animator animator;

        private CharacterController _characterController;
        private Vector3 _verticalVelocity;

        private static readonly int IsGroundedHash = Animator.StringToHash("isGrounded");
        private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");
        private static readonly int IsRunningHash = Animator.StringToHash("isRunning");
        private static readonly int VerticalSpeedHash = Animator.StringToHash("verticalSpeed");
        private static readonly int JumpHash = Animator.StringToHash("jump");

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                UpdateAnimatorState(false, false, false);
                return;
            }

            var moveInput = ReadMovementInput(keyboard);
            var moveDirection = ResolveMoveDirection(moveInput);
            var wantsToMove = moveDirection.sqrMagnitude > 0.001f;
            var wantsToRun = wantsToMove && IsSprintPressed(keyboard);
            var currentMoveSpeed = wantsToRun ? runSpeed : moveSpeed;

            if (_characterController.isGrounded && _verticalVelocity.y < 0f)
            {
                _verticalVelocity.y = groundedStickForce;
            }

            var jumpPressedThisFrame = _characterController.isGrounded && keyboard.spaceKey.wasPressedThisFrame;
            if (jumpPressedThisFrame)
            {
                _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity.y += gravity * Time.deltaTime;

            var motion = moveDirection * currentMoveSpeed;
            motion.y = _verticalVelocity.y;

            _characterController.Move(motion * Time.deltaTime);

            if (rotateTowardsMovement && moveDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            }

            UpdateAnimatorState(wantsToMove, wantsToRun, jumpPressedThisFrame);
        }

        private void UpdateAnimatorState(bool wantsToMove, bool wantsToRun, bool jumpPressedThisFrame)
        {
            if (animator == null)
            {
                return;
            }

            var isGrounded = _characterController != null && _characterController.isGrounded;
            var isRunning = isGrounded && wantsToRun;
            var isWalking = isGrounded && wantsToMove && !isRunning;

            animator.SetBool(IsGroundedHash, isGrounded);
            animator.SetBool(IsWalkingHash, isWalking);
            animator.SetBool(IsRunningHash, isRunning);
            animator.SetFloat(VerticalSpeedHash, _verticalVelocity.y);

            if (jumpPressedThisFrame)
            {
                animator.SetTrigger(JumpHash);
            }
        }

        private static bool IsSprintPressed(Keyboard keyboard)
        {
            return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        }

        private static Vector2 ReadMovementInput(Keyboard keyboard)
        {
            var x = 0f;
            var y = 0f;

            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                x -= 1f;
            }

            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                x += 1f;
            }

            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                y -= 1f;
            }

            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                y += 1f;
            }

            var input = new Vector2(x, y);
            return input.sqrMagnitude > 1f ? input.normalized : input;
        }

        private Vector3 ResolveMoveDirection(Vector2 moveInput)
        {
            var moveDirection = new Vector3(moveInput.x, 0f, moveInput.y);

            if (!moveRelativeToCamera || cameraTransform == null)
            {
                return moveDirection;
            }

            var cameraForward = cameraTransform.forward;
            cameraForward.y = 0f;
            cameraForward.Normalize();

            var cameraRight = cameraTransform.right;
            cameraRight.y = 0f;
            cameraRight.Normalize();

            var relativeDirection = cameraForward * moveInput.y + cameraRight * moveInput.x;
            return relativeDirection.sqrMagnitude > 1f ? relativeDirection.normalized : relativeDirection;
        }
    }
}
