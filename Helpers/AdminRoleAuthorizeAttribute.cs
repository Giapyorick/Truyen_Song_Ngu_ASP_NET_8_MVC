using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using System;

namespace WebTruyenTranh.Helpers
{
    public class AdminRoleAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var httpContext = context.HttpContext;
            var session = httpContext.Session;
            var role = session.GetString("AdminRole")?.Trim() ?? "";

            // Kiểm tra quyền Admin
            bool isAllowed = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (!isAllowed)
            {
                // Lấy IStringLocalizer từ DI Container
                var localizer = httpContext.RequestServices.GetService<IStringLocalizer<SharedResource>>();

                // Đọc chuỗi theo Culture hiện tại tự động
                string message = localizer != null
                    ? localizer["AdminLayout_ViewerReadOnlyNotice"]
                    : "You have VIEWER (read-only) access. You are not permitted to add, edit, delete, or save data!";

                context.Result = new JsonResult(new
                {
                    success = false,
                    message = message
                });
                return;
            }

            base.OnActionExecuting(context);
        }
    }

    // Dummy class dùng làm Anchor cho IStringLocalizer<SharedResource>
    public class SharedResource
    {
    }
}