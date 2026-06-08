using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using WEBTechnologies_Final.Models;

namespace WEBTechnologies_Final.Controllers
{
    // The default template controller for the static/extra pages: the marketing home page,
    // the privacy page, and the shared error page. (The app's real landing page is the car
    // catalogue in CarsController — see the default route in Program.cs.)
    public class HomeController : Controller
    {
        // GET /Home or /Home/Index — renders Views/Home/Index.cshtml.
        public IActionResult Index()
        {
            return View();
        }

        // GET /Home/Privacy — the privacy page.
        public IActionResult Privacy()
        {
            return View();
        }

        // The error handler. When the app is not in Development, the exception-handling
        // middleware (configured in Program.cs) forwards unhandled errors here.
        // [ResponseCache(NoStore = true, ...)] prevents the error page from being cached, so
        // a stale error is never shown in place of a later successful page.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Capture a correlation id for this request so it can be matched to server logs.
            // Activity.Current?.Id is the distributed-tracing id when available; otherwise we
            // fall back to the per-request TraceIdentifier.
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
