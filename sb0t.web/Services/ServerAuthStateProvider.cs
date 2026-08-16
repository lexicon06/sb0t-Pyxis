using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Http;

namespace sb0t.web.Services
{
    public class ServerAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ServerService _server;

        public ServerAuthStateProvider(IHttpContextAccessor httpContextAccessor, ServerService server)
        {
            _httpContextAccessor = httpContextAccessor;
            _server = server;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var user = _httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();
            return Task.FromResult(new AuthenticationState(user));
        }

        public async Task Login(string password)
        {
            if (!_server.CheckPassword(password))
                return;

            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "owner") }, "sb0t");
            var principal = new ClaimsPrincipal(identity);

            var http = _httpContextAccessor.HttpContext;
            if (http != null)
                await http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(principal)));
        }

        public async Task Logout()
        {
            var http = _httpContextAccessor.HttpContext;
            if (http != null)
                await http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(new ClaimsPrincipal())));
        }
    }
}
