using System.ComponentModel.DataAnnotations;

namespace CPTStore.Models
{
    public enum MessageType
    {
        Text,
        Image,
        System
    }

    public class ChatMessage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [Required]
        public string ReceiverId { get; set; } = string.Empty;

        [Required]
        [StringLength(1000, ErrorMessage = "Nội dung tin nhắn không được vượt quá 1000 ký tự")]
        public string Content { get; set; } = string.Empty;

        public MessageType MessageType { get; set; } = MessageType.Text;

        public DateTime SentAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }
    }
}