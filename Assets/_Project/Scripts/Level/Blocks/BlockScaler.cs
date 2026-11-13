using UnityEngine;

namespace SmashFest.Level
{
    public class BlockScaler
    {
        private readonly Transform owner;
        private readonly Transform tableTop;
        private Renderer[] tableTopRenderers;

        public BlockScaler(Transform owner, Transform tableTop)
        {
            this.owner = owner;
            this.tableTop = tableTop;
        }

        public void Apply(Vector3 layoutScale, float globalScale, float tableHeightFactor, Vector3Int sizeInCells)
        {
            if (tableTop == null)
            {
                Vector3 blockScale = layoutScale == Vector3.zero ? Vector3.one : layoutScale;
                owner.localScale = blockScale * globalScale;
                return;
            }

            float heightFactor = Mathf.Max(1f, tableHeightFactor);
            owner.localScale = new Vector3(globalScale, globalScale * heightFactor, globalScale);

            float topScaleX = Mathf.Max(1, sizeInCells.x);
            float topScaleZ = Mathf.Max(1, sizeInCells.z);
            Vector3 oldScale = tableTop.localScale;
            Vector3 oldPosition = tableTop.localPosition;
            tableTop.localScale = new Vector3(topScaleX, 1f / heightFactor, topScaleZ);
            tableTop.localPosition = new Vector3(
                oldPosition.x / oldScale.x * topScaleX,
                oldPosition.y,
                oldPosition.z / oldScale.z * topScaleZ);

            AlignTableTop(globalScale * heightFactor * 0.5f);
        }

        private void AlignTableTop(float heightAboveCenter)
        {
            if (tableTopRenderers == null || tableTopRenderers.Length == 0)
                tableTopRenderers = tableTop.GetComponentsInChildren<Renderer>(true);

            if (tableTopRenderers.Length == 0)
                return;

            float currentTop = float.MinValue;
            foreach (Renderer topRenderer in tableTopRenderers)
                currentTop = Mathf.Max(currentTop, topRenderer.bounds.max.y);

            float offset = owner.position.y + heightAboveCenter - currentTop;
            tableTop.position += Vector3.up * offset;
        }
    }
}
