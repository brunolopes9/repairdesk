import LegalLayout, { Section } from './LegalLayout';

export default function Cookies() {
  return (
    <LegalLayout title="Política de Cookies" lastUpdated="2026-05-17">
      <p>
        Esta política explica como a LopesTech usa cookies e tecnologias semelhantes no site e no RepairDesk.
      </p>

      <Section title="1. O que são cookies">
        <p>
          Cookies são pequenos ficheiros guardados no teu dispositivo pelo browser.
          Podem servir para manter a sessão iniciada, guardar preferências, medir
          utilização do site ou apoiar marketing.
        </p>
      </Section>

      <Section title="2. Que tipos de cookies usamos">
        <div className="overflow-x-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
          <table className="w-full text-left text-sm">
            <thead className="bg-zinc-50 text-xs uppercase tracking-wide text-zinc-500 dark:bg-zinc-900">
              <tr>
                <th className="px-3 py-2">Tipo</th>
                <th className="px-3 py-2">Para que servem</th>
                <th className="px-3 py-2">Consentimento</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              <tr>
                <td className="px-3 py-2 font-medium">Essenciais</td>
                <td className="px-3 py-2">Login, segurança, sessão, preferências técnicas, protecção contra abuso</td>
                <td className="px-3 py-2">Não exigem consentimento</td>
              </tr>
              <tr>
                <td className="px-3 py-2 font-medium">Analítica</td>
                <td className="px-3 py-2">Perceber páginas visitadas, erros, funis e uso agregado</td>
                <td className="px-3 py-2">Exigem consentimento antes de activar</td>
              </tr>
              <tr>
                <td className="px-3 py-2 font-medium">Marketing</td>
                <td className="px-3 py-2">Medir campanhas, remarketing, pixels de publicidade</td>
                <td className="px-3 py-2">Exigem consentimento antes de activar</td>
              </tr>
            </tbody>
          </table>
        </div>
      </Section>

      <Section title="3. Cookies essenciais">
        <p>
          São necessários para o site/app funcionar. Sem estes cookies, não seria possível
          iniciar sessão, manter segurança ou guardar certas preferências.
        </p>
        <p>Exemplos:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>cookie de sessão / autenticação</li>
          <li>preferência de idioma / tema</li>
          <li>cookie de consentimento</li>
          <li>protecção anti-CSRF ou antifraude, se aplicável</li>
        </ul>
      </Section>

      <Section title="4. Cookies de analítica">
        <p>
          Só serão usados se deres consentimento. Servem para perceber como o site é usado e
          melhorar o produto. Damos preferência a soluções privacy-friendly (Plausible / Umami
          self-hosted) ou sem cookies quando possível.
        </p>
      </Section>

      <Section title="5. Cookies de marketing">
        <p>
          Só serão usados se deres consentimento. Servem para medir campanhas ou publicidade.
          Estado actual: <strong>não usamos cookies de marketing</strong>.
        </p>
      </Section>

      <Section title="6. Como gerir preferências">
        <p>
          Podes aceitar, rejeitar ou alterar preferências de cookies não essenciais a qualquer
          momento através das definições do teu browser. Para mais informação sobre como
          processamos dados pessoais, consulta a <a href="/privacidade" className="text-brand-600 hover:underline dark:text-brand-400">Política de Privacidade</a>.
        </p>
      </Section>

      <Section title="Contacto">
        <p>
          Para questões sobre cookies ou dados pessoais escreve para{' '}
          <a href="mailto:privacidade@lopestech.pt" className="text-brand-600 hover:underline dark:text-brand-400">
            privacidade@lopestech.pt
          </a>.
        </p>
      </Section>
    </LegalLayout>
  );
}
