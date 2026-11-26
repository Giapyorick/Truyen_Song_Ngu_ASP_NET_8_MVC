using System;

namespace WebTruyenTranh.Models;

/// <summary>
/// Audio ElevenLabs của 1 câu/đoạn theo 1 ngôn ngữ (bảng dbo.tblParagraphAudio – script StoreProcedure/ParagraphAudio.sql).
/// File mp3 lưu trên ổ đĩa (wwwroot/audio/...), bảng chỉ giữ thông tin mô tả.
/// </summary>
public partial class TblParagraphAudio
{
    public int ParagraphAudioId { get; set; }

    public int ParagraphId { get; set; }

    /// <summary>en | vi | zh | ja | fr</summary>
    public string Lang { get; set; } = null!;

    public string ModelId { get; set; } = null!;

    public string VoiceId { get; set; } = null!;

    /// <summary>Đường dẫn web, ví dụ /audio/s1/c2/p3_vi.mp3</summary>
    public string FilePath { get; set; } = null!;

    /// <summary>16 ký tự đầu SHA-256 của nội dung đã đọc (phát hiện câu bị sửa sau khi tạo audio)</summary>
    public string TextHash { get; set; } = null!;

    public int CharCount { get; set; }

    public DateTime CreatedAt { get; set; }
}
