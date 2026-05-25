using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace IslaTortuga.Server.Core.World.Tiled
{
    public sealed class TiledWorldBuilder
    {
        public TiledWorldMap BuildFromFile(string mapPath)
        {
            if (string.IsNullOrWhiteSpace(mapPath) || !File.Exists(mapPath))
            {
                return new TiledWorldMap(
                    mapPath ?? string.Empty,
                    "missing-map",
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<TiledLayerData>(),
                    Array.Empty<TiledTilesetData>());
            }

            var json = File.ReadAllText(mapPath);
            var map = JsonUtility.FromJson<TiledMapJson>(json);

            if (map == null)
            {
                return new TiledWorldMap(
                    mapPath,
                    Path.GetFileNameWithoutExtension(mapPath),
                    0,
                    0,
                    0,
                    0,
                    Array.Empty<TiledLayerData>(),
                    Array.Empty<TiledTilesetData>());
            }

            var layers = (map.layers ?? Array.Empty<TiledLayerJson>())
                .Select(ConvertLayer)
                .ToArray();

            var tilesets = (map.tilesets ?? Array.Empty<TiledTilesetJson>())
                .Select(ConvertTileset)
                .ToArray();

            return new TiledWorldMap(
                mapPath,
                string.IsNullOrWhiteSpace(map.name) ? Path.GetFileNameWithoutExtension(mapPath) : map.name,
                map.width,
                map.height,
                map.tilewidth,
                map.tileheight,
                layers,
                tilesets);
        }

        private static TiledLayerData ConvertLayer(TiledLayerJson layer)
        {
            return new TiledLayerData(
                layer.id,
                layer.name ?? string.Empty,
                layer.type ?? string.Empty,
                layer.@class,
                layer.visible,
                layer.data ?? Array.Empty<int>(),
                (layer.objects ?? Array.Empty<TiledObjectJson>()).Select(ConvertObject).ToArray());
        }

        private static TiledObjectData ConvertObject(TiledObjectJson obj)
        {
            return new TiledObjectData(
                obj.id,
                obj.name ?? string.Empty,
                obj.type,
                obj.@class,
                obj.x,
                obj.y,
                obj.width,
                obj.height);
        }

        private static TiledTilesetData ConvertTileset(TiledTilesetJson tileset)
        {
            return new TiledTilesetData(
                tileset.name ?? string.Empty,
                tileset.firstgid,
                tileset.tilewidth,
                tileset.tileheight);
        }

        [Serializable]
        private sealed class TiledMapJson
        {
            public string name;
            public int width;
            public int height;
            public int tilewidth;
            public int tileheight;
            public TiledLayerJson[] layers;
            public TiledTilesetJson[] tilesets;
        }

        [Serializable]
        private sealed class TiledLayerJson
        {
            public int id;
            public string name;
            public string type;
            public string @class;
            public bool visible = true;
            public int[] data;
            public TiledObjectJson[] objects;
        }

        [Serializable]
        private sealed class TiledObjectJson
        {
            public int id;
            public string name;
            public string type;
            public string @class;
            public float x;
            public float y;
            public float width;
            public float height;
        }

        [Serializable]
        private sealed class TiledTilesetJson
        {
            public string name;
            public int firstgid;
            public int tilewidth;
            public int tileheight;
        }
    }

    public sealed class TiledWorldMap
    {
        public TiledWorldMap(
            string sourcePath,
            string name,
            int width,
            int height,
            int tileWidth,
            int tileHeight,
            IReadOnlyList<TiledLayerData> layers,
            IReadOnlyList<TiledTilesetData> tilesets)
        {
            SourcePath = sourcePath;
            Name = name;
            Width = width;
            Height = height;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
            Layers = layers;
            Tilesets = tilesets;
        }

        public string SourcePath { get; }

        public string Name { get; }

        public int Width { get; }

        public int Height { get; }

        public int TileWidth { get; }

        public int TileHeight { get; }

        public IReadOnlyList<TiledLayerData> Layers { get; }

        public IReadOnlyList<TiledTilesetData> Tilesets { get; }

        public TiledLayerData GetLayer(string name)
        {
            return Layers.FirstOrDefault(layer => string.Equals(layer.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyList<TiledObjectData> GetSpawnPoints()
        {
            var spawnLayer = GetLayer("SpawnPoints");
            if (spawnLayer == null)
            {
                return Array.Empty<TiledObjectData>();
            }

            return spawnLayer.Objects
                .Where(obj =>
                    string.Equals(obj.Class, "PlayerSpawn", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(obj.Type, "PlayerSpawn", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(spawnLayer.Name, "SpawnPoints", StringComparison.OrdinalIgnoreCase))
                .ToArray();
        }
    }

    public sealed class TiledLayerData
    {
        public TiledLayerData(
            int id,
            string name,
            string type,
            string className,
            bool visible,
            IReadOnlyList<int> tileData,
            IReadOnlyList<TiledObjectData> objects)
        {
            Id = id;
            Name = name;
            Type = type;
            Class = className;
            Visible = visible;
            TileData = tileData;
            Objects = objects;
        }

        public int Id { get; }

        public string Name { get; }

        public string Type { get; }

        public string Class { get; }

        public bool Visible { get; }

        public IReadOnlyList<int> TileData { get; }

        public IReadOnlyList<TiledObjectData> Objects { get; }
    }

    public sealed class TiledObjectData
    {
        public TiledObjectData(
            int id,
            string name,
            string type,
            string className,
            float x,
            float y,
            float width,
            float height)
        {
            Id = id;
            Name = name;
            Type = type;
            Class = className;
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        public int Id { get; }

        public string Name { get; }

        public string Type { get; }

        public string Class { get; }

        public float X { get; }

        public float Y { get; }

        public float Width { get; }

        public float Height { get; }
    }

    public sealed class TiledTilesetData
    {
        public TiledTilesetData(
            string name,
            int firstGlobalId,
            int tileWidth,
            int tileHeight)
        {
            Name = name;
            FirstGlobalId = firstGlobalId;
            TileWidth = tileWidth;
            TileHeight = tileHeight;
        }

        public string Name { get; }

        public int FirstGlobalId { get; }

        public int TileWidth { get; }

        public int TileHeight { get; }
    }
}
