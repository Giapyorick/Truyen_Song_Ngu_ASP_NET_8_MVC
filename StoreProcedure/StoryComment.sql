/* ============================================================================
   BÌNH LUẬN TRUYỆN – bảng riêng dbo.tblStoryComment
   Dùng cho: Controllers/StoryCommentsController.cs (trang Stories/Detail)
   - KHÔNG đụng tới bảng tblComment cũ (bảng đó trong CSDL thật không khớp model).
   - Xóa truyện  -> bình luận của truyện tự xóa theo (ON DELETE CASCADE).
   - Xóa tài khoản -> bình luận vẫn giữ, chỉ bỏ liên kết người dùng (ON DELETE SET NULL),
     tên hiển thị lấy từ cột UserName đã lưu lúc bình luận.
   Chạy 1 lần trong SSMS trên đúng database của web. Chạy lại nhiều lần vẫn an toàn.
   ============================================================================ */

IF OBJECT_ID('dbo.tblStoryComment', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblStoryComment (
        StoryCommentID  INT IDENTITY(1,1) NOT NULL,
        StoryID         INT               NOT NULL,
        UserID          INT               NULL,
        UserName        NVARCHAR(100)     NULL,
        Contents        NVARCHAR(1000)    NOT NULL,
        CreatedAt       DATETIME          NOT NULL CONSTRAINT DF_tblStoryComment_CreatedAt DEFAULT (GETDATE()),

        CONSTRAINT PK_tblStoryComment PRIMARY KEY (StoryCommentID),
        CONSTRAINT FK_tblStoryComment_tblStory FOREIGN KEY (StoryID)
            REFERENCES dbo.tblStory (StoryID) ON DELETE CASCADE,
        CONSTRAINT FK_tblStoryComment_tblUser FOREIGN KEY (UserID)
            REFERENCES dbo.tblUser (UserID) ON DELETE SET NULL
    );
END
GO

/* Lấy danh sách theo truyện, mới nhất trước */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblStoryComment_Story_Created' AND object_id = OBJECT_ID('dbo.tblStoryComment'))
    CREATE INDEX IX_tblStoryComment_Story_Created ON dbo.tblStoryComment (StoryID, CreatedAt DESC);
GO

/* Kiểm tra chống spam theo người dùng */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_tblStoryComment_User_Created' AND object_id = OBJECT_ID('dbo.tblStoryComment'))
    CREATE INDEX IX_tblStoryComment_User_Created ON dbo.tblStoryComment (UserID, CreatedAt DESC);
GO

/* Kiểm tra */
SELECT TOP 5 * FROM dbo.tblStoryComment ORDER BY CreatedAt DESC;
GO
