using Aikido.Zen.Core;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.DotNetCore
{
    public class Zen
    {

        private static IServiceProvider _serviceProvider;

        public static void Initialize(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public static void SetUser(string id, string name, HttpContext context)
        {
            var user = new User(id, name);
            context.Items["Aikido.Zen.CurrentUser"] = user;
        }

        public static Context GetContext()
        {
            if (_serviceProvider == null)
            {
                return null;
            }
            var contextAccessor = _serviceProvider.GetService(typeof(ContextAccessor)) as ContextAccessor;
            return contextAccessor?.CurrentContext;
        }

        public static User GetUser()
        {
            if (_serviceProvider == null)
            {
                return null;
            }
            var contextAccessor = _serviceProvider.GetService(typeof(ContextAccessor)) as ContextAccessor;
            return contextAccessor?.CurrentUser;
        }
    }
}
