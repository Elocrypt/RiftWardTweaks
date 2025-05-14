namespace RiftWardTweaks
{
    public class RiftWardConfig
    {
        /// <summary>
        /// The effective radius (in blocks) within which the Rift Ward can block rifts.
        /// Default: 30
        /// </summary>
        public int RiftBlockRange { get; set; } = 30;

        /// <summary>
        /// Multiplier for fuel consumption rate. Lower values = slower consumption.
        /// Default: 1.0f 
        /// </summary>
        public float FuelConsumptionMultiplier { get; set; } = 1.0f;

        /// <summary>
        /// The radius for scanning Rift Wards around the player.
        /// Default: 10
        /// </summary>
        public int ScanRadius { get; set; } = 10;

        /// <summary>
        /// The color of the highlighted Rift Ward effective range when scanned.
        /// Using ARGB hex.
        /// Default: 33C00FF00
        /// </summary>
        public string HighlightColor { get; set; } = "#3C00FF00"; // Transparent green

        /// <summary>
        /// The duration of the color preview in milliseconds.
        /// Default: 10000
        /// </summary>
        public int ColorPreviewDurationMs { get; set; } = 10000; // 10 seconds

        /// <summary>
        /// The color of light emitted by Rift Wards, in HSV format.
        /// Default: { 34, 5, 10 } (calm blue)
        /// </summary>
        public int[] LightHSV { get; set; } = new int[] { 34, 5, 15 };

        /// <summary>
        /// Whether Rift Wards emit light when active.
        /// </summary>
        public bool ToggleLight { get; set; } = true;
    }

}