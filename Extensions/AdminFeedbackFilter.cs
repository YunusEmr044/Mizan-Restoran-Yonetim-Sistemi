using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Services;

namespace RestoranYonetim.Extensions;

// Admin panelinde: mesaj bırakmadan yönlenen POST işlemlerine başarı mesajı ekler;
// başka kayıtlarda kullanılan bir kaydın silinmesi/değiştirilmesi 500 yerine anlaşılır bir uyarıyla sonuçlanır.
public sealed class AdminFeedbackFilter : IResultFilter, IExceptionFilter
{
    private readonly ITempDataDictionaryFactory _tempData;

    public AdminFeedbackFilter(ITempDataDictionaryFactory tempData) => _tempData = tempData;

    private static bool Applies(ActionContext context) =>
        HttpMethods.IsPost(context.HttpContext.Request.Method)
        && string.Equals(context.RouteData.Values["area"] as string, "Admin", StringComparison.OrdinalIgnoreCase);

    private static string ActionName(ActionContext context) => context.RouteData.Values["action"] as string ?? string.Empty;

    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (!Applies(context) || context.Result is not (RedirectToActionResult or RedirectResult or LocalRedirectResult or RedirectToRouteResult))
        {
            return;
        }
        var tempData = _tempData.GetTempData(context.HttpContext);
        if (!tempData.ContainsKey("Info") && !tempData.ContainsKey("Error"))
        {
            tempData["Info"] = AdminFeedback.SuccessMessageFor(ActionName(context));
        }
    }

    public void OnResultExecuted(ResultExecutedContext context)
    {
    }

    public void OnException(ExceptionContext context)
    {
        if (!Applies(context) || context.Exception is not DbUpdateException || !AdminFeedback.IsForeignKeyViolation(context.Exception))
        {
            return;
        }
        var action = ActionName(context);
        _tempData.GetTempData(context.HttpContext)["Error"] = AdminFeedback.ForeignKeyMessage(action);
        var referer = context.HttpContext.Request.Headers.Referer.ToString();
        context.Result = !action.Contains("Delete", StringComparison.Ordinal)
                         && Uri.TryCreate(referer, UriKind.Absolute, out var uri)
                         && uri.Authority == context.HttpContext.Request.Host.Value
            ? new LocalRedirectResult(uri.PathAndQuery)
            : new RedirectToActionResult("Index", null, null);
        context.ExceptionHandled = true;
    }
}
