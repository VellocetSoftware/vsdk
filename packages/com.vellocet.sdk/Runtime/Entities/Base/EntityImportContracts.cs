namespace Vellocet.Sdk.Entities.Base
{
    public interface ISplineEntityMarker { }

    // A group owns its authored hierarchy without turning static geometry into a mover.
    public interface IEntityImportGroup { }

    public interface ILightEntityMarker
    {
        bool SupportsAreaLight { get; }
    }

    public interface IEntityImportOwner
    {
        EntityMarker ImportOwner { get; }
    }
}
