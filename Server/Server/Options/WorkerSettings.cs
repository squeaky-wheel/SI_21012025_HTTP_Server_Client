using System.ComponentModel.DataAnnotations;

namespace Server.Options
{
    public class WorkerSettings
    {
        [Required]
        [Range(1, int.MaxValue)]
        public int? TimeToPretendToWorkForMilliseconds { get; set; }
    }
}
