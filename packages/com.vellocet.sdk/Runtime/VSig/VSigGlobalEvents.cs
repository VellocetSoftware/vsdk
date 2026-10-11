// Copyright (c) 2026 Vellocet Corporation. All rights reserved.
// SPDX-License-Identifier: LicenseRef-Vellocet-Proprietary

using System.Linq;

namespace Vellocet.Sdk.VSig
{
    /// <summary>
    ///     Stable names for engine-authored VSig events that do not originate from a map entity.
    /// </summary>
    public static partial class VSigGlobalEvents
    {
        private static SdkGameDefinition _game;
        private static Vertex.Contracts.GlobalSignalCatalog _catalog;
        public static Vertex.Contracts.GlobalSignalCatalog Catalog
        {
            get
            {
                if (!ReferenceEquals(_game, SdkGame.Current))
                {
                    _game = SdkGame.Current;
                    _catalog = new Vertex.Contracts.GlobalSignalCatalog(Shared.Concat(_game.AdditionalGlobalEvents));
                }
                return _catalog;
            }
        }

        private static readonly Vertex.Contracts.GlobalSignalDefinition[] Shared = new[]
        {
            new Vertex.Contracts.GlobalSignalDefinition("map", "ready"),
            new Vertex.Contracts.GlobalSignalDefinition("round", "warmup"),
            new Vertex.Contracts.GlobalSignalDefinition("round", "pre_round"),
            new Vertex.Contracts.GlobalSignalDefinition("round", "live"),
            new Vertex.Contracts.GlobalSignalDefinition("round", "overtime"),
            new Vertex.Contracts.GlobalSignalDefinition("round", "post_round"),
            new Vertex.Contracts.GlobalSignalDefinition("round", "intermission"),
        };

    }
}
