// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

namespace Vellocet.Sdk.Entities.Signals
{
    public static class EntitySignalNameUtility
    {
        public static string Normalize(string value) => Vertex.Contracts.EntitySignalNameUtility.Normalize(value);

        public static bool Matches(string declaredName, string authoredName) =>
            Vertex.Contracts.EntitySignalNameUtility.Matches(declaredName, authoredName);
    }
}
