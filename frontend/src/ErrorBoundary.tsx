import React from "react";

type Props = {
  children: React.ReactNode;
};

type State = {
  hasError: boolean;
  message?: string;
};

export class ErrorBoundary extends React.Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, message: error.message };
  }

  componentDidCatch(error: Error, info: React.ErrorInfo): void {
    // Логируем в консоль; при необходимости сюда можно добавить отправку на бэкенд
    console.error("React error boundary", error, info);
  }

  handleReload = () => {
    window.location.reload();
  };

  render() {
    if (this.state.hasError) {
      return (
        <div className="page">
          <div className="card">
            <h2>Что-то пошло не так</h2>
            <p className="muted">{this.state.message ?? "Неизвестная ошибка"}</p>
            <button onClick={this.handleReload}>Перезагрузить</button>
          </div>
        </div>
      );
    }

    return this.props.children;
  }
}
