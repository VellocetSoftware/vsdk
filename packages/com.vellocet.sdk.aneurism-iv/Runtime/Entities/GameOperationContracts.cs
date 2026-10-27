using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    public static class GameOperationContracts
    {
        public static void Utility(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("broken", EntitySignalValueType.None);
            builder.AddOutput("repaired", EntitySignalValueType.None);
            builder.AddProperty("is_broken", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("available", EntitySignalValueType.Bool, true, false);
            builder.AddProperty("durability", EntitySignalValueType.Float, true, false);
        }
    }
}
