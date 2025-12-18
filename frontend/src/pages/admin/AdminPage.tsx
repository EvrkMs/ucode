import { useState } from "react";
import { CodeHistory } from "../../types";

type Props = {
  pointsToGenerate: number;
  onPointsChange: (value: number) => void;
  onGenerate: () => void;
  busy: boolean;
  history: CodeHistory[];
  wsConnected?: boolean;
  isRoot?: boolean;
  rootSearchQuery?: string;
  onRootSearchChange?: (q: string) => void;
  onRootSearch?: () => void;
  rootResults?: Array<{
    telegramId: number;
    username?: string;
    firstName?: string;
    lastName?: string;
    isAdmin: boolean;
    isRoot: boolean;
  }>;
  onToggleAdmin?: (telegramId: number, current: boolean) => void;
  rootBusy?: boolean;
  rootError?: string | null;
};

export function AdminPage({
  pointsToGenerate,
  onPointsChange,
  onGenerate,
  busy,
  history,
  wsConnected = true,
  isRoot = false,
  rootSearchQuery = "",
  onRootSearchChange,
  onRootSearch,
  rootResults = [],
  onToggleAdmin,
  rootBusy = false,
  rootError = null
}: Props) {
  const [showAllHistory, setShowAllHistory] = useState(false);
  const visibleHistory = showAllHistory ? history : history.slice(0, 5);

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
        <>
          <ul className="list">
            {visibleHistory.map((c) => (
            <li key={c.id} className="list-item">
              <div className="list-text">
                <div className="user-name">
                  {c.value} (+{c.points})
                </div>
                <div className="muted">
                  {!c.used ? `истекает: ${new Date(c.expiresAt).toLocaleString()} | ` : ""}
                  {c.used ? (c.usedByTag ? `использован (${c.usedByTag})` : "использован") : "не использован"}
                </div>
              </div>
            </li>
            ))}
          </ul>
          {!showAllHistory && history.length > 5 ? (
            <button onClick={() => setShowAllHistory(true)} style={{ marginTop: 12 }}>
              Показать ещё
            </button>
          ) : null}
        </>
      )}

      {isRoot && (
        <>
          <h3 style={{ marginTop: 24 }}>Root: управление админами</h3>
          <div className="actions">
            <input
              type="text"
              placeholder="@username или id"
              value={rootSearchQuery}
              onChange={(e) => onRootSearchChange?.(e.target.value)}
              disabled={rootBusy}
            />
            <button onClick={onRootSearch} disabled={rootBusy || !rootSearchQuery.trim()}>
              Найти
            </button>
          </div>
          {rootError ? <div className="error-box">{rootError}</div> : null}
          {rootResults.length === 0 ? (
            <div className="muted">Нет результатов</div>
          ) : (
            <ul className="list">
              {rootResults.map((u) => (
                <li key={u.telegramId} className="list-item">
                  <div className="list-text">
                    <div className="user-name">
                      {u.username ? `@${u.username}` : u.telegramId} {u.isRoot ? "(root)" : null}
                    </div>
                    <div className="muted">
                      {u.firstName} {u.lastName}
                    </div>
                  </div>
                  <button
                    onClick={() => onToggleAdmin?.(u.telegramId, u.isAdmin)}
                    disabled={rootBusy || u.isRoot}
                  >
                    {u.isAdmin ? "Снять админа" : "Выдать админа"}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}
