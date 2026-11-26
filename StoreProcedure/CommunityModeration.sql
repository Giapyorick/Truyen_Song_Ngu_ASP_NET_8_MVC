/* =============================================================================
   KIỂM DUYỆT CỘNG ĐỒNG – chạy 1 lần trên CSDL TruyenSongNgu (SSMS: mở file -> Execute)
   - Thêm cột ẩn nội dung cho tblPosts, tblPostComments
   - Tạo bảng tblContentReports (báo cáo vi phạm từ người đọc)
   Script chạy lại nhiều lần vẫn an toàn (kiểm tra tồn tại trước khi tạo).
   ============================================================================= */
SET XACT_ABORT ON;
BEGIN TRANSACTION;

/* ---------- 1. tblPosts ---------- */
IF COL_LENGTH('dbo.tblPosts', 'IsHidden') IS NULL
    ALTER TABLE dbo.tblPosts ADD IsHidden BIT NOT NULL CONSTRAINT DF_tblPosts_IsHidden DEFAULT (0);
IF COL_LENGTH('dbo.tblPosts', 'HiddenAt') IS NULL
    ALTER TABLE dbo.tblPosts ADD HiddenAt DATETIME2 NULL;
IF COL_LENGTH('dbo.tblPosts', 'HiddenReason') IS NULL
    ALTER TABLE dbo.tblPosts ADD HiddenReason NVARCHAR(200) NULL;

/* ---------- 2. tblPostComments ---------- */
IF COL_LENGTH('dbo.tblPostComments', 'IsHidden') IS NULL
    ALTER TABLE dbo.tblPostComments ADD IsHidden BIT NOT NULL CONSTRAINT DF_tblPostComments_IsHidden DEFAULT (0);
IF COL_LENGTH('dbo.tblPostComments', 'HiddenAt') IS NULL
    ALTER TABLE dbo.tblPostComments ADD HiddenAt DATETIME2 NULL;
IF COL_LENGTH('dbo.tblPostComments', 'HiddenReason') IS NULL
    ALTER TABLE dbo.tblPostComments ADD HiddenReason NVARCHAR(200) NULL;

/* ---------- 3. tblContentReports ---------- */
IF OBJECT_ID('dbo.tblContentReports', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.tblContentReports
    (
        ReportId          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_tblContentReports PRIMARY KEY,
        TargetType        NVARCHAR(10)  NOT NULL,                 -- 'post' | 'comment'
        TargetId          INT           NOT NULL,                 -- PostId hoặc CommentId
        ReporterUserId    INT           NOT NULL,
        Reason            NVARCHAR(30)  NOT NULL,                 -- spam | offensive | spoiler | inappropriate | other
        Note              NVARCHAR(300) NULL,
        CreatedAt         DATETIME2     NOT NULL CONSTRAINT DF_tblContentReports_CreatedAt DEFAULT (SYSDATETIME()),
        Status            NVARCHAR(20)  NOT NULL CONSTRAINT DF_tblContentReports_Status DEFAULT (N'pending'),
        ResolvedAt        DATETIME2     NULL,
        ResolvedByAdminId INT           NULL,
        CONSTRAINT FK_tblContentReports_tblUser FOREIGN KEY (ReporterUserId)
            REFERENCES dbo.tblUser (UserID) ON DELETE CASCADE,
        CONSTRAINT CK_tblContentReports_TargetType CHECK (TargetType IN (N'post', N'comment'))
    );

    -- Mỗi người chỉ báo cáo 1 nội dung 1 lần
    CREATE UNIQUE INDEX IX_tblContentReports_Target_Reporter
        ON dbo.tblContentReports (TargetType, TargetId, ReporterUserId);

    CREATE INDEX IX_tblContentReports_Status
        ON dbo.tblContentReports (Status);
END

COMMIT TRANSACTION;
PRINT N'Đã cập nhật CSDL cho chức năng kiểm duyệt cộng đồng.';
