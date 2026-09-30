using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebTruyenTranh.Helpers
{
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var adminId = context.HttpContext.Session.GetInt32("AdminId");
            if (!adminId.HasValue || adminId.Value <= 0)
            {
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult("Login", "Auth", new { area = "Admin", returnUrl });
            }
            base.OnActionExecuting(context);
        }
    }
}