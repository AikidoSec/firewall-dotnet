using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.DotNetCore
{
    public class Zen
    {
        public static void SetUser(string id, string name, HttpContext context)
        {
            var user = new User(id, name);
            context.Items["Aikido.Zen.CurrentUser"] = user;
        }
    }
}
