namespace RiftWardTweaks.Config
{
    /// <summary>
    /// Server-authoritative gameplay configuration for Rift Ward Tweaks. Loaded
    /// from and saved to <c>riftwardtweaksconfig.json</c> on the server, then
    /// mirrored on each client via <c>RiftWardConfigSyncPacket</c>.
    /// </summary>
    /// <remarks>
    /// Plain, serialization-friendly data: no <c>ICoreAPI</c> reference, no events.
    /// The documented invariants (positive fuel multiplier, three-element HSV,
    /// positive ranges) are enforced by <see cref="Normalize"/>, a pure repair
    /// method called right after the config is loaded from disk or rebuilt from a
    /// sync packet. <c>LightHSV</c> values are additionally clamped to engine-safe
    /// byte ranges by <c>GetSafeHSV</c> at the point of use.
    /// Client-local settings (highlight colour, colour-preview duration) are NOT
    /// here - they live in <see cref="RiftWardClientConfig"/> and never sync.
    /// </remarks>
    internal class RiftWardConfig
    {
        /// <summary>
        /// Effective radius, in blocks, within which an active ward blocks rifts.
        /// Default: 30.
        /// </summary>
        public int RiftBlockRange { get; set; } = 30;

        /// <summary>
        /// Multiplier applied to the fuel consumption rate. Below 1 slows the burn;
        /// above 1 accelerates it.
        /// <para><b>Invariant:</b> must be greater than 0. A value &lt;= 0 is treated
        /// as invalid and reset to 1.0 on load.</para>
        /// Default: 1.0.
        /// </summary>
        public float FuelConsumptionMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// Radius, in blocks, scanned around the player by the client <c>/rwt show</c>
        /// command when searching for nearby wards to highlight. Default: 10.
        /// </summary>
        public int ScanRadius { get; set; } = 10;

        /// <summary>
        /// Colour of light emitted by active wards, as raw HSV <c>[H, S, V]</c>.
        /// <para><b>Invariant:</b> must contain exactly 3 elements. Engine-safe
        /// ranges (clamped by <c>GetSafeHSV</c>) are H 0-64, S 0-8, V 3-21.</para>
        /// Default: <c>{ 34, 5, 15 }</c> (a calm blue).
        /// </summary>
        public int[] LightHSV { get; set; } = new int[] { 34, 5, 15 };

        /// <summary>
        /// Whether active wards emit light at all. Default: true.
        /// </summary>
        public bool ToggleLight { get; set; } = true;

        /// <summary>
        /// Repairs any values that violate this type's invariants, resetting them to
        /// safe defaults: a positive <see cref="FuelConsumptionMultiplier"/>, a
        /// three-element <see cref="LightHSV"/>, and positive <see cref="RiftBlockRange"/>
        /// and <see cref="ScanRadius"/>. Pure (mutates only this instance) and
        /// idempotent; call after loading from disk or receiving a sync packet.
        /// Value-range clamping of <see cref="LightHSV"/> stays at the point of use
        /// (<c>GetSafeHSV</c>); this only guarantees the array length and the scalar
        /// invariants.
        /// </summary>
        /// <returns><c>true</c> if any value had to be changed.</returns>
        public bool Normalize()
        {
            bool changed = false;

            if (FuelConsumptionMultiplier <= 0 ||
                float.IsNaN(FuelConsumptionMultiplier) ||
                float.IsInfinity(FuelConsumptionMultiplier))
            {
                FuelConsumptionMultiplier = 1.0f;
                changed = true;
            }

            if (LightHSV is not { Length: 3 })
            {
                LightHSV = new int[] { 34, 5, 15 };
                changed = true;
            }

            if (RiftBlockRange <= 0)
            {
                RiftBlockRange = 30;
                changed = true;
            }

            if (ScanRadius <= 0)
            {
                ScanRadius = 10;
                changed = true;
            }

            return changed;
        }
    }
}
