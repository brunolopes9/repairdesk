/**
 * mender-faturas — Email Worker da LopesTech (Mender, Sprint 632, Doc 98 F5).
 * Recebe os emails enviados para faturas@lopestech.pt e entrega-os ao Mender, que tira os PDFs das faturas
 * e os põe «por aprovar». O que não tiver PDF (ex.: código de confirmação do Gmail) ou falhar volta para o
 * email do Bruno (ENCAMINHAR) — nada se perde.
 * Variáveis: MENDER_URL (texto), ENCAMINHAR (texto, endereço já verificado no Email Routing), MENDER_SECRET (secret).
 */
export default {
  async email(message, env, ctx) {
    let devolver = true;
    try {
      const raw = await new Response(message.raw).arrayBuffer();
      const res = await fetch(env.MENDER_URL, {
        method: "POST",
        headers: {
          "Content-Type": "message/rfc822",
          "X-Para": message.to,
          "X-Webhook-Secret": env.MENDER_SECRET || "",
        },
        body: raw,
      });
      if (res.ok) {
        const r = await res.json();
        devolver = Boolean(r.semPdf) || r.falhas > 0;
      } else {
        console.log("Mender respondeu " + res.status);
      }
    } catch (e) {
      console.log("Falhou o envio para o Mender: " + e);
    }
    if (devolver && env.ENCAMINHAR) {
      await message.forward(env.ENCAMINHAR);
    }
  },

  async fetch() {
    return new Response("mender-faturas: só recebe emails.", { status: 200 });
  },
};
