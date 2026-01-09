import { useMemo } from "react";
import { LeaderboardItem } from "../types";

type Props = {
  leaderboard: LeaderboardItem[];
  wsConnected: boolean;
};

export function ParticipantsPage({ leaderboard, wsConnected }: Props) {
  const shuffledParticipants = useMemo(() => {
    const participants: (LeaderboardItem & { uniqueId: string })[] = [];
    
    // Повторяем каждого по балансу
    leaderboard.forEach((item) => {
      for (let i = 0; i < Math.max(1, item.balance); i++) {
        participants.push({ 
          ...item, 
          uniqueId: `${item.telegramId}-${i}-${Date.now()}-${Math.random().toString(36)}`
        });
      }
    });
    
    // Fisher-Yates shuffle - РАНДОМ КАЖДЫЙ РАЗ
    for (let i = participants.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [participants[i], participants[j]] = [participants[j], participants[i]];
    }
    
    return participants;
  }, [leaderboard]); // Меняем при обновлении лидерборда

  return (
    <section className="card">
      <div className="card-header">
        <h2>Участники акции</h2>
        <div
          className={`ws-indicator ${wsConnected ? "connected" : "disconnected"}`}
          title={wsConnected ? "WS подключен (обновления в реальном времени)" : "WS отключен"}
        />
      </div>
      <div className="muted">
        {shuffledParticipants.length} записей • Обновлено: {new Date().toLocaleString()}
      </div>
      
      {shuffledParticipants.length === 0 ? (
        <div className="muted">Пока нет участников с баллами 🍬</div>
      ) : (
        <ul className="list" style={{ maxHeight: "70vh", overflow: "auto" }}>
          {shuffledParticipants.map((item, index) => (
            <li key={item.uniqueId} className="list-item">
              <div 
                className="muted" 
                style={{ 
                  minWidth: "45px", 
                  fontWeight: "bold", 
                  fontSize: "0.95em",
                  color: "#666"
                }}
              >
                {index + 1}.
              </div>
              {item.photoUrl && (
                <img 
                  src={item.photoUrl} 
                  alt="" 
                  width={42} 
                  height={42}
                  style={{ borderRadius: "50%" }}
                />
              )}
              <div className="list-text">
                <div className="user-name">
                  {item.username 
                    ? `@${item.username}` 
                    : `${item.firstName ?? ""} ${item.lastName ?? ""}`.trim() || "Без имени"
                  }
                </div>
                <div className="muted">🍬 {item.balance}</div>
              </div>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
