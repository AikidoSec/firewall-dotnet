using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.DotNetCore
{
    internal class ContextAccessor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        internal ContextAccessor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        internal Context CurrentContext => (Context)_httpContextAccessor.HttpContext?.Items["Aikido.Zen.Context"];
        internal User CurrentUser => (User)_httpContextAccessor.HttpContext?.Items["Aikido.Zen.CurrentUser"];
    }
}
