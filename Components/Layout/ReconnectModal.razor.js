// イベントハンドラーを登録する
const reconnectModal = document.getElementById("components-reconnect-modal");
reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);

const retryButton = document.getElementById("components-reconnect-button");
retryButton.addEventListener("click", retry);

const resumeButton = document.getElementById("components-resume-button");
resumeButton.addEventListener("click", resume);

// 既定の回数の再接続に失敗した後も、この間隔で再試行を続ける (プロキシのタイムアウトなどで切れた場合に、
// サーバーへ届くようになったら自動で戻れるように)。ログイン状態は Cookie と DB にあるので、再読み込みしても保たれる
const autoRetryIntervalMs = 10000;
let autoRetryTimer = null;

function scheduleAutoRetry() {
    if (autoRetryTimer === null) {
        autoRetryTimer = setTimeout(() => {
            autoRetryTimer = null;
            retry();
        }, autoRetryIntervalMs);
    }
}

function handleReconnectStateChanged(event) {
    if (event.detail.state === "show") {
        reconnectModal.showModal();
    } else if (event.detail.state === "hide") {
        reconnectModal.close();
    } else if (event.detail.state === "failed") {
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        scheduleAutoRetry();
    } else if (event.detail.state === "rejected") {
        location.reload();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);

    try {
        // Blazor.reconnect() は非同期に次の結果を返す:
        // - true: 再接続に成功
        // - false: サーバーには到達したが接続を拒否された (例: 不明な circuit ID)
        // - 例外: サーバーに到達できなかった (同期・非同期のどちらでも起こりうる)
        const successful = await Blazor.reconnect();
        if (!successful) {
            // サーバーには到達できたが、circuit はもう使えない。
            // circuit の再開を試み、だめならページを再読み込みしてすぐに使い続けられるようにする。
            const resumeSuccessful = await Blazor.resumeCircuit();
            if (!resumeSuccessful) {
                location.reload();
            } else {
                reconnectModal.close();
            }
        }
    } catch (err) {
        // 例外が発生した = 現在サーバーを利用できない
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
        scheduleAutoRetry();
    }
}

async function resume() {
    try {
        const successful = await Blazor.resumeCircuit();
        if (!successful) {
            location.reload();
        }
    } catch {
        reconnectModal.classList.replace("components-reconnect-paused", "components-reconnect-resume-failed");
    }
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}
