using Microsoft.AspNetCore.Components.Server.Circuits;

namespace LogicCommon
{
    // circuit の開始時に、接続元の情報 (ClientConnectionInfo) の初期値を入れる。
    // 接続元の情報は MainLayout がプリレンダリングのときに取得して入れる。
    // プリレンダリングを経ずに circuit が始まった場合に備え、SignalR の接続のリクエストから取った値を先に入れておく
    // (MainLayout が取得できた場合はそちらで上書きされる)
    public sealed class ClientConnectionCircuitHandler : CircuitHandler
    {
        private readonly ClientConnectionInfo clientInfo;
        private readonly IHttpContextAccessor httpContextAccessor;

        public ClientConnectionCircuitHandler(ClientConnectionInfo clientInfo, IHttpContextAccessor httpContextAccessor)
        {
            this.clientInfo = clientInfo;
            this.httpContextAccessor = httpContextAccessor;
        }

        public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
        {
            HttpContext? httpContext = httpContextAccessor.HttpContext;
            if (httpContext != null && clientInfo.IpAddress == null)
            {
                clientInfo.IpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
                clientInfo.UserAgent = httpContext.Request.Headers.UserAgent.ToString();
            }
            return Task.CompletedTask;
        }
    }
}
