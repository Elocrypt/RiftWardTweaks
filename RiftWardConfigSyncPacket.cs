using ProtoBuf;

[ProtoContract]
public class RiftWardConfigSyncPacket
{
    [ProtoMember(1)]
    public int[]? LightHSV { get; set; }
    [ProtoMember(2)]
    public bool ToggleLight { get; set; }
    [ProtoMember(3)]
    public int RiftBlockRange { get; set; }
    [ProtoMember(4)]
    public float FuelConsumptionMultiplier { get; set; }
    [ProtoMember(5)]
    public int ScanRadius { get; set; }
}
