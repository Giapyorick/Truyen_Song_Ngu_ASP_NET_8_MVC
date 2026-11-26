/* ============================================================================
   MÃ ĐỊNH DANH TRUYỆN (StoryCode) – dạng YYMMNNN, hiển thị "#2611001"
     YY  : 2 số cuối của năm tạo truyện   (26 = 2026)
     MM  : tháng tạo truyện               (11 = tháng 11)
     NNN : số thứ tự truyện trong tháng   (001, 002, ...; quá 999 thì thành 4 số)

   - Truyện ĐÃ CÓ: cấp mã theo ngày xuất bản (PublicationDate), cùng ngày thì
     theo StoryID. Truyện chưa có ngày xuất bản: lấy tháng hiện tại.
   - Truyện MỚI: trigger tự cấp mã theo NGÀY TẠO (thời điểm thêm vào CSDL).
   - Mã KHÔNG THỂ sửa hay xóa (trigger chặn), không trùng (unique index).
   - Chạy 1 lần trong SSMS trên đúng database của web. Chạy lại nhiều lần vẫn an toàn.
   ============================================================================ */

SET XACT_ABORT ON;
GO

/* 1. Thêm cột */
IF COL_LENGTH('dbo.tblStory', 'StoryCode') IS NULL
BEGIN
    ALTER TABLE dbo.tblStory ADD StoryCode VARCHAR(10) NULL;
END
GO

/* 2. Cấp mã cho các truyện đang có (chỉ truyện chưa có mã) */
;WITH src AS (
    SELECT  s.StoryID,
            s.StoryCode,
            Prefix = RIGHT(CONVERT(CHAR(4), YEAR(d.Dt)), 2)
                   + RIGHT('0' + CONVERT(VARCHAR(2), MONTH(d.Dt)), 2),
            d.Dt
    FROM dbo.tblStory s
    CROSS APPLY (SELECT Dt = COALESCE(CONVERT(DATE, s.PublicationDate), CONVERT(DATE, GETDATE()))) d
    WHERE s.StoryCode IS NULL
),
numbered AS (
    SELECT  src.StoryID,
            src.Prefix,
            Seq = ISNULL((SELECT MAX(CAST(SUBSTRING(x.StoryCode, 5, 10) AS INT))
                          FROM dbo.tblStory x
                          WHERE x.StoryCode LIKE src.Prefix + '%'), 0)
                + ROW_NUMBER() OVER (PARTITION BY src.Prefix ORDER BY src.Dt, src.StoryID)
    FROM src
)
UPDATE s
SET StoryCode = n.Prefix
              + CASE WHEN n.Seq < 1000 THEN RIGHT('00' + CONVERT(VARCHAR(10), n.Seq), 3)
                     ELSE CONVERT(VARCHAR(10), n.Seq) END
FROM dbo.tblStory s
JOIN numbered n ON n.StoryID = s.StoryID;
GO

/* 3. Không cho trùng mã */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_tblStory_StoryCode' AND object_id = OBJECT_ID('dbo.tblStory'))
BEGIN
    CREATE UNIQUE INDEX UX_tblStory_StoryCode ON dbo.tblStory (StoryCode) WHERE StoryCode IS NOT NULL;
END
GO

/* 4. Trigger: tự cấp mã khi thêm truyện + chặn mọi thao tác sửa / xóa mã
      (Tên trigger được khai báo trong TruyenSongNguContext: HasTrigger("trg_tblStory_StoryCode")) */
CREATE OR ALTER TRIGGER dbo.trg_tblStory_StoryCode
ON dbo.tblStory
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Lệnh UPDATE do chính trigger này chạy bên dưới -> bỏ qua
    IF TRIGGER_NESTLEVEL(@@PROCID) > 1 RETURN;

    -- 4a. UPDATE: không được đổi, xóa hay tự đặt mã
    IF EXISTS (SELECT 1 FROM deleted)
    BEGIN
        IF UPDATE(StoryCode) AND EXISTS (
            SELECT 1
            FROM inserted i
            JOIN deleted d ON d.StoryID = i.StoryID
            WHERE ISNULL(i.StoryCode, '') <> ISNULL(d.StoryCode, '')
        )
        BEGIN
            ;THROW 51000, N'Mã định danh truyện (StoryCode) không được phép thay đổi.', 1;
        END
        RETURN;
    END

    -- 4b. INSERT: luôn cấp mã theo ngày tạo (bỏ qua giá trị tự nhập, nếu có)
    DECLARE @now DATETIME = GETDATE();
    DECLARE @prefix CHAR(4) = RIGHT(CONVERT(CHAR(4), YEAR(@now)), 2)
                            + RIGHT('0' + CONVERT(VARCHAR(2), MONTH(@now)), 2);
    DECLARE @max INT;

    -- Khóa phạm vi để 2 truyện thêm cùng lúc không bị trùng số thứ tự
    SELECT @max = ISNULL(MAX(CAST(SUBSTRING(StoryCode, 5, 10) AS INT)), 0)
    FROM dbo.tblStory WITH (UPDLOCK, HOLDLOCK)
    WHERE StoryCode LIKE @prefix + '%'
      AND StoryID NOT IN (SELECT StoryID FROM inserted);

    ;WITH n AS (
        SELECT StoryID, rn = ROW_NUMBER() OVER (ORDER BY StoryID)
        FROM inserted
    )
    UPDATE s
    SET StoryCode = @prefix
                  + CASE WHEN @max + n.rn < 1000 THEN RIGHT('00' + CONVERT(VARCHAR(10), @max + n.rn), 3)
                         ELSE CONVERT(VARCHAR(10), @max + n.rn) END
    FROM dbo.tblStory s
    JOIN n ON n.StoryID = s.StoryID;
END
GO

/* 5. Kiểm tra kết quả */
SELECT StoryID, Title, PublicationDate, StoryCode, '#' + StoryCode AS HienThi
FROM dbo.tblStory
ORDER BY StoryCode;
GO
