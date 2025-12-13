import { CodeHistory } from "../../types";

type Props = {
  pointsToGenerate: number;
  onPointsChange: (value: number) => void;
  onGenerate: () => void;
  busy: boolean;
  history: CodeHistory[];
  wsConnected?: boolean;
};

export function AdminPage({ pointsToGenerate, onPointsChange, onGenerate, busy, history, wsConnected = true }: Props) {
  return (
    <section className="card">
      <div className="card-header">
        <h2>Админ: генерация кода</h2>
        <div
          className={`ws-indicator ${wsConnected ? "connected" : "disconnected"}`}
          title={wsConnected ? "WS подключен" : "WS нет подключения"}
        />
      </div>
      <div className="actions">
        <input
          type="number"
          value={pointsToGenerate}
          onChange={(e) => onPointsChange(parseInt(e.target.value, 10))}
          min={1}
        />
        <button onClick={onGenerate} disabled={busy || pointsToGenerate <= 0}>
          Сгенерировать
        </button>
      </div>
      <h3>История</h3>
      {history.length === 0 ? (
        <div className="muted">Пока нет кодов.</div>
      ) : (
        <ul className="list">
          {history.map((c) => (
            <li key={c.id} className="list-item">
              <div className="list-text">
                <div className="user-name">
                  {c.value} (+{c.points})
                </div>
                <div className="muted">
                  истекает: {new Date(c.expiresAt).toLocaleString()} |{" "}
                  {c.used ? `использован (by ${c.usedBy ?? "?"})` : "не использован"}
                </div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
