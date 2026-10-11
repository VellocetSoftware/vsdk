using UnityEngine;

namespace Vellocet.Sdk.Aneurism
{
    public static class AneurismCollision
    {
        public const string WorldClip = "WorldClip";
        public const string PlayerSolid = "PlayerSolid";
        public const string PlayerClip = "PlayerClip";
        public const string EntitySolid = "EntitySolid";
        public const string EntityClip = "EntityClip";
        public const string RaycastOnly = "RaycastOnly";

        public static int ClipMask => LayerMask.GetMask(WorldClip, PlayerClip, EntityClip);
        public static int SurfaceMask => LayerMask.GetMask(PlayerSolid, EntitySolid, RaycastOnly);
    }
}
