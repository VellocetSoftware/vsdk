using Vellocet.Stereoform;
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
        public AudioEventReference openedSound;
        public AudioEventReference closedSound;
        public AudioEventReference interactSound;
        public AudioEventReference moveSound;
        public AudioEventReference lockedSound;
    }
}