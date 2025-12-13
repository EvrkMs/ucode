import { LeaderboardItem } from "../../types";

type Props = {
  redeemCode: string;
  onRedeemCodeChange: (value: string) => void;
  onRedeem: () => void;
  busy: boolean;
  leaderboard: LeaderboardItem[];
  wsConnected?: boolean;
};

export function ClientPage({ redeemCode, onRedeemCodeChange, onRedeem, busy, leaderboard, wsConnected = true }: Props) {
  return (
    <>
      <section className="card">
        <h2>Клиент</h2>
        <div className="actions column">
          <input
            type="text"
            value={redeemCode}
            onChange={(e) => onRedeemCodeChange(e.target.value.toUpperCase())}
            placeholder="Введите код"
          />
          <button onClick={onRedeem} disabled={busy || !redeemCode.trim()}>
            Активировать код
          </button>
        </div>
      </section>

      <section className="card">
        <div className="card-header">
          <h2>Топ 100 по баллам</h2>
          <div
            className={`ws-indicator ${wsConnected ? "connected" : "disconnected"}`}
            title={wsConnected ? "WS подключен" : "WS нет подключения"}
          />
        </div>
        {leaderboard.length === 0 ? (
          <div className="muted">Пока нет данных.</div>
        ) : (
          <ul className="list">
            {leaderboard.map((item) => (
              <li key={item.telegramId} className="list-item">
                {item.photoUrl && <img src={item.photoUrl} alt="" width={40} height={40} />}
                <div className="list-text">
                  <div className="user-name">
                    {item.username ? `@${item.username}` : `${item.firstName ?? ""} ${item.lastName ?? ""}`.trim()}
                  </div>
                  <div className="muted">Баланс: {item.balance}</div>
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>
    </>
  );
}
