// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System;

namespace Vellocet.Sdk
{
    public enum Team
    {
        None = 0,
        Spectate = 1,
        Order = 2,
        Union = 3
    }

    [Flags]
    public enum TeamMask
    {
        None = 0,
        Spectate = 1 << 0,
        Order = 1 << 1,
        Union = 1 << 2,
        All = Spectate | Order | Union
    }

    public static class TeamExtensions
    {
        public static string ToID(this Team team)
        {
            return team switch
            {
                Team.Order => "order",
                Team.Union => "union",
                _ => "neutral"
            };
        }

        public static TeamMask ToMask(this Team team)
        {
            return team switch
            {
                Team.Spectate => TeamMask.Spectate,
                Team.Order => TeamMask.Order,
                Team.Union => TeamMask.Union,
                _ => TeamMask.None
            };
        }

        public static bool IsInMask(this Team team, TeamMask mask)
        {
            return (mask & team.ToMask()) != 0;
        }
    }
}