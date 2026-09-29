using System;
using Game.Core.Ports;

namespace Game.Core.Home
{
    /// <summary>
    /// Remembers the region for as long as the process lives.
    /// </summary>
    /// <remarks>
    /// For containers built without a machine behind them — tests, and the
    /// service registration's own default — so that resolving the region system
    /// never depends on player preferences existing.
    /// </remarks>
    public sealed class InMemoryServerRegionStore : IServerRegionStore
    {
        private string saved;

        public bool TryLoad(out string code)
        {
            code = saved;
            return !string.IsNullOrWhiteSpace(saved);
        }

        public void Save(string code)
        {
            saved = code;
        }
    }

    /// <summary>
    /// Which region the player is set to play on, and remembering it.
    /// </summary>
    /// <remarks>
    /// Reads the saved choice once, at construction, because that is when the
    /// answer is needed: the picker opens showing it and the network layer asks
    /// for it before the first connection.
    /// </remarks>
    public sealed class ServerRegionSystem
    {
        private readonly IServerRegionStore store;

        /// <param name="fallbackCode">
        /// Where to start when this machine has saved nothing: the region the
        /// build was shipped for. A build made for one region should open there
        /// rather than wherever the catalogue happens to list first.
        /// </param>
        public ServerRegionSystem(IServerRegionStore store, string fallbackCode = null)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));

            // A saved code the catalogue no longer lists falls back rather than
            // being kept. Regions get renamed and switched off, and a stale one
            // would leave the picker showing nothing selected while the game
            // connected somewhere unnamed.
            if (store.TryLoad(out var code) && ServerRegionCatalog.TryFind(code, out var saved))
            {
                Current = saved;
                return;
            }

            Current = ServerRegionCatalog.TryFind(fallbackCode, out var shipped)
                ? shipped
                : ServerRegionCatalog.Default;
        }

        public ServerRegion Current { get; private set; }

        public event Action<ServerRegion> Changed;

        /// <summary>
        /// Moves to another region and writes it down. False when the code is
        /// not one of ours; true also when it was already the current one, so a
        /// caller cannot read a repeated press as a failure.
        /// </summary>
        public bool TrySelect(string code)
        {
            if (!ServerRegionCatalog.TryFind(code, out var region))
            {
                return false;
            }

            if (string.Equals(region.Code, Current.Code, StringComparison.Ordinal))
            {
                return true;
            }

            Current = region;
            store.Save(region.Code);
            Changed?.Invoke(region);
            return true;
        }
    }
}
