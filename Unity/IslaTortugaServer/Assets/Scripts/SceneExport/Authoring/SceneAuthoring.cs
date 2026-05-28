using UnityEngine;

namespace IslaTortuga.Unity.SceneExport.Authoring
{
    public enum SceneExportMode
    {
        SceneOnly = 1,
    }

    public enum DefaultSceneInstanceKind
    {
        Shared = 1,
        PerPlayer = 2,
        PerParty = 3,
        Named = 4,
    }

    public enum SceneColliderKind
    {
        Blocking = 1,
        WalkableModifier = 2,
        Trigger = 3,
    }

    public enum SceneColliderShapeOverride
    {
        Auto = 1,
        Box = 2,
        Sphere = 3,
        Capsule = 4,
        MeshApprox = 5,
    }

    public enum SceneClientCollisionMode
    {
        None = 1,
        Simple = 2,
        Full = 3,
    }

    public enum SceneSpawnType
    {
        PlayerDefault = 1,
        PlayerInteriorEntry = 2,
        Npc = 3,
        Custom = 4,
    }

    public enum SceneFacingDirection
    {
        Down = 1,
        Up = 2,
        Left = 3,
        Right = 4,
    }

    public enum SceneTransitionInstanceMode
    {
        Shared = 1,
        PerPlayer = 2,
        PerParty = 3,
        Named = 4,
    }

    public enum SceneTransitionShape
    {
        Auto = 1,
        Box = 2,
        Sphere = 3,
        Capsule = 4,
    }

    public enum ScenePropExportMode
    {
        StaticMesh = 1,
        PrimitiveProxy = 2,
        Ignore = 3,
    }

    public enum SceneStaticCollisionSource
    {
        None = 1,
        LinkedColliders = 2,
    }

    public enum SceneLightType
    {
        Point = 1,
        Spot = 2,
        Directional = 3,
    }

    [DisallowMultipleComponent]
    public sealed class SceneExportRoot : MonoBehaviour
    {
        [SerializeField] private string sceneId = string.Empty;
        [SerializeField] private string displayName = string.Empty;
        [SerializeField] private SceneExportMode exportMode = SceneExportMode.SceneOnly;
        [SerializeField] private float coordinateScale = 1f;
        [SerializeField] private DefaultSceneInstanceKind defaultSceneInstanceKind = DefaultSceneInstanceKind.Shared;
        [SerializeField] private bool includeLighting = true;
        [SerializeField] private bool includeAudio;

        public string SceneId
        {
            get { return sceneId; }
            set { sceneId = value; }
        }

        public string DisplayName
        {
            get { return displayName; }
            set { displayName = value; }
        }

        public SceneExportMode ExportMode
        {
            get { return exportMode; }
        }

        public float CoordinateScale
        {
            get { return coordinateScale <= 0f ? 1f : coordinateScale; }
        }

        public DefaultSceneInstanceKind DefaultSceneInstanceKind
        {
            get { return defaultSceneInstanceKind; }
        }

        public bool IncludeLighting
        {
            get { return includeLighting; }
        }

        public bool IncludeAudio
        {
            get { return includeAudio; }
        }
    }

    [DisallowMultipleComponent]
    public sealed class SceneColliderAuthoring : MonoBehaviour
    {
        [SerializeField] private bool export = true;
        [SerializeField] private string colliderId = string.Empty;
        [SerializeField] private SceneColliderKind colliderKind = SceneColliderKind.Blocking;
        [SerializeField] private SceneColliderShapeOverride shapeOverride = SceneColliderShapeOverride.Auto;
        [SerializeField] private SceneClientCollisionMode clientCollision = SceneClientCollisionMode.Simple;

        public bool Export
        {
            get { return export; }
        }

        public string ColliderId
        {
            get { return colliderId; }
            set { colliderId = value; }
        }

        public SceneColliderKind ColliderKind
        {
            get { return colliderKind; }
        }

        public SceneColliderShapeOverride ShapeOverride
        {
            get { return shapeOverride; }
        }

        public SceneClientCollisionMode ClientCollision
        {
            get { return clientCollision; }
        }
    }

