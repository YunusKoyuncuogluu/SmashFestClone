using UnityEditor;

namespace SmashFast.LevelEditor
{
    internal class LevelEditorDragPayload
    {
        public const string Key = "SmashFast.BlockId";

        public void Begin(int blockId, string label)
        {
            DragAndDrop.PrepareStartDrag();
            DragAndDrop.SetGenericData(Key, blockId);
            string dragLabel = string.IsNullOrEmpty(label) ? "Block" : label;
            DragAndDrop.StartDrag(dragLabel);
        }

        public bool TryGetBlockId(out int blockId)
        {
            object payload = DragAndDrop.GetGenericData(Key);
            if (payload is int id)
            {
                blockId = id;
                return true;
            }

            blockId = 0;
            return false;
        }
    }
}
