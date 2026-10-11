using System;

namespace Entities.Shared
{
    [Flags]
    public enum TagsWeapon
    {
        None = 0,
        Blunt = 1 << 0,
        Sharp = 1 << 1,
        Explosive = 1 << 2,
        Burn = 1 << 3,
        Freeze = 1 << 4,
        Poison = 1 << 5,
        Shock = 1 << 6,
        Bleed = 1 << 7,
        Bullet = 1 << 8,
        Repair = 1 << 9,
        Fire = 1 << 10,
        Build = 1 << 11
    }
}
