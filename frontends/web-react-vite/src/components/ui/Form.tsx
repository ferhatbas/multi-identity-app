import type { ChangeEvent, InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react';
import { Checkbox } from 'primereact/checkbox';
import { Dropdown } from 'primereact/dropdown';
import { InputText } from 'primereact/inputtext';
import { InputTextarea } from 'primereact/inputtextarea';
import { ProgressSpinner } from 'primereact/progressspinner';
import { Tag } from 'primereact/tag';

interface FieldProps {
  label: string;
  error?: string;
  hint?: string;
  required?: boolean;
  children: ReactNode;
  wide?: boolean;
}

export function Field({ label, error, hint, required, children, wide }: FieldProps) {
  return (
    <label className={`field${wide ? ' field-wide' : ''}${error ? ' field-error' : ''}`}>
      {label && <span className="field-label">{label}{required && <b aria-hidden="true"> *</b>}</span>}
      {children}
      {error ? <span className="field-msg">{error}</span> : hint ? <span className="field-hint">{hint}</span> : null}
    </label>
  );
}

type TextFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, 'value'> & { value?: string; label: string; error?: string; hint?: string; wide?: boolean };

export function TextField({ label, error, hint, wide, required, ...input }: TextFieldProps) {
  return (
    <Field label={label} error={error} hint={hint} required={required} wide={wide}>
      <InputText required={required} invalid={!!error} {...input} />
    </Field>
  );
}

type AreaProps = Omit<TextareaHTMLAttributes<HTMLTextAreaElement>, 'value'> & { value?: string; label: string; error?: string; wide?: boolean };

export function TextArea({ label, error, wide, ...area }: AreaProps) {
  return (
    <Field label={label} error={error} wide={wide}>
      <InputTextarea rows={3} autoResize invalid={!!error} {...area} />
    </Field>
  );
}

type SelectFieldProps = Omit<SelectHTMLAttributes<HTMLSelectElement>, 'value'> & {
  label: string;
  error?: string;
  hint?: string;
  wide?: boolean;
  value?: string | number | null;
  options: { value: string | number; label: string }[];
  placeholder?: string;
};

export function SelectField({ label, error, hint, wide, options, placeholder, required, value, onChange, disabled }: SelectFieldProps) {
  const items = options.map(o => ({ label: o.label, value: String(o.value) }));
  if (placeholder !== undefined) items.unshift({ label: placeholder, value: '' });
  return (
    <Field label={label} error={error} hint={hint} required={required} wide={wide}>
      <Dropdown value={value === null || value === undefined ? '' : String(value)} options={items} optionLabel="label" optionValue="value"
                disabled={disabled} filter={items.length > 10} invalid={!!error} appendTo="self"
                onChange={e => onChange?.({ target: { value: String(e.value ?? '') } } as ChangeEvent<HTMLSelectElement>)} />
    </Field>
  );
}

export function CheckField({ label, checked, onChange, disabled, hint }: {
  label: string; checked: boolean; onChange: (checked: boolean) => void; disabled?: boolean; hint?: string;
}) {
  return (
    <label className="check">
      <Checkbox checked={checked} disabled={disabled} onChange={e => onChange(!!e.checked)} />
      <span>{label}{hint && <small>{hint}</small>}</span>
    </label>
  );
}

const SEVERITY = { success: 'success', warning: 'warning', danger: 'danger', info: 'info' } as const;

export function Badge({ tone = 'neutral', children }: { tone?: 'neutral' | 'success' | 'warning' | 'danger' | 'info'; children: ReactNode }) {
  return tone === 'neutral'
    ? <Tag className="p-tag-neutral" rounded>{children}</Tag>
    : <Tag severity={SEVERITY[tone]} rounded>{children}</Tag>;
}

export function Spinner({ label }: { label?: string }) {
  return (
    <div className="spinner-row">
      <ProgressSpinner style={{ width: 22, height: 22 }} strokeWidth="6" />
      {label && <span>{label}</span>}
    </div>
  );
}

export function EmptyState({ title, text }: { title: string; text?: string }) {
  return <div className="empty"><strong>{title}</strong>{text && <p>{text}</p>}</div>;
}

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: string; actions?: ReactNode }) {
  return (
    <div className="page-head">
      <div>
        <h1>{title}</h1>
        {subtitle && <p className="muted-text">{subtitle}</p>}
      </div>
      {actions && <div className="page-actions">{actions}</div>}
    </div>
  );
}
