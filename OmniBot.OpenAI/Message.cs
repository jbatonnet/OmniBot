using OmniBot.Common;

using OpenAI.Chat;

namespace OmniBot.OpenAI;

public enum MessageRole
{
    System,
    User,
    Assistant
}
public enum MessageType
{
    Text,
    Image,
    Sound
}

public class Message
{
    public MessageRole Role { get; set; }
    public MessageType Type { get; set; }
    public string Content { get; set; }

    public Message() { }
    public Message(MessageRole role, string content)
    {
        Role = role;
        Content = content;
        Type = MessageType.Text;
    }
    public Message(MessageRole role, byte[] content, MessageType contentType = MessageType.Text)
    {
        Role = role;
        Content = Convert.ToBase64String(content); // TODO: Fix this to use bytes
        Type = contentType;
    }

    internal ChatMessage ToChatMessage()
    {
        ChatMessageContentPart chatMessageContentPart;

        if (Type == MessageType.Text)
            chatMessageContentPart = ChatMessageContentPart.CreateTextPart(Content);
        else if (Type == MessageType.Image)
            chatMessageContentPart = ChatMessageContentPart.CreateImagePart(new BinaryData(Convert.FromBase64String(Content)), "image/png");
        else
            throw new NotImplementedException();

        ChatMessage chatMessage;

        if (Role == MessageRole.System)
            chatMessage = new SystemChatMessage(chatMessageContentPart);
        else if (Role == MessageRole.Assistant)
            chatMessage = new AssistantChatMessage(chatMessageContentPart);
        else if (Role == MessageRole.User)
            chatMessage = new UserChatMessage(chatMessageContentPart);
        else
            throw new NotImplementedException();

        return chatMessage;
    }

    public override string ToString() => $"[{Role}] {Content}";
}
