// 操作 (クリック・キー入力・マウスの移動・スクロールなど) を検知して、.NET (SessionMonitor.ReportActivity) に通知する。
// マウスの移動やスクロールは 1 秒に何十回も起きるので、通知は最大で interval ミリ秒に 1 回にまとめる
// (前回の通知から interval 以上たっていればすぐに、そうでなければ interval がたった時点でまとめて 1 回送る)。
// まとめて送るときは「最後の操作から何秒たったか」も送り、サーバーは送った時刻ではなく実際に操作した時刻を記録する
const EVENTS = ["mousemove", "mousedown", "pointerdown", "keydown", "wheel", "scroll", "touchstart"];

export function start(dotNetRef, interval) {
    let lastSent = 0;
    let lastActivity = 0;
    let timer = null;
    let lastMouseX = null;
    let lastMouseY = null;

    function send() {
        timer = null;
        lastSent = Date.now();
        const idleSeconds = Math.floor((lastSent - lastActivity) / 1000);
        // 接続が切れている間は送れない (再接続後の操作で、また送られる)
        dotNetRef.invokeMethodAsync("ReportActivity", idleSeconds).catch(() => { });
    }

    function onActivity(e) {
        if (e && e.type === "mousemove") {
            // ダイアログの表示などで画面が変わると、マウスを動かしていなくても mousemove が起きることがある。
            // 位置が変わったときだけを操作として数える
            if (e.screenX === lastMouseX && e.screenY === lastMouseY) {
                return;
            }
            lastMouseX = e.screenX;
            lastMouseY = e.screenY;
        }
        lastActivity = Date.now();
        schedule();
    }

    // 通知を予約する (前回の通知から interval 以上たっていればすぐに送る)
    function schedule() {
        if (timer !== null) {
            return;
        }
        const wait = lastSent + interval - Date.now();
        if (wait <= 0) {
            send();
        } else {
            timer = setTimeout(send, wait);
        }
    }

    // 画面の中のスクロール (表など) も拾うため、キャプチャ段階で受け取る。画面の動きを妨げないよう passive にする
    const options = { capture: true, passive: true };
    for (const name of EVENTS) {
        window.addEventListener(name, onActivity, options);
    }

    return {
        // ログアウト前の警告を出している間は間隔を短くし、操作したらすぐに延長されるようにする
        // (予約済みの通知は新しい間隔で予約し直す。操作の時刻は変えない)
        setInterval(value) {
            interval = value;
            if (timer !== null) {
                clearTimeout(timer);
                timer = null;
                schedule();
            }
        },
        stop() {
            for (const name of EVENTS) {
                window.removeEventListener(name, onActivity, options);
            }
            if (timer !== null) {
                clearTimeout(timer);
            }
        },
    };
}
