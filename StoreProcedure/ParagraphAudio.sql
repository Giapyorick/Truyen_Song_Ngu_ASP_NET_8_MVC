/* ============================================================================
   AUDIO ELEVENLABS CHO CÂU/ĐOẠN – bảng dbo.tblParagraphAudio
   Dùng cho: Areas/Admin/Controllers/AudioController.cs (trang Admin > Audio truyện)
             Helpers/ParagraphAudioService.cs (job Hangfire tạo audio)
             Controllers/ParagraphsController.cs -> GetAudioMap (trình đọc phát audio)
   - File mp3 nằm trên ổ đĩa: wwwroot/audio/s{StoryID}/c{ChapID}/p{ParagraphID}_{Lang}.mp3
     Bảng CHỈ lưu thông tin mô tả (~150 byte/dòng) -> CSDL không phình.
   - Mỗi câu/đoạn chỉ có 1 audio cho mỗi ngôn ngữ (UNIQUE ParagraphID + Lang).
   - Xóa câu/đoạn -> dòng audio tự xóa theo (ON DELETE CASCADE).
     File mp3 còn sót lại dọn bằng nút "Dọn file thừa" ở trang Admin > Audio truyện.
   - TextHash: 16 ký tự đầu SHA-256 của nội dung đã đọc -> biết câu nào bị sửa sau khi tạo audio.
   Chạy 1 lần trong SSMS trên đúng database của web. Chạy lại nhiều lần vẫn an toàn.
   ============================================================================ */

IF OBJECT_ID('dbo.tblParagraphAudio', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblParagraphAudio (
        ParagraphAudioID INT IDENTITY(1,1) NOT NULL,
        ParagraphID      INT               NOT NULL,
        Lang             CHAR(2)           NOT NULL,   -- en | vi | zh | ja | fr
        ModelId          VARCHAR(40)       NOT NULL,   -- eleven_v3, eleven_flash_v2_5...
        VoiceId          VARCHAR(40)       NOT NULL,
        FilePath         VARCHAR(200)      NOT NULL,   -- /audio/s1/c2/p3_vi.mp3
        TextHash         CHAR(16)          NOT NULL,
        CharCount        INT               NOT NULL,
        CreatedAt        DATETIME          NOT NULL CONSTRAINT DF_tblParagraphAudio_CreatedAt DEFAULT (GETDATE()),

        CONSTRAINT PK_tblParagraphAudio PRIMARY KEY (ParagraphAudioID),
        CONSTRAINT FK_tblParagraphAudio_tblParagraph FOREIGN KEY (ParagraphID)
            REFERENCES dbo.tblParagraph (ParagraphID) ON DELETE CASCADE,
        CONSTRAINT CK_tblParagraphAudio_Lang CHECK (Lang IN ('en', 'vi', 'zh', 'ja', 'fr'))
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_tblParagraphAudio_Paragraph_Lang' AND object_id = OBJECT_ID('dbo.tblParagraphAudio'))
    CREATE UNIQUE INDEX UX_tblParagraphAudio_Paragraph_Lang ON dbo.tblParagraphAudio (ParagraphID, Lang);
GO

/* Kiểm tra */
SELECT TOP 5 * FROM dbo.tblParagraphAudio ORDER BY CreatedAt DESC;
GO
