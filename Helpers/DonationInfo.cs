using System;

namespace WebTruyenTranh.Helpers
{
    /// <summary>
    /// Thông tin nhận ủng hộ (nút trái tim trên header – Views/Shared/_DonateModalPartial.cshtml).
    /// Để trong CODE (biên dịch vào .dll) chứ không để trong appsettings.json hay file ảnh,
    /// nên muốn đổi phải sửa code + build + deploy lại – khó bị tráo hơn nhiều.
    /// Mã QR được vẽ sẵn (SVG nội tuyến trong view) từ chuỗi VietQR, không dùng file ảnh.
    /// </summary>
    public static class DonationInfo
    {
        public const string AccountName = "NGUYEN VAN GIAP";
        public const string AccountNumber = "100877385788";
        public const string BankName = "VietinBank";
        public const string BankBranch = "CN Nghệ An – Hội sở";

        /// <summary>Nội dung chuyển khoản gợi ý (không dấu, ≤ 25 ký tự) – đã gắn sẵn vào các mã QR có số tiền</summary>
        public const string TransferNote = "UNG HO BILINGUAL";

        /// <summary>
        /// Mã VietQR gốc của ngân hàng (chuẩn EMVCo/Napas, không kèm số tiền):
        /// BIN 970415 = VietinBank, STK 100877385788. Các mã có số tiền 10k/20k/50k/100k trong view
        /// được tạo từ chính chuỗi này (thêm tag 54 số tiền + tag 62 nội dung, tính lại CRC) và đã quét thử đúng.
        /// </summary>
        public const string VietQrPayload = "00020101021138560010A0000007270126000697041501121008773857880208QRIBFTTA53037045802VN6304377A";

        /// <summary>Kiểm tra chuỗi VietQR có đúng BIN VietinBank + số tài khoản trên không (lệch thì view ẩn QR)</summary>
        public static bool PayloadMatchesAccount()
            => VietQrPayload.Contains("970415", StringComparison.Ordinal)
               && VietQrPayload.Contains(AccountNumber, StringComparison.Ordinal);
    }
}
