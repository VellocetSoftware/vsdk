using UnityEditor.AddressableAssets.Settings;

namespace Vellocet.Sdk.Editor.Maps
{
    public sealed class BaseMapValidationOptions
    {
        public bool RequireStandardLayout;
        public bool ValidateNavigation;
        public bool AllowSkippedSpatialOctreeBake = true;
        public bool ValidateAddressables;
        public AddressableAssetSettings Addressables;
        public string DefinitionAddress;
        public string SceneAddress;
    }
}
