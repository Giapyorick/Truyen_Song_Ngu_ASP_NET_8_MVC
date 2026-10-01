using System;
using Microsoft.AspNetCore.Http;

namespace WebTruyenTranh.Helpers
{
    public static class CaptchaHelper
    {
        private static readonly Random _random = new Random();

        // Tạo câu hỏi toán ngẫu nhiên và lưu kết quả vào Session
        public static string GenerateMathCaptcha(ISession session, string sessionKey)
        {
            int num1 = _random.Next(1, 20);
            int num2 = _random.Next(1, 15);
            int result = num1 + num2;

            session.SetInt32(sessionKey, result);
            return $"{num1} + {num2} = ?";
        }

        // Xác thực câu trả lời
        public static bool ValidateCaptcha(ISession session, string sessionKey, string? userAnswer)
        {
            if (string.IsNullOrWhiteSpace(userAnswer)) return false;

            int? correctAnswer = session.GetInt32(sessionKey);
            if (!correctAnswer.HasValue) return false;

            // Xóa session captcha sau khi kiểm tra để tránh dùng lại (Replay attack)
            session.Remove(sessionKey);

            if (int.TryParse(userAnswer.Trim(), out int parsedAnswer))
            {
                return parsedAnswer == correctAnswer.Value;
            }

            return false;
        }
    }
}