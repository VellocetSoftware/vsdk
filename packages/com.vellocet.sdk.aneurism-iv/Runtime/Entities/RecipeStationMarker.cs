using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public abstract class RecipeStationMarker : OperationEntityMarker
    {
        public override void ConfigureSignalContract(EntitySignalContractBuilder builder)
        {
            builder.AddInput("start", EntitySignalValueType.String);
            builder.AddInput("open_ui", EntitySignalValueType.None);
            builder.AddOutput("started", EntitySignalValueType.String);
            builder.AddOutput("completed", EntitySignalValueType.String);
            builder.AddOutput("rejected", EntitySignalValueType.String);
            builder.AddProperty("busy", EntitySignalValueType.Bool, readable: true, writable: false);
        }
    }
}
