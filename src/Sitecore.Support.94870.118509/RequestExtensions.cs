using System;
using System.Linq;
using System.Web;

namespace Sitecore.Support.ItemWebApi.Extensions
{
    internal static class RequestExtensions
    {
        private static readonly string[] Separator = new string[]
        {
            ","
        };

        internal static bool HasQueryStringFlag(this HttpRequest request, string parameterName)
        {
            string queryString = Sitecore.Web.WebUtil.GetQueryString(parameterName, null);
            if (queryString != null)
            {
                return true;
            }
            string text = request.QueryString[null];
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }
            string[] source = text.Split(RequestExtensions.Separator, StringSplitOptions.RemoveEmptyEntries);
            return source.Contains(parameterName);
        }
    }
}