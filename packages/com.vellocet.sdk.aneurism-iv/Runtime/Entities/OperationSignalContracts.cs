using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Aneurism.Entities
{
    internal static class OperationSignalContracts
    {
        public static void Processing(EntitySignalContractBuilder builder)
        {
            builder.AddOutput("started", EntitySignalValueType.None);
            builder.AddOutput("completed", EntitySignalValueType.None);
            builder.AddOutput("cancelled", EntitySignalValueType.None);
            builder.AddOutput("rejected", EntitySignalValueType.String);
        }
        public static void Ignition(EntitySignalContractBuilder builder)
        {
            builder.AddInput("ignite", EntitySignalValueType.None);
            builder.AddInput("extinguish", EntitySignalValueType.None);
            builder.AddInput("toggle", EntitySignalValueType.None);
            builder.AddOutput("ignited", EntitySignalValueType.None);
            builder.AddOutput("extinguished", EntitySignalValueType.None);
            builder.AddProperty("is_lit", EntitySignalValueType.Bool, true, false);
        }
    }
}
