using System.Text;

namespace ProtoStream;

public enum MessageType
{
    Command = 0,
    CommandResponse = 1,
    Event = 2,
}
