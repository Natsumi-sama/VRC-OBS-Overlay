using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.Win32;

[SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
public class PollRegistry
{
    public class RegistryData
    {
        public string WorldId { get; set; } = string.Empty;
        public string WorldName { get; set; } = string.Empty;
    }
    
    public static RegistryData? GetVRChatLocation()
    {
        const string keyName = "LocationContext_World_h2703649242";
        using var regKey = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\VRChat\VRChat");
        var data = regKey?.GetValue(keyName);
        if (data == null)
            return null;

        var locationString = Encoding.ASCII.GetString((byte[])data);
        var index = locationString.IndexOf('|');
        if (index < 0)
            return null;
        
        return new RegistryData
        {
            WorldId = locationString.Substring(0, index),
            WorldName = locationString.Substring(index + 1)
        };
    }
}