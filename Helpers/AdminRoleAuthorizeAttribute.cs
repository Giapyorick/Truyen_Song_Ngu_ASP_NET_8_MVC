using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebTruyenTranh.Helpers
{
    public class AdminRoleAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var role = context.HttpContext.Session.GetString("AdminRole");

            // Nếu không phải quyền Admin (ví dụ là Viewer) thì chặn lại ngay
            if (!string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
            {
                // Nếu là request AJAX trả về JSON thông báo lỗi
                if (context.HttpContext.Request.Headers["X-Requested-With"] == "XMLHttpRequest" ||
                    context.HttpContext.Request.Headers.Accept.ToString().Contains("application/json"))
                {
                    context.Result = new JsonResult(new
                    {
                        success = false,
                        message = "You have VIEWER (read-only) access. You are not permitted to add, edit, delete, or save data!"
                    });
                }
                else
                {
                    context.Result = new ForbidResult();
                }
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}