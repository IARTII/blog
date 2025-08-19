using Blogs.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Blogs.Filters
{
    public class CustomExceptionFilter : IExceptionFilter
    {
        public void OnException(ExceptionContext context)
        {
            var status = 500;
            var message = "Произошла ошибка.";

            if (context.Exception is CustomException ce)
            {
                status = ce.StatusCode;
                message = ce.Message;
            }

            var vr = new ViewResult
            {
                ViewName = "~/Views/Shared/Error.cshtml",
                ViewData = new Microsoft.AspNetCore.Mvc.ViewFeatures.ViewDataDictionary(
                    new Microsoft.AspNetCore.Mvc.ModelBinding.EmptyModelMetadataProvider(),
                    context.ModelState)
                {
                    ["Message"] = message
                }
            };

            context.Result = vr;
            context.HttpContext.Response.StatusCode = status;
            context.ExceptionHandled = true;
        }
    }
}
