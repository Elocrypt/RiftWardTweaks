using ProtoBuf;

namespace RiftWardTweaks.Networking
{
    /// <summary>
    /// Server -&gt; client snapshot of the server-authoritative config values the
    /// client needs to mirror locally. Broadcast on player join and after any
    /// successful <c>/rwt set</c> or <c>/rwt reload</c>.
    /// </summary>
    /// <remarks>
    /// Carries only the fields the client actually consumes. Client-local
    /// settings (highlight colour, colour-preview duration) are deliberately
    /// excluded - they live in <see cref="Config.RiftWardClientConfig"/> and
    /// never travel over the network.
    /// <para>
    /// The <c>[ProtoMember]</c> tag numbers (1-5) are part of the wire format and
    /// must not be reordered or reused. The attribute model below is identical in
    /// protobuf-net 2.x and 3.x, so it is correct regardless of which version
    /// ships with the installed game.
    /// </para>
    /// </remarks>
    [ProtoContract]
    internal class RiftWardConfigSyncPacket
    {
        /// <summary>
        /// Raw HSV light values emitted by active wards. Clamped to valid byte
        /// ranges client-side before use (see <c>GetSafeHSV</c>).
        /// </summary>
        [ProtoMember(1)]
        public int[]? LightHSV { get; set; }

        /// <summary>Whether active wards emit light at all.</summary>
        [ProtoMember(2)]
        public bool ToggleLight { get; set; }

        /// <summary>Effective rift-blocking radius, in blocks.</summary>
        [ProtoMember(3)]
        public int RiftBlockRange { get; set; }

        /// <summary>Fuel burn-rate multiplier. Must be greater than zero.</summary>
        [ProtoMember(4)]
        public float FuelConsumptionMultiplier { get; set; }

        /// <summary>
        /// Radius, in blocks, that the client scans around the player for
        /// <c>/rwt show</c>. Synced (not server-only) because the client builds
        /// its entire <c>Config</c> from this packet and never reads the server
        /// config file; without it, the highlight scan would silently fall back
        /// to the hard-coded default radius and ignore the admin's setting.
        /// </summary>
        [ProtoMember(5)]
        public int ScanRadius { get; set; }
    }
}
