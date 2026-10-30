using System.Runtime.CompilerServices;
using System.Text;

namespace Wh40kSim.Web.Client.Services;

/// <summary>Lecture d'un flux Server-Sent Events, événement par événement (champs <c>event</c> et <c>data</c>).</summary>
public static class ServerSentEvents
{
    /// <summary>Événements du flux, au fil de leur arrivée ; type « message » si le serveur n'en donne pas.</summary>
    public static async IAsyncEnumerable<(string Type, string Data)> ReadAsync(Stream stream, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8);
        string type = "message";
        var data = new StringBuilder();
        bool hasData = false;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (line.Length == 0)
            {
                if (hasData)
                {
                    yield return (type, data.ToString());
                }
                type = "message";
                data.Clear();
                hasData = false;
                continue;
            }
            if (line.StartsWith(':'))
            {
                continue; // commentaire
            }
            int colon = line.IndexOf(':');
            string field = colon < 0 ? line : line[..colon];
            string value = colon < 0 ? "" : line[(colon + 1)..];
            if (value.StartsWith(' '))
            {
                value = value[1..];
            }
            switch (field)
            {
                case "event":
                    type = value;
                    break;
                case "data":
                    if (hasData)
                    {
                        data.Append('\n');
                    }
                    data.Append(value);
                    hasData = true;
                    break;
            }
        }
        if (hasData)
        {
            yield return (type, data.ToString());
        }
    }
}
