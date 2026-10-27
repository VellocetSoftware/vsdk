using UnityEngine;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class ContactOperationMarker : PrefabEntityMarker
    {
        [SerializeField, Tooltip("Optional independently placed contact/ingredient volume.")]
        private OperationVolumeMarker inputVolume;
        [SerializeField, Tooltip("Show and collide with the supplied model. Disable when composing custom geometry and presentation.")]
        private bool useBuiltInPresentation = true;
        public OperationVolumeMarker InputVolume => inputVolume;
        public bool UseBuiltInPresentation => useBuiltInPresentation;
    }
}
