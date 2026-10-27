using System;
using UnityEngine;
using Vellocet.Sdk.Entities.Base;
using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    [EntityClass("func_bulletin_board")]
    [EntityAuthoring("Information", EntityPlacementKind.Point, "Bulletin Board")]
    public sealed class BulletinBoardMarker : ContactOperationMarker
    {
        public override bool SupportsInteraction => true;
        public override string ArchetypeId => "aniv.bulletin-board";
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddInput("post_ui", EntitySignalValueType.None);
            builder.AddOutput("posted", EntitySignalValueType.None);
            builder.AddProperty("post_count", EntitySignalValueType.Int, true, false);
        }
    }
}
