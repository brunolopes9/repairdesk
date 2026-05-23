import LegalLayout, { Section } from './LegalLayout';

export default function Termos() {
  return (
    <LegalLayout title="Termos de Serviço" lastUpdated="2026-05-17">
      <p>
        Estes Termos regulam a utilização do Reparo, um software SaaS de gestão para
        oficinas de reparação. Ao criar conta ou usar o Reparo, a loja aceita estes Termos.
      </p>

      <Section title="1. Quem presta o serviço">
        <p>O Reparo é prestado pela LopesTech, projecto de Bruno Lopes, em Portugal.</p>
        <p>
          Contacto geral: <a href="mailto:geral@lopestech.pt" className="text-brand-600 hover:underline dark:text-brand-400">geral@lopestech.pt</a><br />
          Contacto privacidade: <a href="mailto:privacidade@lopestech.pt" className="text-brand-600 hover:underline dark:text-brand-400">privacidade@lopestech.pt</a>
        </p>
      </Section>

      <Section title="2. Quem pode usar">
        <p>
          O Reparo destina-se a profissionais, empresas e trabalhadores independentes
          que gerem reparações, clientes, equipamentos, orçamentos, despesas e operação de
          oficina.
        </p>
        <p>
          Ao usar o Reparo em nome de uma loja ou empresa, declaras que tens autorização
          para aceitar estes Termos em nome dessa entidade.
        </p>
      </Section>

      <Section title="3. O que o Reparo faz">
        <p>O Reparo ajuda a gerir:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>clientes</li>
          <li>reparações e estados de reparação</li>
          <li>equipamentos e IMEI / serial</li>
          <li>orçamentos e documentos operacionais não fiscais</li>
          <li>custos, despesas e margem</li>
          <li>portal cliente público</li>
          <li>comunicações e integrações futuras</li>
        </ul>
        <p>
          Salvo indicação expressa em contrário, o Reparo não substitui aconselhamento
          jurídico, fiscal, contabilístico ou técnico.
        </p>
      </Section>

      <Section title="4. Documentos fiscais e facturação">
        <p>
          Enquanto o Reparo não tiver módulo fiscal certificado ou integração certificada
          activa, os documentos gerados <strong>são documentos operacionais</strong> (orçamentos,
          fichas de reparação, garantias) e <strong>não substituem facturas legalmente
          obrigatórias</strong>.
        </p>
        <p>
          Quando existir integração com provider de facturação certificado, a emissão fiscal
          é feita através desse provider, nos termos e limites da integração.
        </p>
        <p>A loja continua responsável por cumprir as suas obrigações fiscais e contabilísticas.</p>
      </Section>

      <Section title="5. Conta e segurança">
        <p>A loja é responsável por:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>usar dados correctos</li>
          <li>proteger credenciais de acesso</li>
          <li>dar acesso apenas a pessoas autorizadas</li>
          <li>remover utilizadores que já não trabalhem na loja</li>
          <li>verificar que os dados inseridos são lícitos e necessários</li>
        </ul>
        <p>
          A LopesTech pode suspender acessos em caso de abuso, risco de segurança,
          utilização ilegal ou incumprimento grave destes Termos.
        </p>
      </Section>

      <Section title="6. Propriedade dos dados">
        <p>
          Os dados inseridos pela loja no Reparo <strong>pertencem sempre à loja</strong> ou aos
          respectivos titulares, conforme aplicável.
        </p>
        <p>
          A LopesTech não vende os dados da loja, não os usa para marketing próprio e não os
          usa para competir com a loja.
        </p>
        <p>
          A loja pode pedir exportação dos seus dados em formato razoável e utilizável. O
          objectivo do Reparo é evitar lock-in.
        </p>
      </Section>

      <Section title="7. Protecção de dados">
        <p>
          Para dados dos clientes finais da loja, a <strong>loja é responsável pelo tratamento</strong>{' '}
          e a LopesTech actua como subcontratante.
        </p>
        <p>
          O tratamento destes dados é regulado pelo Contrato de Processamento de Dados (DPA),
          que faz parte destes Termos. Ver também a{' '}
          <a href="/privacidade" className="text-brand-600 hover:underline dark:text-brand-400">Política de Privacidade</a>.
        </p>
      </Section>

      <Section title="8. Planos, pagamentos e cancelamento">
        <p>Os planos e preços são apresentados na página de pricing ou proposta comercial aceite.</p>
        <p>Salvo acordo diferente:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>a subscrição é mensal ou anual</li>
          <li>a loja pode cancelar no fim do período pago</li>
          <li>não há fidelização obrigatória nos planos standard</li>
          <li>depois do cancelamento, a conta fica em modo leitura/exportação durante 30 dias</li>
          <li>após esse período, os dados podem ser apagados de sistemas activos e, posteriormente, dos backups por rotação técnica</li>
        </ul>
        <p>
          Durante beta, podem existir condições especiais, descontos ou acesso gratuito,
          definidos por escrito.
        </p>
      </Section>

      <Section title="9. Disponibilidade e SLA">
        <p>A LopesTech fará esforços razoáveis para manter o Reparo disponível e seguro.</p>
        <p>SLA durante beta:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>objectivo de disponibilidade: 99% mensal, excluindo manutenções programadas, falhas de terceiros, força maior e problemas fora do controlo razoável da LopesTech</li>
          <li>suporte em horário útil português, com resposta inicial pretendida em até 2 dias úteis</li>
          <li>incidentes críticos de segurança ou indisponibilidade prolongada têm prioridade</li>
        </ul>
        <p>
          Durante beta, o serviço pode ter alterações, bugs e manutenções mais frequentes. A
          LopesTech comunicará incidentes relevantes de forma honesta.
        </p>
      </Section>

      <Section title="10. Backups">
        <p>
          A LopesTech mantém backups técnicos com periodicidade adequada. Backups <strong>não
          substituem</strong> a obrigação da loja de exportar e guardar documentos que tenha de
          conservar legalmente.
        </p>
      </Section>

      <Section title="11. Uso aceitável">
        <p>Não é permitido usar o Reparo para:</p>
        <ul className="ml-5 list-disc space-y-1">
          <li>actividades ilegais</li>
          <li>inserir dados obtidos de forma ilícita</li>
          <li>tentar aceder a contas ou dados de outras lojas</li>
          <li>interferir com a segurança ou disponibilidade do serviço</li>
          <li>enviar spam ou comunicações sem legitimidade</li>
          <li>carregar malware ou conteúdo abusivo</li>
        </ul>
      </Section>

      <Section title="12. Limitação de responsabilidade">
        <p>
          Na medida permitida por lei, a LopesTech não será responsável por perdas indirectas,
          lucros cessantes, perda de negócio, perda de reputação ou danos resultantes de uso
          indevido do Reparo pela loja.
        </p>
        <p>
          A responsabilidade total da LopesTech por danos relacionados com o serviço fica
          limitada ao valor pago pela loja nos 3 meses anteriores ao evento que originou a
          responsabilidade, salvo nos casos em que a lei não permita essa limitação.
        </p>
      </Section>

      <Section title="13. Alterações ao serviço e aos Termos">
        <p>
          A LopesTech pode melhorar, alterar ou remover funcionalidades, tentando evitar
          impacto injustificado no uso normal da loja.
        </p>
        <p>
          Alterações materiais aos Termos serão comunicadas com antecedência razoável. Se a
          loja não aceitar alterações materiais, pode cancelar a subscrição e exportar os dados.
        </p>
      </Section>

      <Section title="14. Lei e foro">
        <p>
          Estes Termos são regidos pela lei portuguesa. Salvo norma legal imperativa em
          contrário, qualquer litígio será submetido aos tribunais portugueses competentes.
        </p>
      </Section>
    </LegalLayout>
  );
}
