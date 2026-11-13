using System;
using SmashFest.Data;
using UnityEngine;

namespace SmashFest.Audio
{
    [Serializable]
    public class MaterialSounds
    {
        public BlockMaterialType material;
        public AudioClip[] hits;
        public AudioClip[] breaks;
        [Range(0f, 1f)] public float volume = 1f;
    }

    [CreateAssetMenu(fileName = "SoundLibrary", menuName = "SmashFest/Sound Library", order = 5)]
    public class SoundLibrarySO : ScriptableObject
    {
        public AudioClip music;
        [Range(0f, 1f)] public float musicVolume = 0.22f;
        [Range(0f, 1f)] public float effectsVolume = 0.9f;
        public AudioClip[] cannonFire;
        public AudioClip[] ballThuds;
        public AudioClip[] liquidSplats;
        public AudioClip[] uiClicks;
        [Range(0f, 1f)] public float uiClickVolume = 0.6f;
        public AudioClip levelWin;
        public AudioClip levelFail;
        [Range(0f, 1f)] public float jingleVolume = 0.8f;
        public MaterialSounds[] materials;

        public MaterialSounds Find(BlockMaterialType material)
        {
            if (materials == null)
                return null;

            foreach (MaterialSounds sounds in materials)
            {
                if (sounds != null && sounds.material == material)
                    return sounds;
            }

            return null;
        }
    }
}
