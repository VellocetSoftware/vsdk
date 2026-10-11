using UnityEngine;

namespace Entities.MapObjects
{
    [CreateAssetMenu(fileName = "DoorModel", menuName = "AneurismIV/DoorModel")]
    public class DoorModel : ScriptableObject
    {
        public bool useOcclusionPortal = true;
        public Vector3 occlusionPortalPosition = new(0, 0, 0);
        public Vector3 occlusionPortalSize = new(1, 1, 1);
        public Mesh mesh;
        public Material material;
        public AudioClip openedSound;
        public AudioClip closedSound;
        public AudioClip interactSound;
        public AudioClip moveSound;
        public AudioClip lockedSound;
    }
}