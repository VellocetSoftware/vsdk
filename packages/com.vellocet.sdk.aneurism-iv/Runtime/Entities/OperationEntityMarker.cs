using UnityEngine;
using Vellocet.Sdk.Entities.Base;

namespace Vellocet.Sdk.Aneurism.Entities
{
    // Attachments change arrangement, never the game-owned recipe or permission rules.
    public abstract class OperationEntityMarker : ContactOperationMarker
    {
        [SerializeField, Tooltip("Optional independently placed output point in this map.")]
        private EntityMarker outputTarget;
        public EntityMarker OutputTarget => outputTarget;
    }
}
