using System.ComponentModel.DataAnnotations;

namespace Server.Options
{
    public class MessagesSettings
    {
        [Required]
        [Range(0, int.MaxValue)]
        public int? WebSocketConnectionKeepAliveIntervalMilliseconds { get; set; }
    }
}
