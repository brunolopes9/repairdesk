#!/usr/bin/env node
// Sprint 394 (Doc 04): converte o dump aberto Osmocom TAC DB (CC-BY-SA) para o CSV
// `tac;marca;modelo` que o Mender importa em Definições → Automações → "Auto-detetar modelo por IMEI".
//
// Uso:
//   1) descarregar:  curl -o tacdb-raw.csv http://tacdb.osmocom.org/export/tacdb.csv
//   2) converter:    node scripts/convert-tac-osmocom.mjs tacdb-raw.csv tac-mender.csv
//   3) importar tac-mender.csv pelo card no Mender.
//
// Formato Osmocom: linha 1 = aviso de licença; linha 2 = header "tac,name,name,contributor,...".
// Só usamos as 3 primeiras colunas (tac, marca, modelo). Parsing CSV com aspas tratado.

import { readFileSync, writeFileSync } from 'node:fs';

const [, , inPath, outPath = 'tac-mender.csv'] = process.argv;
if (!inPath) {
  console.error('Uso: node convert-tac-osmocom.mjs <tacdb-raw.csv> [saida.csv]');
  process.exit(1);
}

// Parser CSV mínimo (aspas + vírgulas dentro de aspas) — devolve array de campos.
function parseCsvLine(line) {
  const out = [];
  let field = '';
  let inQuotes = false;
  for (let i = 0; i < line.length; i++) {
    const c = line[i];
    if (inQuotes) {
      if (c === '"' && line[i + 1] === '"') { field += '"'; i++; }
      else if (c === '"') inQuotes = false;
      else field += c;
    } else if (c === '"') inQuotes = true;
    else if (c === ',') { out.push(field); field = ''; }
    else field += c;
  }
  out.push(field);
  return out;
}

const raw = readFileSync(inPath, 'utf8').split(/\r?\n/);
const seen = new Set();
const rows = [];
for (const line of raw) {
  if (!line.trim()) continue;
  const f = parseCsvLine(line);
  const tac = (f[0] || '').replace(/\D/g, '');
  if (tac.length !== 8) continue;            // salta cabeçalhos/avisos
  const brand = (f[1] || '').trim();
  const model = (f[2] || '').trim();
  if (!brand && !model) continue;
  if (seen.has(tac)) continue;               // 1ª ocorrência ganha
  seen.add(tac);
  // Limpa ';' nos campos para não quebrar o formato de saída.
  rows.push(`${tac};${brand.replace(/;/g, ',')};${model.replace(/;/g, ',')}`);
}

writeFileSync(outPath, 'tac;marca;modelo\n' + rows.join('\n') + '\n', 'utf8');
console.log(`OK — ${rows.length} TACs escritos em ${outPath}`);
