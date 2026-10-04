using LogicCommon;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace MyBlazorApp.Components
{
    // 画面 (Components/Pages 配下) の基底クラス。Components/Pages/_Imports.razor の @inherits で全画面に適用する。
    // ユーザーの操作で始まったサーバー側の処理を SessionProcessingTracker に「処理中」として数え、
    // 実行中は自動ログアウトにしない (アイドル時間は処理が終わった時刻から数える)。対象は
    //  - イベント処理 (ボタンのクリックなど。MudBlazor の OnClick なども最終的にこの画面の HandleEventAsync を通る)。
    //    async の処理は await の先まで含め、終わるまで処理中になる
    //  - 画面を開いたときの読み込み (OnInitializedAsync / OnParametersSetAsync)
    // 各画面に処理中を記録するコードを書く必要はない
    public abstract class AppPageBase : ComponentBase, IHandleEvent
    {
        [Inject] private SessionProcessingTracker ProcessingTracker { get; set; } = default!;

        // ログインしていない画面 (ログイン画面など) では null
        [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }

        public override async Task SetParametersAsync(ParameterView parameters)
        {
            // 先に base を呼ぶまでは [CascadingParameter] が入っていないので、パラメータから直接取り出す
            if (AuthenticationStateTask == null &&
                parameters.TryGetValue(nameof(AuthenticationStateTask), out Task<AuthenticationState>? authTask))
            {
                AuthenticationStateTask = authTask;
            }

            using (await BeginProcessingAsync())
            {
                await base.SetParametersAsync(parameters);
            }
        }

        // ComponentBase の HandleEventAsync と同じく、イベントの後に自動で再描画する (処理中の記録を加えただけ)
        async Task IHandleEvent.HandleEventAsync(EventCallbackWorkItem callback, object? arg)
        {
            using (await BeginProcessingAsync())
            {
                Task task = callback.InvokeAsync(arg);
                bool shouldAwait = task.Status is not (TaskStatus.RanToCompletion or TaskStatus.Canceled);
                StateHasChanged();
                if (!shouldAwait)
                {
                    return;
                }

                try
                {
                    await task;
                }
                catch when (task.IsCanceled)
                {
                    // キャンセルされた処理は、ComponentBase と同じく何もしない
                    return;
                }
                StateHasChanged();
            }
        }

        private async Task<IDisposable?> BeginProcessingAsync()
        {
            if (AuthenticationStateTask == null)
            {
                return null;
            }
            AuthenticationState state = await AuthenticationStateTask;
            return Guid.TryParse(state.User.FindFirst(AuthConstants.SessionIdClaim)?.Value, out Guid sessionId)
                ? ProcessingTracker.Begin(sessionId)
                : null;
        }
    }
}
