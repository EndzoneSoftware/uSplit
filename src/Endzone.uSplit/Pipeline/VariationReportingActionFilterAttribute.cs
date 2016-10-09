using System;
using System.Web;
using System.Web.Mvc;
using Endzone.uSplit.Models;

namespace Endzone.uSplit.Pipeline
{
    public class VariationReportingActionFilterAttribute : ActionFilterAttribute
    {
        private const string FilteringDoesntWork =
            "uSplit cannot insert a required JS fragment (to report chosen A/B testing variation) to your page " +
            "automatically. Add a call to @Html.RenderAbTestingScriptTags() into your master layout " +
            "to manually include this JS snippet and suppress this error.";

        public override void OnActionExecuted(ActionExecutedContext filterContext)
        {
            //ignore child actions, cannot use filter with them
            if (filterContext.IsChildAction)
                return;

            var httpContext = filterContext.HttpContext;
            var umbracoContext = httpContext.GetUmbracoContext();
            var request = umbracoContext.PublishedContentRequest;
            var response = httpContext.Response;

            if (response.ContentType != "text/html")
                return; //we only know how to report from JavaScript, so we need to be serving an HTML page

            var variedContent = request?.PublishedContent as VariedContent;
            if (variedContent == null)
                return; //this is not a variation, not part of an experiment

            Func<bool> scriptsNeeded =
                () => Equals(filterContext.HttpContext.Items[Constants.VariationReportedHttpContextItemsKey], true);

            try
            {
                response.Filter = new VariationReportingHttpResponseFilter(response.Filter, variedContent, scriptsNeeded);
            }
            catch (HttpException)
            {
                //There are instances where filtering simply doesn't work
                //https://github.com/EndzoneSoftware/uSplit/issues/4

                if (!filterContext.HttpContext.Items.Contains(Constants.VariationReportedHttpContextItemsKey))
                    filterContext.HttpContext.Items[Constants.VariationReportedHttpContextItemsKey] = false;
            }
        }

        public override void OnResultExecuted(ResultExecutedContext filterContext)
        {
            var variationReported = (bool) (filterContext.HttpContext.Items[Constants.VariationReportedHttpContextItemsKey] ?? true);

            if (!variationReported)
                throw new NotSupportedException(FilteringDoesntWork);
        }
    }
}