using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WEBTechnologies_Final.Services
{
    // A custom "action filter" used as an attribute: put [AdminOnly] on a controller or
    // action and this code runs before that action, blocking non-admins. Filters are the
    // idiomatic MVC way to handle cross-cutting concerns like authorization in one place
    // instead of repeating the same check at the top of every admin action.
    public class AdminOnlyAttribute : ActionFilterAttribute
    {
        // OnActionExecuting runs just BEFORE the action method. If we set context.Result here,
        // MVC short-circuits: the action never runs and our result is returned instead.
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // Admin status was saved in Session at login as the string "true".
            var isAdmin = context.HttpContext.Session.GetString(SessionKeys.IsAdmin) == "true";
            if (!isAdmin)
            {
                // Not an admin -> redirect to the login page, passing the page they tried to
                // reach as returnUrl so they can be sent back after logging in.
                context.Result = new RedirectToActionResult(
                    "Login", "Account",
                    new { returnUrl = context.HttpContext.Request.Path });
            }

            // If isAdmin was true we set no Result, so execution continues to the action.
            base.OnActionExecuting(context);
        }
    }
}
