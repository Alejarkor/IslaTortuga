using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using IslaTortuga.Unity.SceneExport.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslaTortuga.Unity.SceneExport.Editor
{
    internal enum SceneExportIssueSeverity
    {
        Info = 1,
        Warning = 2,
        Error = 3,
    }

    internal sealed class SceneExportIssue
    {
        public SceneExportIssue(SceneExportIssueSeverity severity, string message, UnityEngine.Object context = null)
        {
            Severity = severity;
            Message = message;
            Context = context;
        }

        public SceneExportIssueSeverity Severity { get; }

        public string Message { get; }

        public UnityEngine.Object Context { get; }
    }

    internal sealed class SceneExportResult
    {
        public SceneExportResult(
            bool success,
            string sceneId,
            string sceneDataPath,
            string manifestPath,
            string sceneDefinitionsPath,
            IReadOnlyList<SceneExportIssue> issues)
        {
            Success = success;
            SceneId = sceneId;
            SceneDataPath = sceneDataPath;
            ManifestPath = manifestPath;
            SceneDefinitionsPath = sceneDefinitionsPath;
            Issues = issues ?? Array.Empty<SceneExportIssue>();
        }

        public bool Success { get; }

        public string SceneId { get; }

        public string SceneDataPath { get; }

        public string ManifestPath { get; }

        public string SceneDefinitionsPath { get; }

        public IReadOnlyList<SceneExportIssue> Issues { get; }
    }

    internal sealed class SceneExportService
    {
        private const string SceneBuilderId = "unity-scene-export";
        private static readonly string[] ExpectedLayerNames =
        {
            "Scene_Visual",
            "Scene_Collision",
            "Scene_Trigger",
            "Scene_Spawn",
            "Scene_Transition",
            "Scene_Audio",
            "Scene_Ignore",
        };

        public SceneExportResult ValidateActiveScene()
        {
            var issues = new List<SceneExportIssue>();
            var root = ResolveActiveSceneRoot(issues);
            if (root == null)
            {
                return new SceneExportResult(false, string.Empty, string.Empty, string.Empty, string.Empty, issues);
            }

            ValidateScene(root, issues);

            return new SceneExportResult(
                issues.All(issue => issue.Severity != SceneExportIssueSeverity.Error),
                root.SceneId ?? string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                issues);
        }

        public SceneExportResult ExportActiveScene(string contentPackVersion)
        {
            var issues = new List<SceneExportIssue>();
            var root = ResolveActiveSceneRoot(issues);
            if (root == null)
            {
                return new SceneExportResult(false, string.Empty, string.Empty, string.Empty, string.Empty, issues);
            }

            ValidateScene(root, issues);
            if (issues.Any(issue => issue.Severity == SceneExportIssueSeverity.Error))
            {
                return new SceneExportResult(false, root.SceneId ?? string.Empty, string.Empty, string.Empty, string.Empty, issues);
            }

            var sceneId = root.SceneId.Trim();
            var repoRoot = ResolveRepositoryRoot();
            var contentRoot = Path.Combine(repoRoot, "content-packs");
            var version = string.IsNullOrWhiteSpace(contentPackVersion) ? "v001" : contentPackVersion.Trim();
            var packRoot = Path.Combine(contentRoot, version);
            var definitionsRoot = Path.Combine(packRoot, "definitions");
            var scenesRoot = Path.Combine(packRoot, "scenes");
            var manifestPath = Path.Combine(packRoot, "manifest.json");
            var sceneDefinitionsPath = Path.Combine(definitionsRoot, "scene-definitions.json");
            var sceneDataFileName = sceneId + ".json";
            var sceneDataPath = Path.Combine(scenesRoot, sceneDataFileName);
            var sceneDataFileId = "scene." + sceneId;
            var sceneDataUrl = "/content/" + version + "/scenes/" + sceneDataFileName.Replace("\\", "/");

            Directory.CreateDirectory(definitionsRoot);
            Directory.CreateDirectory(scenesRoot);

            var sceneDocument = BuildSceneDocument(root, issues);
            var sceneJson = SceneExportJson.Serialize(sceneDocument);
            File.WriteAllText(sceneDataPath, sceneJson, new UTF8Encoding(false));

            UpsertSceneDefinition(sceneDefinitionsPath, sceneId, sceneDataFileId);
            UpsertManifestSceneFile(manifestPath, version, sceneDataFileId, sceneDataUrl, sceneDataPath);

            AssetDatabase.Refresh();

            issues.Add(new SceneExportIssue(
                SceneExportIssueSeverity.Info,
                "Escena exportada correctamente a " + sceneDataPath + ".",
                root));

            return new SceneExportResult(true, sceneId, sceneDataPath, manifestPath, sceneDefinitionsPath, issues);
        }

        private static SceneExportRoot ResolveActiveSceneRoot(List<SceneExportIssue> issues)
        {
            var activeScene = SceneManager.GetActiveScene();
            if (!activeScene.IsValid() || !activeScene.isLoaded)
            {
                issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "No hay ninguna escena activa cargada."));
                return null;
            }

            var roots = activeScene.GetRootGameObjects()
                .Select(rootObject => rootObject.GetComponent<SceneExportRoot>())
                .Where(component => component != null)
                .ToArray();

            if (roots.Length == 0)
            {
                issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "La escena activa no contiene ningun SceneExportRoot."));
                return null;
            }

            if (roots.Length > 1)
            {
                issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "La escena activa contiene mas de un SceneExportRoot."));
                return null;
            }

            return roots[0];
        }

        private static void ValidateScene(SceneExportRoot root, List<SceneExportIssue> issues)
        {
            if (string.IsNullOrWhiteSpace(root.SceneId))
            {
                issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "SceneExportRoot necesita un sceneId.", root));
            }

            if (!root.name.StartsWith("SCN_", StringComparison.Ordinal))
            {
                issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Warning, "Se recomienda que el root exportable empiece por SCN_.", root));
            }

            ValidateExpectedLayers(issues);
            ValidateColliders(root, issues);
            ValidateSpawns(root, issues);
            ValidateTransitions(root, issues);
            ValidateProps(root, issues);
        }

        private static void ValidateExpectedLayers(List<SceneExportIssue> issues)
        {
            for (var index = 0; index < ExpectedLayerNames.Length; index++)
            {
                var layerName = ExpectedLayerNames[index];
                if (LayerMask.NameToLayer(layerName) < 0)
                {
                    issues.Add(new SceneExportIssue(
                        SceneExportIssueSeverity.Warning,
                        "La capa recomendada '" + layerName + "' no existe todavia en el proyecto."));
                }
            }
        }

        private static void ValidateColliders(SceneExportRoot root, List<SceneExportIssue> issues)
        {
            var colliders = root.GetComponentsInChildren<SceneColliderAuthoring>(true);
            for (var index = 0; index < colliders.Length; index++)
            {
                var authoring = colliders[index];
                if (!authoring.Export)
                {
                    continue;
                }

                if (authoring.GetComponent<Collider>() == null)
                {
                    issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "SceneColliderAuthoring requiere un Collider.", authoring));
                    continue;
                }

                if (authoring.GetComponent<MeshCollider>() != null)
                {
                    issues.Add(new SceneExportIssue(
                        SceneExportIssueSeverity.Warning,
                        "MeshCollider se exportara como aproximacion simple; no como colision exacta de cliente.",
                        authoring));
                }

                ValidateLayer(authoring.gameObject, "Scene_Collision", issues);
            }
        }

        private static void ValidateSpawns(SceneExportRoot root, List<SceneExportIssue> issues)
        {
            var spawns = root.GetComponentsInChildren<SceneSpawnPointAuthoring>(true)
                .Where(spawn => spawn.Export)
                .ToArray();

            var duplicateGroups = spawns
                .GroupBy(spawn => ResolveId(spawn.SpawnId, spawn.gameObject.name))
                .Where(group => group.Count() > 1)
                .ToArray();

            foreach (var duplicateGroup in duplicateGroups)
            {
                foreach (var spawn in duplicateGroup)
                {
                    issues.Add(new SceneExportIssue(
                        SceneExportIssueSeverity.Error,
                        "Hay spawn points duplicados con id '" + duplicateGroup.Key + "'.",
                        spawn));
                }
            }

            for (var index = 0; index < spawns.Length; index++)
            {
                ValidateLayer(spawns[index].gameObject, "Scene_Spawn", issues);
            }
        }

        private static void ValidateTransitions(SceneExportRoot root, List<SceneExportIssue> issues)
        {
            var transitions = root.GetComponentsInChildren<SceneTransitionAuthoring>(true)
                .Where(transition => transition.Export)
                .ToArray();

            for (var index = 0; index < transitions.Length; index++)
            {
                var transition = transitions[index];
                if (string.IsNullOrWhiteSpace(transition.TargetSceneId))
                {
                    issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "Una transicion necesita targetSceneId.", transition));
                }

                if (string.IsNullOrWhiteSpace(transition.TargetSpawnId))
                {
                    issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "Una transicion necesita targetSpawnId.", transition));
                }

                var triggerCollider = transition.GetComponent<Collider>();
                if (triggerCollider != null && !triggerCollider.isTrigger)
                {
                    issues.Add(new SceneExportIssue(
                        SceneExportIssueSeverity.Warning,
                        "Se recomienda que el collider de transicion tenga isTrigger activado.",
                        transition));
                }

                ValidateLayer(transition.gameObject, "Scene_Transition", issues);
            }
        }

        private static void ValidateProps(SceneExportRoot root, List<SceneExportIssue> issues)
        {
            var props = root.GetComponentsInChildren<ScenePropAuthoring>(true);
            for (var index = 0; index < props.Length; index++)
            {
                var prop = props[index];
                if (!prop.Export || prop.ExportMode == ScenePropExportMode.Ignore)
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(prop.VisualAssetId))
                {
                    issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "El prop exportable necesita visualAssetId.", prop));
                }

                ValidateLayer(prop.gameObject, "Scene_Visual", issues);
            }
        }

        private static void ValidateLayer(GameObject gameObject, string expectedLayerName, List<SceneExportIssue> issues)
        {
            var expectedLayer = LayerMask.NameToLayer(expectedLayerName);
            if (expectedLayer < 0)
            {
                return;
            }

            if (gameObject.layer != expectedLayer)
            {
                issues.Add(new SceneExportIssue(
                    SceneExportIssueSeverity.Warning,
                    "Se esperaba la capa '" + expectedLayerName + "' en " + gameObject.name + ".",
                    gameObject));
            }
        }

        private static IDictionary BuildSceneDocument(SceneExportRoot root, List<SceneExportIssue> issues)
        {
            var rootTransform = root.transform;
            var colliders = root.GetComponentsInChildren<SceneColliderAuthoring>(true)
                .Where(authoring => authoring.Export)
                .Select(authoring => BuildColliderDocument(rootTransform, authoring, issues))
                .Where(document => document != null)
                .ToList();

            var spawnPoints = root.GetComponentsInChildren<SceneSpawnPointAuthoring>(true)
                .Where(authoring => authoring.Export)
                .Select(authoring => BuildSpawnDocument(rootTransform, authoring))
                .ToList();

            var transitions = root.GetComponentsInChildren<SceneTransitionAuthoring>(true)
                .Where(authoring => authoring.Export)
                .Select(authoring => BuildTransitionDocument(rootTransform, authoring, issues))
                .Where(document => document != null)
                .ToList();

            var props = root.GetComponentsInChildren<ScenePropAuthoring>(true)
                .Where(authoring => authoring.Export && authoring.ExportMode != ScenePropExportMode.Ignore)
                .Select(authoring => BuildPropDocument(rootTransform, authoring))
                .ToList();

            var audioEmitters = root.IncludeAudio
                ? root.GetComponentsInChildren<SceneAudioEmitterAuthoring>(true)
                    .Where(authoring => authoring.Export)
                    .Select(authoring => BuildAudioEmitterDocument(rootTransform, authoring))
                    .Cast<object>()
                    .ToList()
                : new List<object>();

            var lights = root.IncludeLighting
                ? root.GetComponentsInChildren<SceneLightAuthoring>(true)
                    .Where(authoring => authoring.Export)
                    .Select(authoring => BuildLightDocument(rootTransform, authoring))
                    .Cast<object>()
                    .ToList()
                : new List<object>();

            var bounds = ComputeSceneBounds(rootTransform, colliders, spawnPoints, props, transitions);

            var sceneDocument = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sceneId"] = root.SceneId,
                ["displayName"] = root.DisplayName,
                ["builder"] = SceneBuilderId,
                ["coordinateScale"] = Round(root.CoordinateScale),
                ["defaultSceneInstanceKind"] = ToSnakeCase(root.DefaultSceneInstanceKind),
                ["bounds"] = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["width"] = bounds.width,
                    ["depth"] = bounds.depth,
                },
                ["spawnPoints"] = spawnPoints,
                ["transitions"] = transitions,
                ["colliders"] = colliders,
                ["props"] = props,
                ["audioEmitters"] = audioEmitters,
                ["lights"] = lights,
            };

            return sceneDocument;
        }

        private static IDictionary BuildSpawnDocument(Transform rootTransform, SceneSpawnPointAuthoring authoring)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["spawnId"] = ResolveId(authoring.SpawnId, authoring.gameObject.name),
                ["spawnType"] = ToSnakeCase(authoring.SpawnType),
                ["facing"] = ToSnakeCase(authoring.Facing),
                ["position"] = SerializePosition(rootTransform, authoring.transform.position),
            };
        }

        private static IDictionary BuildTransitionDocument(Transform rootTransform, SceneTransitionAuthoring authoring, List<SceneExportIssue> issues)
        {
            var collider = authoring.GetComponent<Collider>();
            if (collider == null)
            {
                issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "La transicion no tiene collider.", authoring));
                return null;
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["transitionId"] = ResolveId(authoring.TransitionId, authoring.gameObject.name),
                ["targetSceneId"] = authoring.TargetSceneId,
                ["targetSpawnId"] = authoring.TargetSpawnId,
                ["instanceMode"] = ToSnakeCase(authoring.InstanceMode),
                ["namedInstanceId"] = string.IsNullOrWhiteSpace(authoring.NamedInstanceId) ? null : authoring.NamedInstanceId,
                ["trigger"] = BuildColliderShapeDocument(rootTransform, collider, authoring.TransitionShape),
            };
        }

        private static IDictionary BuildPropDocument(Transform rootTransform, ScenePropAuthoring authoring)
        {
            var linkedColliderIds = authoring.StaticCollisionSource == SceneStaticCollisionSource.LinkedColliders
                ? authoring.GetComponentsInChildren<SceneColliderAuthoring>(true)
                    .Where(collider => collider.Export)
                    .Select(collider => ResolveId(collider.ColliderId, collider.gameObject.name))
                    .Distinct(StringComparer.Ordinal)
                    .Cast<object>()
                    .ToList()
                : new List<object>();

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["propId"] = ResolveId(authoring.PropId, authoring.gameObject.name),
                ["visualAssetId"] = authoring.VisualAssetId,
                ["exportMode"] = ToSnakeCase(authoring.ExportMode),
                ["staticCollisionSource"] = ToSnakeCase(authoring.StaticCollisionSource),
                ["position"] = SerializePosition(rootTransform, authoring.transform.position),
                ["rotation"] = SerializeRotation(rootTransform, authoring.transform.rotation),
                ["scale"] = SerializeScale(rootTransform, authoring.transform),
                ["linkedColliderIds"] = linkedColliderIds,
            };
        }

        private static IDictionary BuildAudioEmitterDocument(Transform rootTransform, SceneAudioEmitterAuthoring authoring)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["audioEventId"] = authoring.AudioEventId,
                ["position"] = SerializePosition(rootTransform, authoring.transform.position),
                ["radius"] = Round(authoring.Radius),
                ["loop"] = authoring.Loop,
                ["spatial"] = authoring.Spatial,
            };
        }

        private static IDictionary BuildLightDocument(Transform rootTransform, SceneLightAuthoring authoring)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["lightType"] = ToSnakeCase(authoring.LightType),
                ["position"] = SerializePosition(rootTransform, authoring.transform.position),
                ["rotation"] = SerializeRotation(rootTransform, authoring.transform.rotation),
                ["color"] = "#" + ColorUtility.ToHtmlStringRGB(authoring.Color),
                ["intensity"] = Round(authoring.Intensity),
                ["range"] = Round(authoring.Range),
            };
        }

        private static IDictionary BuildColliderDocument(Transform rootTransform, SceneColliderAuthoring authoring, List<SceneExportIssue> issues)
        {
            var collider = authoring.GetComponent<Collider>();
            if (collider == null)
            {
                issues.Add(new SceneExportIssue(SceneExportIssueSeverity.Error, "No se puede exportar un SceneColliderAuthoring sin Collider.", authoring));
                return null;
            }

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["colliderId"] = ResolveId(authoring.ColliderId, authoring.gameObject.name),
                ["colliderKind"] = ToSnakeCase(authoring.ColliderKind),
                ["clientCollision"] = ToSnakeCase(authoring.ClientCollision),
                ["shape"] = BuildColliderShapeDocument(rootTransform, collider, authoring.ShapeOverride),
            };
        }

        private static IDictionary BuildColliderShapeDocument(Transform rootTransform, Collider collider, Enum shapeOverride)
        {
            if (collider is BoxCollider boxCollider)
            {
                var center = rootTransform.InverseTransformPoint(boxCollider.transform.TransformPoint(boxCollider.center));
                var worldSize = Vector3.Scale(boxCollider.size, Abs(boxCollider.transform.lossyScale));
                return new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["type"] = "box",
                    ["center"] = SerializeVector(center),
                    ["size"] = SerializeVector(worldSize),
                };
            }

            if (collider is SphereCollider sphereCollider)
            {
                var center = rootTransform.InverseTransformPoint(sphereCollider.transform.TransformPoint(sphereCollider.center));
                var radiusScale = MaxComponent(Abs(sphereCollider.transform.lossyScale));
                return new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["type"] = "sphere",
                    ["center"] = SerializeVector(center),
                    ["radius"] = Round(sphereCollider.radius * radiusScale),
                };
            }

            if (collider is CapsuleCollider capsuleCollider)
            {
                var center = rootTransform.InverseTransformPoint(capsuleCollider.transform.TransformPoint(capsuleCollider.center));
                var lossyScale = Abs(capsuleCollider.transform.lossyScale);
                var heightAxisScale = capsuleCollider.direction == 0 ? lossyScale.x : capsuleCollider.direction == 1 ? lossyScale.y : lossyScale.z;
                var radiusScale = capsuleCollider.direction == 0
                    ? Math.Max(lossyScale.y, lossyScale.z)
                    : capsuleCollider.direction == 1 ? Math.Max(lossyScale.x, lossyScale.z) : Math.Max(lossyScale.x, lossyScale.y);

                return new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["type"] = "capsule",
                    ["center"] = SerializeVector(center),
                    ["height"] = Round(capsuleCollider.height * heightAxisScale),
                    ["radius"] = Round(capsuleCollider.radius * radiusScale),
                    ["axis"] = capsuleCollider.direction == 0 ? "x" : capsuleCollider.direction == 1 ? "y" : "z",
                };
            }

            var bounds = collider.bounds;
            var localCenter = rootTransform.InverseTransformPoint(bounds.center);
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["type"] = ToSnakeCase(shapeOverride),
                ["center"] = SerializeVector(localCenter),
                ["size"] = SerializeVector(bounds.size),
            };
        }

        private static (double width, double depth) ComputeSceneBounds(
            Transform rootTransform,
            IEnumerable<object> colliders,
            IEnumerable<object> spawnPoints,
            IEnumerable<object> props,
            IEnumerable<object> transitions)
        {
            var minX = double.MaxValue;
            var maxX = double.MinValue;
            var minZ = double.MaxValue;
            var maxZ = double.MinValue;

            void IncludePosition(IDictionary vector)
            {
                if (vector == null)
                {
                    return;
                }

                var x = ToDouble(vector["x"]);
                var z = ToDouble(vector["z"]);
                minX = Math.Min(minX, x);
                maxX = Math.Max(maxX, x);
                minZ = Math.Min(minZ, z);
                maxZ = Math.Max(maxZ, z);
            }

            void IncludeShape(IDictionary shape)
            {
                if (shape == null || !shape.Contains("center"))
                {
                    return;
                }

                var center = (IDictionary)shape["center"];
                if (shape.Contains("size"))
                {
                    var size = (IDictionary)shape["size"];
                    var halfX = ToDouble(size["x"]) * 0.5d;
                    var halfZ = ToDouble(size["z"]) * 0.5d;
                    var cx = ToDouble(center["x"]);
                    var cz = ToDouble(center["z"]);
                    minX = Math.Min(minX, cx - halfX);
                    maxX = Math.Max(maxX, cx + halfX);
                    minZ = Math.Min(minZ, cz - halfZ);
                    maxZ = Math.Max(maxZ, cz + halfZ);
                    return;
                }

                var radius = shape.Contains("radius") ? ToDouble(shape["radius"]) : 0.5d;
                var centerX = ToDouble(center["x"]);
                var centerZ = ToDouble(center["z"]);
                minX = Math.Min(minX, centerX - radius);
                maxX = Math.Max(maxX, centerX + radius);
                minZ = Math.Min(minZ, centerZ - radius);
                maxZ = Math.Max(maxZ, centerZ + radius);
            }

            foreach (IDictionary collider in colliders)
            {
                IncludeShape((IDictionary)collider["shape"]);
            }

            foreach (IDictionary spawn in spawnPoints)
            {
                IncludePosition((IDictionary)spawn["position"]);
            }

            foreach (IDictionary prop in props)
            {
                IncludePosition((IDictionary)prop["position"]);
            }

            foreach (IDictionary transition in transitions)
            {
                IncludeShape((IDictionary)transition["trigger"]);
            }

            if (minX == double.MaxValue || minZ == double.MaxValue)
            {
                var rootPosition = rootTransform.position;
                minX = rootPosition.x - 1d;
                maxX = rootPosition.x + 1d;
                minZ = rootPosition.z - 1d;
                maxZ = rootPosition.z + 1d;
            }

            return (Round(maxX - minX), Round(maxZ - minZ));
        }

        private static void UpsertSceneDefinition(string sceneDefinitionsPath, string sceneId, string sceneDataFileId)
        {
            IDictionary rootObject = LoadJsonObject(sceneDefinitionsPath);
            var scenes = GetOrCreateObject(rootObject, "scenes");
            var sceneDefinition = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sceneId"] = sceneId,
                ["builder"] = SceneBuilderId,
                ["sceneDataFileId"] = sceneDataFileId,
            };

            scenes[sceneId] = sceneDefinition;
            SaveJson(sceneDefinitionsPath, rootObject);
        }

        private static void UpsertManifestSceneFile(
            string manifestPath,
            string version,
            string sceneDataFileId,
            string sceneDataUrl,
            string sceneDataPath)
        {
            IDictionary rootObject = LoadJsonObject(manifestPath);
            IList files = GetOrCreateArray(rootObject, "files");
            IDictionary existingEntry = null;

            for (var index = 0; index < files.Count; index++)
            {
                if (files[index] is IDictionary dictionary &&
                    string.Equals(dictionary["id"] as string, sceneDataFileId, StringComparison.Ordinal))
                {
                    existingEntry = dictionary;
                    break;
                }
            }

            if (existingEntry == null)
            {
                existingEntry = new Dictionary<string, object>(StringComparer.Ordinal);
                files.Add(existingEntry);
            }

            existingEntry["id"] = sceneDataFileId;
            existingEntry["type"] = "scene";
            existingEntry["url"] = sceneDataUrl;
            existingEntry["hash"] = ComputeSha256(sceneDataPath);
            existingEntry["size"] = new FileInfo(sceneDataPath).Length;

            if (!rootObject.Contains("sceneId") || string.IsNullOrWhiteSpace(rootObject["sceneId"] as string))
            {
                rootObject["sceneId"] = sceneDataFileId.StartsWith("scene.", StringComparison.Ordinal)
                    ? sceneDataFileId.Substring("scene.".Length)
                    : sceneDataFileId;
            }

            if (!rootObject.Contains("version") || string.IsNullOrWhiteSpace(rootObject["version"] as string))
            {
                rootObject["version"] = version;
            }

            SaveJson(manifestPath, rootObject);
        }

        private static IDictionary LoadJsonObject(string path)
        {
            if (!File.Exists(path))
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            var parsed = SceneExportJson.Deserialize(File.ReadAllText(path)) as IDictionary;
            if (parsed == null)
            {
                return new Dictionary<string, object>(StringComparer.Ordinal);
            }

            return ConvertDictionary(parsed);
        }

        private static Dictionary<string, object> ConvertDictionary(IDictionary source)
        {
            var target = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (DictionaryEntry entry in source)
            {
                target[entry.Key.ToString()] = NormalizeJsonValue(entry.Value);
            }

            return target;
        }

        private static IList<object> ConvertList(IList source)
        {
            var target = new List<object>(source.Count);
            for (var index = 0; index < source.Count; index++)
            {
                target.Add(NormalizeJsonValue(source[index]));
            }

            return target;
        }

        private static object NormalizeJsonValue(object value)
        {
            if (value is IDictionary dictionary)
            {
                return ConvertDictionary(dictionary);
            }

            if (value is IList list)
            {
                return ConvertList(list);
            }

            return value;
        }

        private static Dictionary<string, object> GetOrCreateObject(IDictionary rootObject, string propertyName)
        {
            if (rootObject[propertyName] is Dictionary<string, object> dictionary)
            {
                return dictionary;
            }

            if (rootObject[propertyName] is IDictionary genericDictionary)
            {
                var converted = ConvertDictionary(genericDictionary);
                rootObject[propertyName] = converted;
                return converted;
            }

            var created = new Dictionary<string, object>(StringComparer.Ordinal);
            rootObject[propertyName] = created;
            return created;
        }

        private static IList GetOrCreateArray(IDictionary rootObject, string propertyName)
        {
            if (rootObject[propertyName] is IList existingArray)
            {
                return existingArray;
            }

            var created = new List<object>();
            rootObject[propertyName] = created;
            return created;
        }

        private static void SaveJson(string path, IDictionary data)
        {
            var json = SceneExportJson.Serialize(data);
            File.WriteAllText(path, json, new UTF8Encoding(false));
        }

        private static string ResolveRepositoryRoot()
        {
            var current = new DirectoryInfo(Application.dataPath);

            while (current != null)
            {
                var contentPacksCandidate = Path.Combine(current.FullName, "content-packs");
                if (Directory.Exists(contentPacksCandidate))
                {
                    return current.FullName;
                }

                current = current.Parent;
            }

            throw new DirectoryNotFoundException("No se pudo localizar la carpeta content-packs desde el proyecto Unity.");
        }

        private static string ComputeSha256(string path)
        {
            using (var sha256 = SHA256.Create())
            using (var fileStream = File.OpenRead(path))
            {
                var hash = sha256.ComputeHash(fileStream);
                var builder = new StringBuilder(hash.Length * 2 + 7);
                builder.Append("sha256-");

                for (var index = 0; index < hash.Length; index++)
                {
                    builder.Append(hash[index].ToString("x2", CultureInfo.InvariantCulture));
                }

                return builder.ToString();
            }
        }

        private static IDictionary SerializePosition(Transform rootTransform, Vector3 worldPosition)
        {
            var localPosition = rootTransform.InverseTransformPoint(worldPosition);
            return SerializeVector(localPosition);
        }

        private static IDictionary SerializeRotation(Transform rootTransform, Quaternion worldRotation)
        {
            var localRotation = Quaternion.Inverse(rootTransform.rotation) * worldRotation;
            var euler = localRotation.eulerAngles;
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["x"] = Round(euler.x),
                ["y"] = Round(euler.y),
                ["z"] = Round(euler.z),
            };
        }

        private static IDictionary SerializeScale(Transform rootTransform, Transform transform)
        {
            var rootScale = Abs(rootTransform.lossyScale);
            var localScale = Abs(transform.lossyScale);

            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["x"] = Round(rootScale.x == 0f ? localScale.x : localScale.x / rootScale.x),
                ["y"] = Round(rootScale.y == 0f ? localScale.y : localScale.y / rootScale.y),
                ["z"] = Round(rootScale.z == 0f ? localScale.z : localScale.z / rootScale.z),
            };
        }

        private static IDictionary SerializeVector(Vector3 vector)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["x"] = Round(vector.x),
                ["y"] = Round(vector.y),
                ["z"] = Round(vector.z),
            };
        }

        private static string ResolveId(string configuredValue, string fallbackName)
        {
            if (!string.IsNullOrWhiteSpace(configuredValue))
            {
                return configuredValue.Trim();
            }

            return Slugify(fallbackName);
        }

        private static string Slugify(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "unnamed";
            }

            var builder = new StringBuilder(value.Length);
            var lowerInvariant = value.Trim().ToLowerInvariant();

            for (var index = 0; index < lowerInvariant.Length; index++)
            {
                var current = lowerInvariant[index];
                if (char.IsLetterOrDigit(current))
                {
                    builder.Append(current);
                    continue;
                }

                if (current == '.' || current == '_' || current == '-')
                {
                    builder.Append(current);
                    continue;
                }

                if (builder.Length == 0 || builder[builder.Length - 1] == '-')
                {
                    continue;
                }

                builder.Append('-');
            }

            return builder.ToString().Trim('-');
        }

        private static string ToSnakeCase(Enum value)
        {
            var raw = value.ToString();
            var builder = new StringBuilder(raw.Length + 4);

            for (var index = 0; index < raw.Length; index++)
            {
                var current = raw[index];
                if (char.IsUpper(current) && index > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(current));
            }

            return builder.ToString();
        }

        private static Vector3 Abs(Vector3 value)
        {
            return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        }

        private static float MaxComponent(Vector3 value)
        {
            return Mathf.Max(value.x, Mathf.Max(value.y, value.z));
        }

        private static double Round(double value)
        {
            return Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }

        private static double ToDouble(object value)
        {
            switch (value)
            {
                case long longValue:
                    return longValue;
                case int intValue:
                    return intValue;
                case float floatValue:
                    return floatValue;
                case double doubleValue:
                    return doubleValue;
                case decimal decimalValue:
                    return (double)decimalValue;
                default:
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
        }
    }
}
