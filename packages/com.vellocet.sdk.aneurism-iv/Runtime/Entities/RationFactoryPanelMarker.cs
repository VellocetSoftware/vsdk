using UnityEngine;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class RationFactoryPanelMarker : PrefabEntityMarker
    {
        public enum Content { Status, Instructions }
        [SerializeField] private RationFactoryMarker factory;
        public RationFactoryMarker Factory => factory;
        [SerializeField, Tooltip("Optional font override. None keeps the panel’s digital font.")] private Font font;
        public Font Font => font;
        public abstract Content DisplayContent { get; }
    }
}
