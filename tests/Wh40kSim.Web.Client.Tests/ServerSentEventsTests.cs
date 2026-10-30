using System.Text;
using Wh40kSim.Web.Client.Services;

namespace Wh40kSim.Web.Client.Tests;

/// <summary>Lecture d'un flux Server-Sent Events.</summary>
public class ServerSentEventsTests
{
    private static async Task<List<(string Type, string Data)>> ReadAsync(string text)
    {
        var events = new List<(string, string)>();
        await foreach (var item in ServerSentEvents.ReadAsync(new MemoryStream(Encoding.UTF8.GetBytes(text))))
        {
            events.Add(item);
        }
        return events;
    }

    [Fact]
    public async Task Events_are_read_with_their_type_and_data()
    {
        var events = await ReadAsync("event: progress\ndata: 0.5\n\nevent: result\ndata: {\"a\":1}\n\n");

        Assert.Equal([("progress", "0.5"), ("result", "{\"a\":1}")], events);
    }

    [Fact]
    public async Task Multi_line_data_comments_and_default_type_are_handled()
    {
        var events = await ReadAsync(": commentaire\ndata: ligne 1\ndata: ligne 2\n\n\r\nevent: queued\r\ndata:3\r\n\r\n");

        Assert.Equal([("message", "ligne 1\nligne 2"), ("queued", "3")], events);
    }

    [Fact]
    public async Task A_last_event_without_blank_line_is_kept()
    {
        Assert.Equal([("error", "panne")], await ReadAsync("event: error\ndata: panne"));
    }
}
