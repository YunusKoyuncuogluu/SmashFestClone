using UnityEngine;

namespace SmashFest.LevelEditor
{
    internal struct HighlightFrame
    {
        public bool active;
        public bool dashed;
        public Vector3Int anchor;
        public Vector3Int size;
        public Color color;
        public bool hasTopLeft;
        public bool hasBottomRight;
        public Rect topLeft;
        public Rect bottomRight;
    }
}
