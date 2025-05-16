using Newtonsoft.Json.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Config;

public static class RiftWardClientConfig
{
    public static string HighlightColor = "#3C00FF00";
    public static int ColorPreviewDurationMs = 10000;

    public static void Load(ICoreClientAPI capi)
    {
        string path = Path.Combine(GamePaths.ModConfig, "riftwardtweaks_client.json");
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                JObject data = JObject.Parse(json);
                HighlightColor = data["HighlightColor"]?.ToString() ?? HighlightColor;
            }
        }
        catch (Exception e)
        {
            capi.Logger.Warning("[RWT] Could not load client config: " + e);
        }
    }

    public static void Save()
    {
        string path = Path.Combine(GamePaths.ModConfig, "riftwardtweaks_client.json");
        JObject obj = new JObject { ["HighlightColor"] = HighlightColor };
        File.WriteAllText(path, obj.ToString());
    }
}
