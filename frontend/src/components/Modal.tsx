type ModalState = { title: string; message: string; type: "success" | "error" } | null;

type Props = {
  modal: ModalState;
  onClose: () => void;
};

export function Modal({ modal, onClose }: Props) {
  if (!modal) return null;
  return (
    <div className="modal-overlay" onClick={onClose}>
      <div className={`modal ${modal.type}`} onClick={(e) => e.stopPropagation()}>
        <div className="modal-header">
          <h3>{modal.title}</h3>
          <button className="modal-close" onClick={onClose} aria-label="Закрыть">
            ×
          </button>
        </div>
        <div className="modal-body">{modal.message}</div>
      </div>
    </div>
  );
}

export type { ModalState };
