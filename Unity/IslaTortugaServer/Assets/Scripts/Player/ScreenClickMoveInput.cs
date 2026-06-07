using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace IslaTortuga.Unity.Player
{
    public sealed class ScreenClickMoveInput : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private NavMeshCharacterMover characterMover;
        [SerializeField] private Camera raycastCamera;

        [Header("Raycast")]
        [SerializeField] private LayerMask raycastMask = Physics.DefaultRaycastLayers;
        [SerializeField] private float raycastDistance = 1000f;
        [SerializeField] private bool ignoreClicksOverUi = true;

        [Header("Fallback")]
        [SerializeField] private bool useGroundPlaneFallback = false;
        [SerializeField] private float groundPlaneHeight = 0f;

        [Header("Input")]
        [SerializeField] private bool useLeftMouseButton = true;

        private void Awake()
        {
            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }
        }

        private void OnValidate()
        {
            raycastDistance = Mathf.Max(0.1f, raycastDistance);
        }

        private void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null || characterMover == null)
            {
                return;
            }

            var button = useLeftMouseButton ? mouse.leftButton : mouse.rightButton;
            if (!button.wasPressedThisFrame)
            {
                return;
            }

            if (ignoreClicksOverUi && EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (raycastCamera == null)
            {
                raycastCamera = Camera.main;
            }

            if (raycastCamera == null)
            {
                return;
            }

            if (TryResolveWorldPoint(mouse.position.ReadValue(), out var worldPoint))
            {
                characterMover.MoveTo(worldPoint);
            }
        }

        public bool TryResolveWorldPoint(Vector2 screenPosition, out Vector3 worldPoint)
        {
            if (raycastCamera == null)
            {
                worldPoint = default;
                return false;
            }

            var ray = raycastCamera.ScreenPointToRay(screenPosition);
            if (Physics.Raycast(ray, out var hit, raycastDistance, raycastMask, QueryTriggerInteraction.Ignore))
            {
                worldPoint = hit.point;
                return true;
            }

            if (useGroundPlaneFallback)
            {
                var groundPlane = new Plane(Vector3.up, new Vector3(0f, groundPlaneHeight, 0f));
                if (groundPlane.Raycast(ray, out var enterDistance))
                {
                    worldPoint = ray.GetPoint(enterDistance);
                    return true;
                }
            }

            worldPoint = default;
            return false;
        }
    }
}
