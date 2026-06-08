using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace WEBTechnologies_Final.Services
{
    // Like AdminOnlyAttribute, but only requires that SOMEONE is logged in (any user),
    // not specifically an admin. Apply [LoggedInOnly] to actions such as bidding or
    // favouriting that need an account but aren't admin-only.
    public class LoggedInOnlyAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // "Logged in" simply means a username exists in the session.
            var username = context.HttpContext.Session.GetString(SessionKeys.Username);
            if (string.IsNullOrEmpty(username))
            {
                // Build the return URL from the full path AND query string, so filters/search
                // parameters on the page they came from are preserved after login.
                var returnUrl = context.HttpContext.Request.Path + context.HttpContext.Request.QueryString;
                context.Result = new RedirectToActionResult(
                    "Login", "Account", new { returnUrl });
            }

            base.OnActionExecuting(context);
        }
    }
}
