using System.Web.Mvc;

namespace DemoLegacyApi.Controllers
{
	public class HomeController : Controller
	{
		public ActionResult Index()
		{
			ViewBag.Title = "Propel Feature Flags - .NET Framework 4.8.1 Demo";
			ViewBag.Message = "This demo showcases Propel Feature Flags running on .NET Framework 4.8.1 without dependency injection.";

			return View();
		}
	}
}
