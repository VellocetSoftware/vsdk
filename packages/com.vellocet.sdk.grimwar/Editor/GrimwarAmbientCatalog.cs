using UnityEditor;
using Vellocet.Sdk.Editor.Entities;

namespace Vellocet.Sdk.Grimwar
{
    [InitializeOnLoad]
    internal static class GrimwarAmbientCatalog
    {
        static GrimwarAmbientCatalog() => AmbientEventCatalog.RegisterCompiled(
            AmbientEventCatalogGenerated.ProjectId, AmbientEventCatalogGenerated.EventNames);
    }
}
