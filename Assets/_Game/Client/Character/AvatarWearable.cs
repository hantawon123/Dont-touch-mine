using System;
using UnityEngine;

namespace Game.Client.Character
{
    [CreateAssetMenu(menuName = "Game/Avatar Wearable")]
    public sealed class AvatarWearable : ScriptableObject
    {
        public Attachment[] attachments = Array.Empty<Attachment>();

        [Serializable]
        public sealed class Attachment
        {
            public string name;
            public string bone;
            public Mesh mesh;
            public Material[] materials;
            // Per material: 0 authored, 1 body colour, 2 shoe colour, 3 darker sole.
            public int[] tintModes;
        }
    }
}
