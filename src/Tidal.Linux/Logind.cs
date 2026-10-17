using Tmds.DBus.Protocol;

namespace Tidal.Linux;

/// <summary>systemd-logind, which lets the user of the active session set backlights without root.</summary>
public interface ILogind
{
    /// <summary>Sets a device's brightness in its own units. Throws when logind refuses or isn't there.</summary>
    void SetBrightness(string subsystem, string device, uint value);
}

/// <summary>logind on the system bus (Session.SetBrightness, systemd 243 and later).</summary>
public sealed class Logind : ILogind
{
    public void SetBrightness(string subsystem, string device, uint value)
    {
        var connection = DBusConnection.System;
        MessageBuffer message;
        using (var writer = connection.GetMessageWriter())
        {
            writer.WriteMethodCallHeader(
                destination: "org.freedesktop.login1",
                path: "/org/freedesktop/login1/session/auto", // the caller's own session
                @interface: "org.freedesktop.login1.Session",
                member: "SetBrightness",
                signature: "ssu");
            writer.WriteString(subsystem);
            writer.WriteString(device);
            writer.WriteUInt32(value);
            message = writer.CreateMessage();
        }
        // Called on a display's I/O thread, which is there to block.
        connection.CallMethodAsync(message).GetAwaiter().GetResult();
    }
}
