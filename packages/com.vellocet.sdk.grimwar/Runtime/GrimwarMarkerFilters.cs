using UnityEngine;

namespace Vellocet.Sdk.Entities
{
    public sealed partial class DamageableMarker
    {
        [field: Header("Teams")]
        [field: Tooltip("Optional team filter. None means any team can damage it. Supports multiple teams via flags.")]
        [field: SerializeField]
        public TeamMask AllowedTeams { get; private set; } = TeamMask.None;
    }

    public sealed partial class TriggerTeleportMarker
    {
        [field: SerializeField] public TeamMask AllowedTeams { get; private set; } = TeamMask.All;
    }

    public sealed partial class TriggerPushMarker
    {
        [field: Tooltip("Participant teams this trigger may push.")]
        [field: SerializeField]
        public TeamMask AllowedTeams { get; private set; } = TeamMask.All;
    }

    public sealed partial class TriggerMarker
    {
        [field: Tooltip(
            "Allow participants from these teams. None blocks all participants; unassigned pawns and other colliders still follow Allowed Activators.")]
        [field: SerializeField]
        public TeamMask AllowedTeams { get; private set; } = TeamMask.None;
    }

    public sealed partial class TriggerHurtMarker
    {
        [field: Tooltip("Optional team mask filter for participant-owned activators.")]
        [field: SerializeField]
        public TeamMask AllowedTeams { get; private set; } = TeamMask.All;
    }
}
