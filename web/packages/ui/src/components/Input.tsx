import clsx from 'clsx';
import { forwardRef, type InputHTMLAttributes } from 'react';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, error, className, id, ...rest }, ref
) {
  return (
    <div>
      {label ? <label htmlFor={id} className="label">{label}</label> : null}
      <input ref={ref} id={id} className={clsx('input', className)} {...rest} />
      {error ? <p className="form-error">{error}</p> : null}
    </div>
  );
});
