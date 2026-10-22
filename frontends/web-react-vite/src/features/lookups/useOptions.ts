import { useEffect, useState } from 'react';

export interface Option { value: number; label: string }

export function useOptions<T>(load: () => Promise<T[]>, toOption: (row: T) => Option): { options: Option[]; rows: T[]; loading: boolean } {
  const [rows, setRows] = useState<T[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let live = true;
    load().then(r => { if (live) setRows(r); }).catch(() => { if (live) setRows([]); }).finally(() => { if (live) setLoading(false); });
    // The loader is a stable module-level function in every caller.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { options: rows.map(toOption), rows, loading };
}
