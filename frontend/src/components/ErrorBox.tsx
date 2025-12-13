type Props = {
  errors: string[];
};

export function ErrorBox({ errors }: Props) {
  if (!errors.length) return null;
  return (
    <div className="error-box">
      {errors.map((e, idx) => (
        <div key={`${e}-${idx}`}>{e}</div>
      ))}
    </div>
  );
}
