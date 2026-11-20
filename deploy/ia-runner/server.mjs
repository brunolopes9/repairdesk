// Sprint 667: serviço interno (só na rede do docker) que corre um pedido no Claude pela subscrição.
// POST /v1/texto  { system, prompt, model? }  →  { text }      cabeçalho X-Ia-Secret obrigatório.
// POST /v1/imagem { system, prompt, imagem (base64), mime?, model? } → { text }  (20/11: fotos de faturas em papel —
//   a imagem vai para /tmp e o Claude lê-a com a ferramenta Read; nunca pela API de créditos).
import http from 'node:http';
import { spawn } from 'node:child_process';
import { writeFileSync, unlinkSync } from 'node:fs';
import { randomUUID } from 'node:crypto';

const SECRET = process.env.IA_RUNNER_SECRET || '';
const TOKEN = process.env.CLAUDE_CODE_OAUTH_TOKEN || '';
let fila = Promise.resolve(); // um pedido de cada vez (a subscrição tem limites por janela)

function correr({ system, prompt, model, ficheiro }) {
  return new Promise((resolve, reject) => {
    const args = ['-p', '--model', model || 'haiku', '--output-format', 'text', '--max-turns', ficheiro ? '3' : '1'];
    if (ficheiro) args.push('--allowedTools', 'Read');
    if (system) args.push('--append-system-prompt', system);
    const env = { ...process.env, CLAUDE_CODE_OAUTH_TOKEN: TOKEN };
    delete env.ANTHROPIC_API_KEY; // nunca créditos da API
    const p = spawn('claude', args, { env, cwd: '/tmp' });
    let out = '', err = '';
    const t = setTimeout(() => { p.kill('SIGKILL'); reject(new Error('timeout')); }, ficheiro ? 240_000 : 120_000);
    p.stdout.on('data', (d) => { out += d; });
    p.stderr.on('data', (d) => { err += d; });
    p.on('close', (code) => { clearTimeout(t); code === 0 ? resolve(out.trim()) : reject(new Error(`claude saiu com ${code}: ${err.slice(0, 400)}`)); });
    p.stdin.end(prompt);
  });
}

http.createServer((req, res) => {
  const json = (s, o) => { res.writeHead(s, { 'Content-Type': 'application/json' }); res.end(JSON.stringify(o)); };
  if (req.method === 'GET' && req.url === '/health') return json(200, { ok: true, token: !!TOKEN });
  const imagem = req.url === '/v1/imagem';
  if (req.method !== 'POST' || (req.url !== '/v1/texto' && !imagem)) return json(404, { erro: 'nao_existe' });
  if (!SECRET || req.headers['x-ia-secret'] !== SECRET) return json(401, { erro: 'sem_autorizacao' });
  if (!TOKEN) return json(503, { erro: 'sem_token', detalhe: 'Falta CLAUDE_CODE_OAUTH_TOKEN (claude setup-token).' });
  let corpo = '';
  req.on('data', (d) => { corpo += d; if (corpo.length > (imagem ? 12_000_000 : 2_000_000)) req.destroy(); });
  req.on('end', () => {
    let pedido;
    try { pedido = JSON.parse(corpo); } catch { return json(400, { erro: 'json_invalido' }); }
    if (!pedido.prompt) return json(400, { erro: 'sem_prompt' });
    let ficheiro = null;
    if (imagem) {
      if (!pedido.imagem) return json(400, { erro: 'sem_imagem' });
      const ext = ({ 'image/png': 'png', 'image/webp': 'webp', 'image/gif': 'gif' })[pedido.mime] || 'jpg';
      ficheiro = `/tmp/fatura-${randomUUID()}.${ext}`;
      writeFileSync(ficheiro, Buffer.from(pedido.imagem, 'base64'));
      pedido.prompt = `Lê a imagem ${ficheiro} com a ferramenta Read.

${pedido.prompt}`;
    }
    const tarefa = fila.then(() => correr({ ...pedido, ficheiro })).finally(() => { if (ficheiro) try { unlinkSync(ficheiro); } catch {} });
    fila = tarefa.catch(() => {});
    tarefa.then((text) => json(200, { text })).catch((e) => json(502, { erro: 'claude_falhou', detalhe: String(e.message || e) }));
  });
}).listen(8090, () => console.log('ia-runner na porta 8090'));
