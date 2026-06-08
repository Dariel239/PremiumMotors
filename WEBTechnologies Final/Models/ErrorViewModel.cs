namespace WEBTechnologies_Final.Models
{
    // The model for the generic error page (Views/Shared/Error.cshtml). This is part of the
    // default ASP.NET Core MVC template. It carries the current request's tracing id so a
    // user can quote it when reporting a problem and it can be matched to server logs.
    public class ErrorViewModel
    {
        // The request identifier (set by HomeController.Error from Activity.Current/HttpContext).
        public string? RequestId { get; set; }

        // Only show the id on the page when we actually have one.
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
