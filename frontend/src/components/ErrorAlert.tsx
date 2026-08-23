interface ErrorAlertProps {
  message: string;
}

export function ErrorAlert({ message }: Readonly<ErrorAlertProps>) {
  return (
    <p className="error" role="alert">
      {message}
    </p>
  );
}
