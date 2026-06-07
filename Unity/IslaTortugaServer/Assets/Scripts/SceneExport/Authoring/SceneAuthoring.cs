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
            set { defaultSceneInstanceKind = value; }
        }

        public bool IncludeLighting
        {
            get { return includeLighting; }
            set { includeLighting = value; }
        }

        public bool IncludeAudio
        {
            get { return includeAudio; }
            set { includeAudio = value; }
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
            set { export = value; }
        }

        public string ColliderId
        {
            get { return colliderId; }
            set { colliderId = value; }
        }

        public SceneColliderKind ColliderKind
        {
            get { return colliderKind; }
            set { colliderKind = value; }
        }

        public SceneColliderShapeOverride ShapeOverride
        {
            get { return shapeOverride; }
            set { shapeOverride = value; }
        }

        public SceneClientCollisionMode ClientCollision
        {
            get { return clientCollision; }
            set { clientCollision = value; }
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
            set { export = value; }
        }

        public string SpawnId
        {
            get { return spawnId; }
            set { spawnId = value; }
        }

        public SceneSpawnType SpawnType
        {
            get { return spawnType; }
            set { spawnType = value; }
        }

        public SceneFacingDirection Facing
        {
            get { return facing; }
            set { facing = value; }
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
            set { export = value; }
        }

        public string TransitionId
        {
            get { return transitionId; }
            set { transitionId = value; }
        }

        public string TargetSceneId
        {
            get { return targetSceneId; }
            set { targetSceneId = value; }
        }

        public string TargetSpawnId
        {
            get { return targetSpawnId; }
            set { targetSpawnId = value; }
        }

        public SceneTransitionInstanceMode InstanceMode
        {
            get { return instanceMode; }
            set { instanceMode = value; }
        }

        public string NamedInstanceId
        {
            get { return namedInstanceId; }
            set { namedInstanceId = value; }
        }

        public SceneTransitionShape TransitionShape
        {
            get { return transitionShape; }
            set { transitionShape = value; }
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
            set { export = value; }
        }

        public string PropId
        {
            get { return propId; }
            set { propId = value; }
        }

        public string VisualAssetId
        {
            get { return visualAssetId; }
            set { visualAssetId = value; }
        }

        public ScenePropExportMode ExportMode
        {
            get { return exportMode; }
            set { exportMode = value; }
        }

        public SceneStaticCollisionSource StaticCollisionSource
        {
            get { return staticCollisionSource; }
            set { staticCollisionSource = value; }
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
            set { export = value; }
        }

        public string AudioEventId
        {
            get { return audioEventId; }
            set { audioEventId = value; }
        }

        public float Radius
        {
            get { return radius; }
            set { radius = value; }
        }

        public bool Loop
        {
            get { return loop; }
            set { loop = value; }
        }

        public bool Spatial
        {
            get { return spatial; }
            set { spatial = value; }
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
            set { export = value; }
        }

        public SceneLightType LightType
        {
            get { return lightType; }
            set { lightType = value; }
        }

        public Color Color
        {
            get { return color; }
            set { color = value; }
        }

        public float Intensity
        {
            get { return intensity; }
            set { intensity = value; }
        }

        public float Range
        {
            get { return range; }
            set { range = value; }
        }
    }
}
