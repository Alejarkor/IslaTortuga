using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IslaTortuga.Server.Core.World.Scenes
{
    public sealed class SceneTemplateBuilder
    {
        public SceneTemplateData BuildFromFile(string scenePath)
        {
            var fallbackSceneId = string.IsNullOrWhiteSpace(scenePath)
                ? "scene.missing"
                : Path.GetFileNameWithoutExtension(scenePath);

            if (string.IsNullOrWhiteSpace(scenePath) || !File.Exists(scenePath))
            {
                return new SceneTemplateData(
                    scenePath ?? string.Empty,
                    fallbackSceneId,
                    fallbackSceneId,
                    30f,
                    30f,
                    Array.Empty<SceneSpawnPointData>());
            }

            var json = File.ReadAllText(scenePath);
            var scene = JsonUtility.FromJson<SceneTemplateJson>(json);
            if (scene == null)
            {
                return new SceneTemplateData(
                    scenePath,
                    fallbackSceneId,
                    fallbackSceneId,
                    30f,
                    30f,
                    Array.Empty<SceneSpawnPointData>());
            }

            var sceneId = string.IsNullOrWhiteSpace(scene.sceneId) ? fallbackSceneId : scene.sceneId;
            var displayName = string.IsNullOrWhiteSpace(scene.displayName) ? sceneId : scene.displayName;
            var boundsWidth = Mathf.Max(scene.bounds?.width ?? 30f, 1f);
            var boundsDepth = Mathf.Max(scene.bounds?.depth ?? 30f, 1f);
            var spawnPoints = ConvertSpawnPoints(scene.spawnPoints);

            return new SceneTemplateData(
                scenePath,
                sceneId,
                displayName,
                boundsWidth,
                boundsDepth,
                spawnPoints);
        }

        private static IReadOnlyList<SceneSpawnPointData> ConvertSpawnPoints(SceneSpawnPointJson[] spawnPoints)
        {
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return Array.Empty<SceneSpawnPointData>();
            }

            var converted = new List<SceneSpawnPointData>(spawnPoints.Length);
            for (var index = 0; index < spawnPoints.Length; index++)
            {
                var spawnPoint = spawnPoints[index];
                if (spawnPoint == null)
                {
                    continue;
                }

                var spawnId = string.IsNullOrWhiteSpace(spawnPoint.spawnId)
                    ? "spawn." + index
                    : spawnPoint.spawnId;

                converted.Add(new SceneSpawnPointData(
                    spawnId,
                    spawnPoint.spawnType ?? string.Empty,
                    spawnPoint.facing ?? string.Empty,
                    spawnPoint.position?.x ?? 0f,
                    spawnPoint.position?.y ?? 0f,
                    spawnPoint.position?.z ?? 0f));
            }

            return converted;
        }

        [Serializable]
        private sealed class SceneTemplateJson
        {
            public string sceneId;
            public string displayName;
            public BoundsJson bounds;
            public SceneSpawnPointJson[] spawnPoints;
        }

        [Serializable]
        private sealed class BoundsJson
        {
            public float width;
            public float depth;
        }

        [Serializable]
        private sealed class SceneSpawnPointJson
        {
            public string spawnId;
            public string spawnType;
            public string facing;
            public Vector3Json position;
        }

        [Serializable]
        private sealed class Vector3Json
        {
            public float x;
            public float y;
            public float z;
        }
    }

    public sealed class SceneTemplateData
    {
        public SceneTemplateData(
            string sourcePath,
            string sceneId,
            string displayName,
            float boundsWidth,
            float boundsDepth,
            IReadOnlyList<SceneSpawnPointData> spawnPoints)
        {
            SourcePath = sourcePath;
            SceneId = sceneId;
            DisplayName = displayName;
            BoundsWidth = boundsWidth;
            BoundsDepth = boundsDepth;
            SpawnPoints = spawnPoints;
        }

        public string SourcePath { get; }

        public string SceneId { get; }

        public string DisplayName { get; }

        public float BoundsWidth { get; }

        public float BoundsDepth { get; }

        public IReadOnlyList<SceneSpawnPointData> SpawnPoints { get; }
    }

    public sealed class SceneSpawnPointData
    {
        public SceneSpawnPointData(
            string spawnId,
            string spawnType,
            string facing,
            float x,
            float y,
            float z)
        {
            SpawnId = spawnId;
            SpawnType = spawnType;
            Facing = facing;
            X = x;
            Y = y;
            Z = z;
        }

        public string SpawnId { get; }

        public string SpawnType { get; }

        public string Facing { get; }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }
    }
}
