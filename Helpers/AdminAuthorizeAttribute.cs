using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WebTruyenTranh.Helpers
{
    public class AdminAuthorizeAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var adminId = context.HttpContext.Session.GetInt32("AdminId");
            if (adminId == null || adminId <= 0)
            {
                context.Result = new RedirectToActionResult("Login", "Auth", new { area = "Admin" });
            }
            base.OnActionExecuting(context);
        }
    }
}