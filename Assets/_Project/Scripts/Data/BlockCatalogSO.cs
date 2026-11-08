using System.Collections.Generic;
using UnityEngine;

namespace SmashFast.Data
{
    [CreateAssetMenu(fileName = "BlockCatalog", menuName = "SmashFast/Block Catalog", order = 1)]
    public class BlockCatalogSO : ScriptableObject
    {
        [SerializeField] private List<BlockDefinition> blocks = new List<BlockDefinition>(8);

        [System.NonSerialized] private Dictionary<int, int> idToIndex;
        [System.NonSerialized] private BlockFootprint blockFootprint;
        [System.NonSerialized] private int cachedBlockCount = -1;

        private BlockFootprint Footprint
        {
            get
            {
                if (blockFootprint == null)
                {
                    blockFootprint = new BlockFootprint();
                }

                return blockFootprint;
            }
        }

        public int BlockCount
        {
            get
            {
                if (blocks == null)
                {
                    return 0;
                }

                return blocks.Count;
            }
        }

        private void OnEnable()
        {
            if (blocks == null)
            {
                blocks = new List<BlockDefinition>(8);
            }

            idToIndex = null;
            cachedBlockCount = -1;
        }

        private void OnValidate()
        {
            SanitizeBlocks();
            idToIndex = null;
            cachedBlockCount = -1;
        }

        public bool TryGetBlockByIndex(int index, out BlockDefinition definition)
        {
            if (blocks == null || index < 0 || index >= blocks.Count)
            {
                definition = default;
                return false;
            }

            definition = blocks[index];
            return true;
        }

        public bool TryGetBlockDefinition(int blockId, out BlockDefinition definition)
        {
            EnsureCache();
            if (idToIndex != null && idToIndex.TryGetValue(blockId, out int index))
            {
                definition = blocks[index];
                return true;
            }

            definition = default;
            return false;
        }

        public bool TryGetFirstByMaterialAndSize(BlockMaterialType materialType, Vector3Int sizeInCells, out BlockDefinition definition)
        {
            if (blocks == null)
            {
                definition = default;
                return false;
            }

            Vector3Int targetSize = Footprint.ClampSize(sizeInCells);
            int count = blocks.Count;
            for (int i = 0; i < count; i++)
            {
                BlockDefinition candidate = blocks[i];
                if (candidate.materialType != materialType)
                {
                    continue;
                }

                if (candidate.prefab == null)
                {
                    continue;
                }

                if (Footprint.ClampSize(candidate.sizeInCells) != targetSize)
                {
                    continue;
                }

                definition = candidate;
                return true;
            }

            definition = default;
            return false;
        }

        private void EnsureCache()
        {
            if (blocks == null)
            {
                blocks = new List<BlockDefinition>(8);
            }

            if (idToIndex != null && cachedBlockCount == blocks.Count)
            {
                return;
            }

            RebuildCache();
        }

        private void RebuildCache()
        {
            int count = blocks.Count;
            if (idToIndex == null)
            {
                idToIndex = new Dictionary<int, int>(count);
            }
            else
            {
                idToIndex.Clear();
            }

            for (int i = 0; i < count; i++)
            {
                int id = blocks[i].blockId;
                if (idToIndex.ContainsKey(id))
                {
                    continue;
                }

                idToIndex.Add(id, i);
            }

            cachedBlockCount = count;
        }

        private void SanitizeBlocks()
        {
            if (blocks == null)
            {
                blocks = new List<BlockDefinition>(8);
                return;
            }

            for (int i = 0; i < blocks.Count; i++)
            {
                BlockDefinition definition = blocks[i];
                bool changed = false;

                if (string.IsNullOrEmpty(definition.blockName))
                {
                    definition.blockName = "Block " + definition.blockId.ToString();
                    changed = true;
                }

                if (definition.baseScale == Vector3.zero)
                {
                    definition.baseScale = Vector3.one;
                    changed = true;
                }

                Vector3Int clampedSize = Footprint.ClampSize(definition.sizeInCells);
                if (definition.sizeInCells != clampedSize)
                {
                    definition.sizeInCells = clampedSize;
                    changed = true;
                }

                if (definition.editorColor.a <= 0f)
                {
                    Color color = definition.editorColor;
                    if (color.maxColorComponent <= 0f)
                    {
                        color = new Color(0.25f, 0.8f, 0.95f, 1f);
                    }
                    else
                    {
                        color.a = 1f;
                    }

                    definition.editorColor = color;
                    changed = true;
                }

                if (definition.mass <= 0f)
                {
                    definition.mass = 1f;
                    changed = true;
                }

                if (definition.impactBreakThreshold <= 0f)
                {
                    definition.impactBreakThreshold = 3.5f;
                    changed = true;
                    if (definition.shapeRole != BlockShapeRole.TopOrnament && definition.shapeRole != BlockShapeRole.FragileProp)
                    {
                        definition.canSupportBlocksAbove = true;
                    }
                }

                if (definition.shapeRole == BlockShapeRole.TopOrnament && definition.canSupportBlocksAbove)
                {
                    definition.canSupportBlocksAbove = false;
                    changed = true;
                }

                if (changed)
                {
                    blocks[i] = definition;
                }
            }

            WarnDuplicateIds();
        }

        private void WarnDuplicateIds()
        {
            int count = blocks.Count;
            if (count < 2)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                int id = blocks[i].blockId;
                for (int j = i + 1; j < count; j++)
                {
                    if (blocks[j].blockId != id)
                    {
                        continue;
                    }

                    Debug.LogWarning("Duplicate blockId " + id.ToString() + " in " + name + ".", this);
                    return;
                }
            }
        }
    }
}
