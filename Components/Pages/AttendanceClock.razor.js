// 勤怠画面の現在の日付と時刻を 1 秒ごとに更新する。
// サーバー側のタイマーで再描画すると毎秒 SignalR の通信が起きるので、ブラウザで表示する。
// ブラウザの時計がずれていても打刻の判定 (サーバーの時刻) と合うよう、開いた時点のサーバーとの差を足して表示する。
// 日本時間の日付が変わったら .NET (AttendanceClock.OnDateChanged) に通知し、その日の打刻状況を読み直させる
const dateFormat = new Intl.DateTimeFormat("ja-JP", {
    timeZone: "Asia/Tokyo", year: "numeric", month: "2-digit", day: "2-digit", weekday: "short"
});
const timeFormat = new Intl.DateTimeFormat("ja-JP", {
    timeZone: "Asia/Tokyo", hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: false
});
// 日付の比較用 (yyyy-MM-dd)
const keyFormat = new Intl.DateTimeFormat("en-CA", {
    timeZone: "Asia/Tokyo", year: "numeric", month: "2-digit", day: "2-digit"
});

export function start(dotNetRef, dateElement, timeElement, serverNowMs, workDate) {
    const offset = serverNowMs - Date.now();
    let currentDate = workDate;

    function tick() {
        const now = new Date(Date.now() + offset);
        dateElement.textContent = dateFormat.format(now);
        timeElement.textContent = timeFormat.format(now);

        const date = keyFormat.format(now);
        if (date !== currentDate) {
            currentDate = date;
            // 接続が切れている間は送れない (ボタンを押したときはサーバーの日付で判定するので問題ない)
            dotNetRef.invokeMethodAsync("OnDateChanged").catch(() => { });
        }
    }

    tick();
    const timer = setInterval(tick, 1000);
    return {
        dispose: () => clearInterval(timer)
    };
}
