import LegalLayout, { Section } from './LegalLayout';

export default function PoliticaPrivacidade() {
  return (
    <LegalLayout title="Política de Privacidade" lastUpdated="2026-05-17">
      <p>
        A LopesTech respeita a tua privacidade. Esta política explica que dados pessoais
        tratamos quando visitas o nosso site, nos contactas ou usas o Reparo enquanto
        utilizador de uma loja.
      </p>
      <p>
        O Reparo é um software de gestão para oficinas de reparação. Quando uma loja
        usa o Reparo para gerir os seus próprios clientes, <strong>a loja é a responsável
        pelo tratamento</strong> desses dados. A LopesTech trata esses dados como subcontratante,
        de acordo com as instruções da loja e com o Contrato de Processamento de Dados (DPA)
        aplicável.
      </p>

      <Section title="1. Quem somos">
        <p>O Reparo é desenvolvido pela LopesTech, projecto de Bruno Lopes, em Portugal.</p>
        <p>
          Contacto para privacidade e protecção de dados:{' '}
          <a href="mailto:privacidade@lopestech.pt" className="text-brand-600 hover:underline dark:text-brand-400">
            privacidade@lopestech.pt
          </a>
        </p>
        <p>
          Enquanto não existir encarregado de protecção de dados formalmente designado,
          este contacto é gerido por Bruno Lopes. Se no futuro for designado um Encarregado de
          Protecção de Dados, os contactos serão actualizados nesta página.
        </p>
      </Section>

      <Section title="2. Que dados recolhemos">
        <h3 className="font-medium text-zinc-900 dark:text-zinc-100">Visitantes do site</h3>
        <ul className="ml-5 list-disc space-y-1">
          <li>endereço IP, informação técnica do browser / dispositivo e páginas visitadas</li>
          <li>cookies essenciais para funcionamento do site</li>
          <li>cookies de analítica ou marketing apenas se forem activados e aceites</li>
        </ul>

        <h3 className="mt-4 font-medium text-zinc-900 dark:text-zinc-100">Pessoas que nos contactam</h3>
        <ul className="ml-5 list-disc space-y-1">
          <li>nome, email, telefone (se fornecido)</li>
          <li>empresa / loja</li>
          <li>mensagem enviada e histórico de contacto</li>
        </ul>

        <h3 className="mt-4 font-medium text-zinc-900 dark:text-zinc-100">Utilizadores do Reparo</h3>
        <ul className="ml-5 list-disc space-y-1">
          <li>nome, email</li>
          <li>palavra-passe em formato protegido (hash)</li>
          <li>loja / empresa a que pertencem</li>
          <li>perfil / permissões</li>
          <li>histórico de login e actividade essencial de segurança</li>
          <li>definições da conta e preferências</li>
        </ul>

        <h3 className="mt-4 font-medium text-zinc-900 dark:text-zinc-100">Clientes da LopesTech</h3>
        <ul className="ml-5 list-disc space-y-1">
          <li>dados da empresa ou actividade</li>
          <li>NIF, morada, email de facturação</li>
          <li>plano contratado</li>
          <li>histórico de pagamentos e facturas</li>
        </ul>

        <h3 className="mt-4 font-medium text-zinc-900 dark:text-zinc-100">Dados que as lojas colocam no Reparo</h3>
        <p>
          As lojas podem inserir dados dos seus próprios clientes finais — nome, telefone, email,
          NIF, equipamento, IMEI/serial, avarias, histórico de reparações, fotos e comunicações.
          Nestes casos, <strong>a loja é responsável pelo tratamento</strong> e a LopesTech actua como
          subcontratante.
        </p>
      </Section>

      <Section title="3. Para que usamos os dados">
        <div className="overflow-x-auto rounded-lg border border-zinc-200 dark:border-zinc-800">
          <table className="w-full text-left text-sm">
            <thead className="bg-zinc-50 text-xs uppercase tracking-wide text-zinc-500 dark:bg-zinc-900">
              <tr>
                <th className="px-3 py-2">Finalidade</th>
                <th className="px-3 py-2">Base legal</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-zinc-100 dark:divide-zinc-800">
              <tr><td className="px-3 py-2">Prestar o Reparo (conta, login, loja, operação)</td><td className="px-3 py-2">Execução de contrato</td></tr>
              <tr><td className="px-3 py-2">Gerir clientes e facturação da LopesTech</td><td className="px-3 py-2">Execução de contrato / obrigação legal</td></tr>
              <tr><td className="px-3 py-2">Responder a contactos e pedidos de demo</td><td className="px-3 py-2">Diligências pré-contratuais / interesse legítimo</td></tr>
              <tr><td className="px-3 py-2">Segurança, prevenção de abuso e logs</td><td className="px-3 py-2">Interesse legítimo / segurança</td></tr>
              <tr><td className="px-3 py-2">Melhorar o produto (métricas agregadas)</td><td className="px-3 py-2">Interesse legítimo</td></tr>
              <tr><td className="px-3 py-2">Comunicações sobre o serviço (avisos, segurança, facturação)</td><td className="px-3 py-2">Execução de contrato / interesse legítimo</td></tr>
              <tr><td className="px-3 py-2">Marketing directo da LopesTech</td><td className="px-3 py-2">Consentimento / interesse legítimo</td></tr>
              <tr><td className="px-3 py-2">Cookies de analítica / marketing</td><td className="px-3 py-2">Consentimento</td></tr>
            </tbody>
          </table>
        </div>
        <p className="text-xs text-zinc-500">
          Não vendemos dados pessoais. Não usamos dados dos clientes finais das lojas para
          marketing da LopesTech.
        </p>
      </Section>

      <Section title="4. Com quem partilhamos dados">
        <p>Podemos usar fornecedores técnicos para operar o Reparo, por exemplo:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>alojamento / cloud e base de dados</li>
          <li>armazenamento de ficheiros e backups</li>
          <li>email transaccional</li>
          <li>facturação / subscrições</li>
          <li>suporte técnico</li>
          <li>analytics, se activado</li>
          <li>provider de facturação certificado, quando a loja activar integração</li>
          <li>WhatsApp / Meta ou outro provider de mensagens, quando a loja activar essa funcionalidade</li>
        </ul>
        <p>
          Estes fornecedores só podem tratar dados na medida necessária para prestar o serviço.
          Sempre que actuem como subcontratantes, devem estar sujeitos a obrigações de protecção
          de dados.
        </p>
        <p>
          Lista actualizada de subcontratantes disponível mediante pedido para{' '}
          <a href="mailto:privacidade@lopestech.pt" className="text-brand-600 hover:underline dark:text-brand-400">privacidade@lopestech.pt</a>.
        </p>
      </Section>

      <Section title="5. Transferências para fora do EEE">
        <p>
          Sempre que possível, usamos fornecedores com tratamento de dados no Espaço Económico
          Europeu. Se for necessário transferir dados para fora do EEE, aplicaremos mecanismos
          adequados previstos no RGPD — decisões de adequação, cláusulas contratuais-tipo ou
          outras garantias aplicáveis.
        </p>
      </Section>

      <Section title="6. Durante quanto tempo guardamos os dados">
        <ul className="ml-5 list-disc space-y-1">
          <li>conta Reparo: durante a vigência da conta</li>
          <li>dados de facturação: pelo prazo legal aplicável a documentos fiscais</li>
          <li>contactos comerciais sem contrato: até 24 meses após o último contacto</li>
          <li>tickets de suporte: até 24 meses</li>
          <li>logs técnicos de segurança: normalmente 90 dias</li>
          <li>backups: rotação técnica de 30 a 90 dias</li>
          <li>dados inseridos pelas lojas: durante a subscrição e período de exportação/apagamento definido nos Termos e DPA</li>
        </ul>
      </Section>

      <Section title="7. Direitos dos titulares">
        <p>Nos termos do RGPD, podes ter direito a:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>aceder aos teus dados</li>
          <li>corrigir dados incorrectos</li>
          <li>pedir apagamento</li>
          <li>limitar o tratamento</li>
          <li>opor-te a certos tratamentos</li>
          <li>pedir portabilidade</li>
          <li>retirar consentimento, quando o tratamento se baseie em consentimento</li>
          <li>apresentar reclamação junto da CNPD</li>
        </ul>
        <p>
          Para exercer direitos sobre a tua conta Reparo ou contacto com a LopesTech, escreve
          para <a href="mailto:privacidade@lopestech.pt" className="text-brand-600 hover:underline dark:text-brand-400">privacidade@lopestech.pt</a>.
        </p>
        <p>
          Se fores cliente final de uma loja que usa Reparo, contacta primeiro essa loja —
          é ela a responsável pelo tratamento dos teus dados. A LopesTech ajudará a loja a
          responder, quando necessário.
        </p>
      </Section>

      <Section title="8. Segurança">
        <p>
          Aplicamos medidas técnicas e organizativas adequadas ao risco — controlo de acessos,
          autenticação, encriptação em trânsito, backups, logs de segurança, separação por loja
          (multi-tenant) e princípio do menor acesso.
        </p>
        <p>
          Nenhum sistema é 100% imune a incidentes. Se ocorrer uma violação de dados pessoais
          com impacto relevante, seguiremos os procedimentos previstos no RGPD e, quando
          aplicável, notificaremos as entidades e pessoas afectadas.
        </p>
      </Section>

      <Section title="9. Alterações a esta política">
        <p>
          Podemos actualizar esta política quando o produto, fornecedores ou requisitos legais
          mudarem. A versão mais recente fica sempre disponível nesta página.
        </p>
      </Section>
    </LegalLayout>
  );
}
