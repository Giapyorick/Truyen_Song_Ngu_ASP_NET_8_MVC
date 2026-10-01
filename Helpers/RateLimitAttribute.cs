using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Net;

namespace WebTruyenTranh.Helpers
{
    [AttributeUsage(AttributeTargets.Method)]
    public class RateLimitAttribute : ActionFilterAttribute
    {
        private readonly int _maxRequests;
        private readonly int _timeWindowInSeconds;
        private readonly string _actionKey;

        /// <summary>
        /// Giới hạn số lần gọi Action
        /// </summary>
        /// <param name="actionKey">Tên định danh Action (ví dụ: UserLogin, UserRegister, AdminLogin)</param>
        /// <param name="maxRequests">Số lần tối đa được gửi trong khoảng thời gian</param>
        /// <param name="timeWindowInSeconds">Khoảng thời gian (giây)</param>
        public RateLimitAttribute(string actionKey, int maxRequests = 5, int timeWindowInSeconds = 60)
        {
            _actionKey = actionKey;
            _maxRequests = maxRequests;
            _timeWindowInSeconds = timeWindowInSeconds;
        }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var memoryCache = context.HttpContext.RequestServices.GetService(typeof(IMemoryCache)) as IMemoryCache;
            if (memoryCache == null)
            {
                base.OnActionExecuting(context);
                return;
            }

            // Lấy địa chỉ IP người gửi (hỗ trợ cả khi chạy sau reverse proxy Cloudflare/Nginx)
            var ip = context.HttpContext.Request.Headers["X-Forwarded-For"].ToString();
            if (string.IsNullOrWhiteSpace(ip))
            {
                ip = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown_ip";
            }
            else
            {
                ip = ip.Split(',')[0].Trim();
            }

            var cacheKey = $"RateLimit_{_actionKey}_{ip}";

            // Kiểm tra số lần đã gọi trong MemoryCache
            if (memoryCache.TryGetValue(cacheKey, out int currentCount))
            {
                if (currentCount >= _maxRequests)
                {
                    // Vượt quá giới hạn -> Chặn ngay lập tức
                    context.HttpContext.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;

                    // Nếu là request AJAX trả JSON
                    if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                        context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json"))
                    {
                        context.Result = new JsonResult(new
                        {
                            success = false,
                            message = $"Too many attempts! Please wait {_timeWindowInSeconds} seconds before trying again."
                        });
                    }
                    else
                    {
                        // Nếu là form POST truyền thống (như Admin)
                        var controller = context.Controller as Controller;
                        if (controller != null)
                        {
                            controller.ModelState.AddModelError(string.Empty, $"Too many requests. Please wait {_timeWindowInSeconds} seconds before trying again.");
                            context.Result = new ViewResult
                            {
                                ViewName = context.ActionDescriptor.RouteValues["action"],
                                ViewData = controller.ViewData
                            };
                        }
                        else
                        {
                            context.Result = new ContentResult
                            {
                                StatusCode = 429,
                                Content = "Too many requests. Please try again later."
                            };
                        }
                    }
                    return;
                }

                memoryCache.Set(cacheKey, currentCount + 1, TimeSpan.FromSeconds(_timeWindowInSeconds));
            }
            else
            {
                memoryCache.Set(cacheKey, 1, TimeSpan.FromSeconds(_timeWindowInSeconds));
            }

            base.OnActionExecuting(context);
        }
    }
}