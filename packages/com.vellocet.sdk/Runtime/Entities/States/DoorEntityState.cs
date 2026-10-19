using Vellocet.Sdk.Entities.Signals;

namespace Vellocet.Sdk.Entities.States
{
    public readonly struct DoorEntityState : IEntityStateSnapshot
    {
        public DoorEntityState(bool isOpen, bool isLocked) : this(isOpen, isLocked, isOpen ? 1 : 0, false, false, -1) { }
        public DoorEntityState(bool isOpen, bool isLocked, float progress01, bool isMoving, bool isKilled = false, float autoCloseRemaining = -1)
        {
            IsOpen = isOpen;
            IsLocked = isLocked;
            Progress01 = progress01;
            IsMoving = isMoving;
            IsKilled = isKilled;
            AutoCloseRemaining = autoCloseRemaining;
        }
        public string StateType => nameof(DoorEntityState);
        public bool IsOpen
        {
            get;
        }
        public bool IsLocked
        {
            get;
        }
        public float Progress01
        {
            get;
        }
        public bool IsMoving
        {
            get;
        }
        public bool IsKilled
        {
            get;
        }
        public float AutoCloseRemaining
        {
            get;
        }
        public bool IsValid => float.IsFinite(Progress01) && Progress01 >= 0 && Progress01 <= 1 && float.IsFinite(AutoCloseRemaining) && AutoCloseRemaining >= -1;
    }
}
