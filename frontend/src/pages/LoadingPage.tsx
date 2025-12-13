type Props = {
  subtitle?: string;
};

export function LoadingPage({ subtitle }: Props) {
  return (
    <div className="loading-page">
      <div className="loading-card">
        <div className="spinner" aria-hidden />
        <h2>Загрузка</h2>
        <p>{subtitle ?? "Проводится авто-вход через Telegram..."}</p>
      </div>
    </div>
  );
}