    [DisallowMultipleComponent]
    public sealed class SceneSpawnPointAuthoring : MonoBehaviour
    {
        [SerializeField] private bool export = true;
        [SerializeField] private string spawnId = string.Empty;
        [SerializeField] private SceneSpawnType spawnType = SceneSpawnType.PlayerDefault;
        [SerializeField] private SceneFacingDirection facing = SceneFacingDirection.Down;

        public bool Export
        {
            get { return export; }
        }

        public string SpawnId
        {
            get { return spawnId; }
            set { spawnId = value; }
        }

        public SceneSpawnType SpawnType
        {
            get { return spawnType; }
        }

        public SceneFacingDirection Facing
        {
            get { return facing; }
        }
    }

    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    public sealed class SceneTransitionAuthoring : MonoBehaviour
    {
        [SerializeField] private bool export = true;
        [SerializeField] private string transitionId = string.Empty;
        [SerializeField] private string targetSceneId = string.Empty;
        [SerializeField] private string targetSpawnId = string.Empty;
        [SerializeField] private SceneTransitionInstanceMode instanceMode = SceneTransitionInstanceMode.Shared;
        [SerializeField] private string namedInstanceId = string.Empty;
        [SerializeField] private SceneTransitionShape transitionShape = SceneTransitionShape.Auto;

        public bool Export
        {
            get { return export; }
        }

        public string TransitionId
        {
            get { return transitionId; }
            set { transitionId = value; }
        }

        public string TargetSceneId
        {
            get { return targetSceneId; }
        }

        public string TargetSpawnId
        {
            get { return targetSpawnId; }
        }

        public SceneTransitionInstanceMode InstanceMode
        {
            get { return instanceMode; }
        }

        public string NamedInstanceId
        {
            get { return namedInstanceId; }
        }

        public SceneTransitionShape TransitionShape
        {
            get { return transitionShape; }
        }
    }

    [DisallowMultipleComponent]
    public sealed class ScenePropAuthoring : MonoBehaviour
    {
        [SerializeField] private bool export = true;
        [SerializeField] private string propId = string.Empty;
        [SerializeField] private string visualAssetId = string.Empty;
        [SerializeField] private ScenePropExportMode exportMode = ScenePropExportMode.StaticMesh;
        [SerializeField] private SceneStaticCollisionSource staticCollisionSource = SceneStaticCollisionSource.None;

        public bool Export
        {
            get { return export; }
        }

        public string PropId
        {
            get { return propId; }
            set { propId = value; }
        }

        public string VisualAssetId
        {
            get { return visualAssetId; }
        }

        public ScenePropExportMode ExportMode
        {
            get { return exportMode; }
        }

        public SceneStaticCollisionSource StaticCollisionSource
        {
            get { return staticCollisionSource; }
        }
    }

    [DisallowMultipleComponent]
    public sealed class SceneAudioEmitterAuthoring : MonoBehaviour
    {
        [SerializeField] private bool export = true;
        [SerializeField] private string audioEventId = string.Empty;
        [SerializeField] private float radius = 8f;
        [SerializeField] private bool loop = true;
        [SerializeField] private bool spatial = true;

        public bool Export
        {
            get { return export; }
        }

        public string AudioEventId
        {
            get { return audioEventId; }
        }

        public float Radius
        {
            get { return radius; }
        }

        public bool Loop
        {
            get { return loop; }
        }

        public bool Spatial
        {
            get { return spatial; }
        }
    }

    [DisallowMultipleComponent]
    public sealed class SceneLightAuthoring : MonoBehaviour
    {
        [SerializeField] private bool export = true;
        [SerializeField] private SceneLightType lightType = SceneLightType.Point;
        [SerializeField] private Color color = Color.white;
        [SerializeField] private float intensity = 1f;
        [SerializeField] private float range = 10f;

        public bool Export
        {
            get { return export; }
        }

        public SceneLightType LightType
        {
            get { return lightType; }
        }

        public Color Color
        {
            get { return color; }
        }

        public float Intensity
        {
            get { return intensity; }
        }

        public float Range
        {
            get { return range; }
        }
    }
}
