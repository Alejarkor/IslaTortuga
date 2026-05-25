using UnityEngine;
using UnityEngine.InputSystem;

namespace IslaTortuga.Unity.Player
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class KeyboardCharacterController : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float jumpHeight = 1.5f;
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundedStickForce = -2f;

        [Header("Orientation")]
        [SerializeField] private bool rotateTowardsMovement = true;
        [SerializeField] private bool moveRelativeToCamera = true;
        [SerializeField] private Transform cameraTransform;

        private CharacterController _characterController;
        private Vector3 _verticalVelocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();

            if (cameraTransform == null && Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            var moveInput = ReadMovementInput(keyboard);
            var moveDirection = ResolveMoveDirection(moveInput);

            if (_characterController.isGrounded && _verticalVelocity.y < 0f)
            {
                _verticalVelocity.y = groundedStickForce;
            }

            if (_characterController.isGrounded && keyboard.spaceKey.wasPressedThisFrame)
            {
                _verticalVelocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }

            _verticalVelocity.y += gravity * Time.deltaTime;

            var motion = moveDirection * moveSpeed;
            motion.y = _verticalVelocity.y;

            _characterController.Move(motion * Time.deltaTime);

            if (rotateTowardsMovement && moveDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            }
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
