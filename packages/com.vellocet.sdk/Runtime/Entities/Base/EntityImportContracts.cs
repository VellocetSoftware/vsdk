namespace Vellocet.Sdk.Entities.Base
{
    public interface ISplineEntityMarker { }

    // A group owns its authored hierarchy without turning static geometry into a mover.
    public interface IEntityImportGroup { }

    // Native game markers may retain helper entities and presentation without becoming moving brush entities.
    public interface IEntityImportHierarchy { }

    public interface ILightEntityMarker
    {
        bool SupportsAreaLight { get; }
    }

    public interface IEntityImportOwner
    {
        EntityMarker ImportOwner { get; }
    }
}
