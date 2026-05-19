interface JsonViewerProps {
  value: string | null | undefined;
}

export default function JsonViewer({ value }: JsonViewerProps) {
  const formatted = formatJson(value);
  return (
    <pre className="max-h-[60vh] overflow-auto rounded-xl bg-zinc-950 p-4 text-xs leading-5 text-zinc-100">
      {formatted.split('\n').map((line, index) => (
        <div key={index}>{highlightLine(line)}</div>
      ))}
    </pre>
  );
}

function formatJson(value: string | null | undefined): string {
  if (!value) return '{}';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

function highlightLine(line: string) {
  const match = /^(\s*)"([^"]+)":\s?(.*)$/.exec(line);
  if (!match) return <span className="text-zinc-300">{line}</span>;
  return (
    <>
      <span>{match[1]}</span>
      <span className="text-sky-300">"{match[2]}"</span>
      <span className="text-zinc-500">: </span>
      <span className={valueClass(match[3])}>{match[3]}</span>
    </>
  );
}

function valueClass(value: string) {
  if (value.startsWith('"')) return 'text-emerald-300';
  if (value === 'true' || value === 'false') return 'text-amber-300';
  if (value === 'null') return 'text-zinc-500';
  if (/^-?\d/.test(value)) return 'text-violet-300';
  return 'text-zinc-300';
}
