using IslaTortuga.Unity.SceneExport.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IslaTortuga.Unity.SceneExport.Editor
{
    internal static class SceneExportSampleSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/SceneExportSample.unity";
        private const int LayerSceneVisual = 6;
        private const int LayerSceneCollision = 7;
        private const int LayerSceneTrigger = 8;
        private const int LayerSceneSpawn = 9;
        private const int LayerSceneTransition = 10;
        private const int LayerSceneAudio = 11;
        private const int LayerSceneIgnore = 12;

        [MenuItem("Isla Tortuga/Scene Export/Create Sample Authoring Scene")]
        private static void CreateSampleScene()
        {
            EnsureRecommendedLayers();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            scene.name = "SceneExportSample";

            var root = new GameObject("SCN_scene.test.plain");
            var rootTransform = root.transform;
            var exportRoot = root.AddComponent<SceneExportRoot>();
            exportRoot.SceneId = "scene.test.plain";
            exportRoot.DisplayName = "Test Plain";
            exportRoot.DefaultSceneInstanceKind = DefaultSceneInstanceKind.Shared;
            exportRoot.IncludeLighting = true;
            exportRoot.IncludeAudio = false;

            var visualRoot = CreateChild(rootTransform, "_Visual");
            var collisionRoot = CreateChild(rootTransform, "_Collision");
            var spawnRoot = CreateChild(rootTransform, "_Spawn");
            var transitionRoot = CreateChild(rootTransform, "_Transitions");
            var physicsRoot = CreateChild(rootTransform, "_PhysicsDynamic");
            var lightingRoot = CreateChild(rootTransform, "_Lighting");

            CreateGround(visualRoot.transform, collisionRoot.transform);
            CreateSpawnPoint(spawnRoot.transform);
            CreateStaticObstacle(visualRoot.transform, collisionRoot.transform, "01", new Vector3(-3f, 0.75f, 2.5f), new Vector3(2f, 1.5f, 2f), "prop.obstacle.box_01");
            CreateStaticObstacle(visualRoot.transform, collisionRoot.transform, "02", new Vector3(3.5f, 0.75f, -1.5f), new Vector3(1.5f, 1.5f, 3f), "prop.obstacle.box_02");
            CreateTree(visualRoot.transform, collisionRoot.transform, "01", new Vector3(-6f, 0f, -5f));
            CreateTree(visualRoot.transform, collisionRoot.transform, "02", new Vector3(6f, 0f, 5f));
            CreateDynamicBox(physicsRoot.transform, "01", new Vector3(-1.5f, 2.5f, -4f));
            CreateDynamicBox(physicsRoot.transform, "02", new Vector3(1.5f, 3.5f, -4.5f));
            CreateDynamicSphere(physicsRoot.transform, "01", new Vector3(0f, 4f, 3.5f));
            CreateDirectionalLight(lightingRoot.transform);
            CreateExampleTransition(transitionRoot.transform);

            EditorSceneManager.SaveScene(scene, ScenePath);
            Selection.activeGameObject = root;
            EditorGUIUtility.PingObject(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Escena de ejemplo creada en " + ScenePath + ".");
        }

        private static void EnsureRecommendedLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layersProperty = tagManager.FindProperty("layers");
            EnsureLayer(layersProperty, LayerSceneVisual, "Scene_Visual");
            EnsureLayer(layersProperty, LayerSceneCollision, "Scene_Collision");
            EnsureLayer(layersProperty, LayerSceneTrigger, "Scene_Trigger");
            EnsureLayer(layersProperty, LayerSceneSpawn, "Scene_Spawn");
            EnsureLayer(layersProperty, LayerSceneTransition, "Scene_Transition");
            EnsureLayer(layersProperty, LayerSceneAudio, "Scene_Audio");
            EnsureLayer(layersProperty, LayerSceneIgnore, "Scene_Ignore");
            tagManager.ApplyModifiedProperties();
        }

        private static void EnsureLayer(SerializedProperty layersProperty, int index, string name)
        {
            if (index < 0 || index >= layersProperty.arraySize)
            {
                return;
            }

            var element = layersProperty.GetArrayElementAtIndex(index);
            if (string.IsNullOrWhiteSpace(element.stringValue) || element.stringValue == name)
            {
                element.stringValue = name;
                return;
            }

            Debug.LogWarning("La capa " + index + " ya esta ocupada por '" + element.stringValue + "'. Se mantiene ese valor.");
        }

        private static GameObject CreateChild(Transform parent, string name)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            return gameObject;
        }

        private static void CreateGround(Transform visualRoot, Transform collisionRoot)
        {
            var groundVisual = GameObject.CreatePrimitive(PrimitiveType.Plane);
            groundVisual.name = "ENV_Ground";
            groundVisual.layer = LayerSceneVisual;
            groundVisual.transform.SetParent(visualRoot, false);
            groundVisual.transform.localScale = new Vector3(3f, 1f, 3f);
            var groundRenderer = groundVisual.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                groundRenderer.sharedMaterial.color = new Color(0.42f, 0.64f, 0.36f, 1f);
            }

            Object.DestroyImmediate(groundVisual.GetComponent<Collider>());

            var groundCollision = new GameObject("COL_Ground");
            groundCollision.layer = LayerSceneCollision;
            groundCollision.transform.SetParent(collisionRoot, false);
            groundCollision.transform.position = Vector3.zero;
            var boxCollider = groundCollision.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(30f, 1f, 30f);
            boxCollider.center = new Vector3(0f, -0.5f, 0f);
            var authoring = groundCollision.AddComponent<SceneColliderAuthoring>();
            authoring.ColliderId = "ground_main";
            authoring.ColliderKind = SceneColliderKind.Blocking;
            authoring.ClientCollision = SceneClientCollisionMode.Simple;
            authoring.ShapeOverride = SceneColliderShapeOverride.Box;
        }

        private static void CreateSpawnPoint(Transform spawnRoot)
        {
            var spawn = CreateChild(spawnRoot, "SPAWN_player_default_main");
            spawn.layer = LayerSceneSpawn;
            spawn.transform.localPosition = new Vector3(0f, 0f, -8f);
            var authoring = spawn.AddComponent<SceneSpawnPointAuthoring>();
            authoring.SpawnId = "player_default_main";
            authoring.SpawnType = SceneSpawnType.PlayerDefault;
            authoring.Facing = SceneFacingDirection.Up;
        }

        private static void CreateStaticObstacle(
            Transform visualRoot,
            Transform collisionRoot,
            string suffix,
            Vector3 position,
            Vector3 size,
            string visualAssetId)
        {
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "PROP_obstacle_box_" + suffix;
            visual.layer = LayerSceneVisual;
            visual.transform.SetParent(visualRoot, false);
            visual.transform.localPosition = position;
            visual.transform.localScale = size;
            var renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = new Color(0.66f, 0.46f, 0.3f, 1f);
            }

            Object.DestroyImmediate(visual.GetComponent<Collider>());

            var prop = visual.AddComponent<ScenePropAuthoring>();
            prop.PropId = "obstacle_box_" + suffix;
            prop.VisualAssetId = visualAssetId;
            prop.ExportMode = ScenePropExportMode.StaticMesh;
            prop.StaticCollisionSource = SceneStaticCollisionSource.LinkedColliders;

            var collision = new GameObject("COL_obstacle_box_" + suffix);
            collision.layer = LayerSceneCollision;
            collision.transform.SetParent(collisionRoot, false);
            collision.transform.localPosition = position;
            var boxCollider = collision.AddComponent<BoxCollider>();
            boxCollider.size = size;
            var colliderAuthoring = collision.AddComponent<SceneColliderAuthoring>();
            colliderAuthoring.ColliderId = "obstacle_box_" + suffix + "_col";
            colliderAuthoring.ColliderKind = SceneColliderKind.Blocking;
            colliderAuthoring.ClientCollision = SceneClientCollisionMode.Simple;
            colliderAuthoring.ShapeOverride = SceneColliderShapeOverride.Box;
        }

        private static void CreateTree(Transform visualRoot, Transform collisionRoot, string suffix, Vector3 position)
        {
            var treeRoot = CreateChild(visualRoot, "PROP_tree_oak_" + suffix);
            treeRoot.layer = LayerSceneVisual;
            treeRoot.transform.localPosition = position;

            var prop = treeRoot.AddComponent<ScenePropAuthoring>();
            prop.PropId = "tree_oak_" + suffix;
            prop.VisualAssetId = "prop.tree.oak_01";
            prop.ExportMode = ScenePropExportMode.StaticMesh;
            prop.StaticCollisionSource = SceneStaticCollisionSource.LinkedColliders;

            var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.name = "Trunk";
            trunk.layer = LayerSceneVisual;
            trunk.transform.SetParent(treeRoot.transform, false);
            trunk.transform.localScale = new Vector3(0.45f, 1.5f, 0.45f);
            trunk.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            var trunkRenderer = trunk.GetComponent<Renderer>();
            if (trunkRenderer != null)
            {
                trunkRenderer.sharedMaterial.color = new Color(0.42f, 0.27f, 0.16f, 1f);
            }

            var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            crown.name = "Crown";
            crown.layer = LayerSceneVisual;
            crown.transform.SetParent(treeRoot.transform, false);
            crown.transform.localScale = new Vector3(2.4f, 2.2f, 2.4f);
            crown.transform.localPosition = new Vector3(0f, 3.8f, 0f);
            var crownRenderer = crown.GetComponent<Renderer>();
            if (crownRenderer != null)
            {
                crownRenderer.sharedMaterial.color = new Color(0.18f, 0.52f, 0.23f, 1f);
            }

            Object.DestroyImmediate(trunk.GetComponent<Collider>());
            Object.DestroyImmediate(crown.GetComponent<Collider>());

            var collision = new GameObject("COL_tree_oak_" + suffix);
            collision.layer = LayerSceneCollision;
            collision.transform.SetParent(collisionRoot, false);
            collision.transform.localPosition = position + new Vector3(0f, 1.1f, 0f);
            var capsuleCollider = collision.AddComponent<CapsuleCollider>();
            capsuleCollider.radius = 0.55f;
            capsuleCollider.height = 2.2f;
            capsuleCollider.direction = 1;
            var colliderAuthoring = collision.AddComponent<SceneColliderAuthoring>();
            colliderAuthoring.ColliderId = "tree_oak_" + suffix + "_col";
            colliderAuthoring.ColliderKind = SceneColliderKind.Blocking;
            colliderAuthoring.ClientCollision = SceneClientCollisionMode.Simple;
            colliderAuthoring.ShapeOverride = SceneColliderShapeOverride.Capsule;
        }

        private static void CreateDynamicBox(Transform physicsRoot, string suffix, Vector3 position)
        {
            var dynamicBox = GameObject.CreatePrimitive(PrimitiveType.Cube);
            dynamicBox.name = "DYN_box_" + suffix;
            dynamicBox.transform.SetParent(physicsRoot, false);
            dynamicBox.transform.localPosition = position;
            dynamicBox.transform.localScale = new Vector3(1f, 1f, 1f);
            var rigidbody = dynamicBox.AddComponent<Rigidbody>();
            rigidbody.mass = 1.5f;
            var renderer = dynamicBox.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = new Color(0.82f, 0.74f, 0.48f, 1f);
            }
        }

        private static void CreateDynamicSphere(Transform physicsRoot, string suffix, Vector3 position)
        {
            var dynamicSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dynamicSphere.name = "DYN_sphere_" + suffix;
            dynamicSphere.transform.SetParent(physicsRoot, false);
            dynamicSphere.transform.localPosition = position;
            dynamicSphere.transform.localScale = new Vector3(1.2f, 1.2f, 1.2f);
            var rigidbody = dynamicSphere.AddComponent<Rigidbody>();
            rigidbody.mass = 1f;
            var renderer = dynamicSphere.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial.color = new Color(0.44f, 0.68f, 0.86f, 1f);
            }
        }

        private static void CreateDirectionalLight(Transform lightingRoot)
        {
            var lightObject = new GameObject("LGT_sun_main");
            lightObject.layer = 0;
            lightObject.transform.SetParent(lightingRoot, false);
            lightObject.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.88f, 1f);
            light.intensity = 1.1f;

            var authoring = lightObject.AddComponent<SceneLightAuthoring>();
            authoring.LightType = SceneLightType.Directional;
            authoring.Color = light.color;
            authoring.Intensity = light.intensity;
            authoring.Range = 100f;
        }

        private static void CreateExampleTransition(Transform transitionRoot)
        {
            var transition = new GameObject("TRN_scene.house.small_frontdoor");
            transition.layer = LayerSceneTransition;
            transition.transform.SetParent(transitionRoot, false);
            transition.transform.localPosition = new Vector3(0f, 1f, 11f);
            var collider = transition.AddComponent<BoxCollider>();
            collider.isTrigger = true;
            collider.size = new Vector3(3f, 2.5f, 1.5f);

            var authoring = transition.AddComponent<SceneTransitionAuthoring>();
            authoring.TransitionId = "frontdoor";
            authoring.TargetSceneId = "scene.house.small";
            authoring.TargetSpawnId = "player_interior_entry_main";
            authoring.InstanceMode = SceneTransitionInstanceMode.Shared;
            authoring.TransitionShape = SceneTransitionShape.Box;
        }
    }
}
