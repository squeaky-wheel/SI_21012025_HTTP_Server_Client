using System.ComponentModel.DataAnnotations;

namespace Server.Options
{
    public class ClientNotificationServiceSettings
    {
        [Required]
        [Range(1024, int.MaxValue)]
        public int? ReceiveBufferSizeBytes { get; set; }
    }
}
