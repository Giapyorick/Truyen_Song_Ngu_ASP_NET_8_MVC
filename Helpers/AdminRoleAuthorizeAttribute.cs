using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace WebTruyenTranh.Helpers
{
    public class AdminRoleAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var role = session.GetString("AdminRole")?.Trim() ?? "";

            // Kiểm tra: Cho phép cả "Admin", "Super Admin", "SuperAdmin"
            bool isAllowed = string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(role, "Super Admin", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);

            if (!isAllowed)
            {
                // Nếu đúng là Viewer hoặc role khác thì mới chặn
                context.Result = new JsonResult(new
                {
                    success = false,
                    message = "You have VIEWER (read-only) access. You are not permitted to add, edit, delete, or save data!"
                });
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}