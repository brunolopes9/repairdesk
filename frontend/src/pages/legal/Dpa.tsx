import LegalLayout, { Section } from './LegalLayout';

/**
 * Sprint 174: Data Processing Agreement (DPA) template em pt-PT.
 *
 * IMPORTANTE: este texto é um ponto de partida razoável baseado em práticas
 * standard (Notion, Stripe, Slack DPAs). NÃO é aconselhamento jurídico.
 * Bruno deve revê-lo com advogado RGPD PT antes de iniciar SaaS comercial.
 */
export default function Dpa() {
  return (
    <LegalLayout title="Contrato de Processamento de Dados (DPA)" lastUpdated="22 Maio 2026">
      <p>
        Este Contrato de Processamento de Dados ("DPA") forma parte integrante dos Termos do
        Mender entre o Cliente (Responsável pelo Tratamento) e a LopesTech (Subcontratante),
        nos termos do Regulamento (UE) 2016/679 (RGPD).
      </p>

      <Section title="1. Definições">
        <ul className="list-disc pl-6">
          <li><strong>Cliente / Tenant</strong>: a entidade que utiliza o Mender.</li>
          <li><strong>LopesTech</strong>: Bruno Lopes, sediada em Viseu, Portugal, fornecedor do Mender.</li>
          <li><strong>Dados Pessoais</strong>: qualquer informação relativa a pessoa singular identificada
          ou identificável processada no contexto do Mender.</li>
          <li><strong>Sub-processador</strong>: terceiro contratado pela LopesTech para apoiar o tratamento.</li>
        </ul>
      </Section>

      <Section title="2. Objecto e duração">
        <p>
          A LopesTech trata Dados Pessoais <strong>em nome do Cliente</strong> exclusivamente para
          fornecer as funcionalidades do Mender. O tratamento dura enquanto a conta estiver activa
          + 90 dias após terminação (retention de backups).
        </p>
      </Section>

      <Section title="3. Categorias de dados e titulares">
        <ul className="list-disc pl-6">
          <li><strong>Clientes finais do tenant</strong>: nome, contacto, IMEI/serial, histórico de reparações.</li>
          <li><strong>Fornecedores</strong>: dados de contacto, NIF, condições comerciais.</li>
          <li><strong>Documentos</strong>: facturas, recibos, fotografias de equipamentos, anexos email.</li>
          <li><strong>Utilizadores</strong>: nome, email, role, IP, user-agent (logs de auditoria).</li>
        </ul>
      </Section>

      <Section title="4. Obrigações da LopesTech">
        <ol className="list-decimal pl-6 space-y-2">
          <li>Tratar Dados Pessoais apenas conforme instruções documentadas do Cliente (estes Termos + DPA).</li>
          <li>Garantir confidencialidade dos colaboradores que acedam aos dados.</li>
          <li>Implementar medidas técnicas e organizativas adequadas (art. 32.º RGPD):
            <ul className="list-disc pl-6 mt-1">
              <li>Encriptação em trânsito (TLS 1.3) e em repouso (AES-256 para secrets).</li>
              <li>Isolamento multi-tenant (queries forçadas com TenantId, row-level security).</li>
              <li>Backups diários encriptados com retenção de 30 dias.</li>
              <li>Auditoria de acessos administrativos.</li>
              <li>Sentry para erros (com PII scrubbing).</li>
            </ul>
          </li>
          <li>Notificar o Cliente sem demora indevida (em 72h) em caso de violação de dados.</li>
          <li>Assistir o Cliente em pedidos de titulares (acesso, rectificação, portabilidade, apagamento).</li>
          <li>Apagar/devolver Dados Pessoais 90 dias após terminação do contrato.</li>
        </ol>
      </Section>

      <Section title="5. Sub-processadores">
        <p>
          A LopesTech recorre a sub-processadores listados em <a href="/sub-processors" className="text-brand-600 hover:underline dark:text-brand-400">/sub-processors</a>.
          O Cliente autoriza estes sub-processadores pela aceitação deste DPA.
        </p>
        <p>
          Novos sub-processadores são anunciados com 30 dias de antecedência. O Cliente pode opor-se;
          se não conseguirmos endereçar a oposição, o Cliente pode rescindir o contrato.
        </p>
      </Section>

      <Section title="6. Transferências internacionais">
        <p>
          Alguns sub-processadores (ex: Anthropic, Stripe) processam dados fora do EEE (Estados Unidos).
          Tais transferências são protegidas por <strong>Cláusulas Contratuais-Tipo</strong> (Decisão (UE)
          2021/914) e medidas suplementares (encriptação, pseudonimização).
        </p>
      </Section>

      <Section title="7. Direitos do Cliente">
        <ul className="list-disc pl-6">
          <li>Pedir auditoria razoável das medidas técnicas (uma vez por ano, ou em caso de incidente).</li>
          <li>Receber relatórios de conformidade quando disponíveis.</li>
          <li>Solicitar export completo de Dados Pessoais (JSON + ficheiros) a qualquer momento.</li>
          <li>Solicitar apagamento permanente (hard-delete) — RGPD art. 17.º.</li>
        </ul>
      </Section>

      <Section title="8. Inteligência Artificial (IA)">
        <p>
          O Cliente reconhece e consente que documentos enviados (PDFs, fotografias de facturas) podem
          ser processados por <strong>Anthropic Claude</strong> para extracção automática de dados
          (parsing, OCR). Aplica-se redacção de PII antes do envio quando tecnicamente possível.
        </p>
        <p>
          O Cliente pode optar por <strong>BYOK</strong> (Bring Your Own Key) em <code>/definicoes/uso-de-ia</code>,
          caso em que a Anthropic processa dados directamente para o Cliente (LopesTech deixa de ser
          Subcontratante para essa parte).
        </p>
      </Section>

      <Section title="9. Responsabilidade e indemnização">
        <p>
          Cada parte é responsável pelo cumprimento das suas obrigações sob o RGPD. A LopesTech responde
          por violações cometidas pela LopesTech ou sub-processadores; o Cliente responde por
          instruções ilegais que tenha dado.
        </p>
      </Section>

      <Section title="10. Contacto">
        <p>
          Para questões de tratamento de dados ou para exercer direitos: <a className="text-brand-600 dark:text-brand-400 hover:underline" href="mailto:privacidade@lopestech.pt">privacidade@lopestech.pt</a>.
        </p>
        <p className="text-xs text-zinc-500 mt-4">
          Autoridade de controlo competente em Portugal: <a href="https://www.cnpd.pt" target="_blank" rel="noreferrer" className="text-brand-600 dark:text-brand-400 hover:underline">CNPD (cnpd.pt)</a>.
        </p>
      </Section>
    </LegalLayout>
  );
}
